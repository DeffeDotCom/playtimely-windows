using System.Diagnostics;
using System.Management;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace GameControl;
public sealed class AgentWorker(ILogger<AgentWorker> logger):BackgroundService {
    readonly GracefulShutdown shutdown=new();
    private static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GameControl");
    protected override async Task ExecuteAsync(CancellationToken stop) {
        var connection=JsonSerializer.Deserialize<Connection>(await File.ReadAllTextAsync(Path.Combine(Root,"connection.json"),stop),PolicyEngine.Json) ?? throw new InvalidDataException("Missing connection");
        if(connection.SchemaVersion!=1||connection.DeviceId<1||connection.Token.Length!=64||!Uri.TryCreate(connection.PolicyUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)) throw new InvalidDataException("A valid HTTPS connection is required");
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(10),MaxResponseContentBufferSize=262144};
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",connection.Token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PlaytimelyAgent/0.13.3");
        client.DefaultRequestHeaders.Add("X-Playtimely-Policy","10");
        client.DefaultRequestHeaders.Add("X-Playtimely-Catalog","2");
        client.DefaultRequestHeaders.Add("X-Playtimely-Receipt","1");
        var budgetReceiptScope=File.GetLastWriteTimeUtc(Path.Combine(Root,"connection.json")).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string cache=Path.Combine(Root,"policy.json");Policy? current=null;
        if(File.Exists(cache)) {
            try { current=PolicyEngine.Verify(await File.ReadAllTextAsync(cache,stop),connection); }
            catch(Exception ex) when(ex is not OperationCanceledException) { logger.LogWarning("Cached policy rejected ({Type}). Waiting for valid rules.",ex.GetType().Name); }
        }
        string budgetPath=Path.Combine(Root,"daily-budget.json");
        DailyBudget budget;bool budgetHealthy=true;
        try {budget=DailyBudget.Load(budgetPath);}catch {budget=new();budgetHealthy=false;logger.LogWarning("Daily budget state needs repair; scheduled games remain paused while a budget is enabled.");}
        var usage = new UsageSnapshot("", 0);
        string historyPath=Path.Combine(Root,"usage-history.json");
        UsageHistory history;
        try {history=UsageHistory.Load(historyPath);}catch {history=new();logger.LogWarning("Usage history state unreadable; starting a new measured history segment.");}
        if(history.DeviceId!=connection.DeviceId)history=new UsageHistory{DeviceId=connection.DeviceId};
        string? historyReport=null;
        var historyUri=new Uri(uri,"/api/v1/usage");
        var requestsUri=new Uri(uri,"/api/v1/requests");
        var snapshot=new AgentSnapshot(current,null,[]);
        using var policyGate=new SemaphoreSlim(1,1);
        var requestServer=RequestPipe.Serve(async (wish,cancel)=>{
            var policy=PolicyEngine.Effective(Volatile.Read(ref snapshot).Policy,DateTimeOffset.UtcNow);
            if(wish.App=="chores"){
                if(policy?.Chores is not {} chores||chores.Revision!=wish.Revision||!chores.Today(DateTimeOffset.UtcNow,policy.Timezone).Any(t=>t.Id==wish.TaskId))return "invalid";
                try{
                    await policyGate.WaitAsync(cancel);
                    try {
                    using var result=await client.PostAsJsonAsync(new Uri(uri,"/api/v1/chores/check"),new {task_id=wish.TaskId,day=wish.Day,revision=wish.Revision,done=wish.Option=="done"},cancel);
                    if(!result.IsSuccessStatusCode)return "rejected";
                    using var response=await client.GetAsync(uri,cancel);response.EnsureSuccessStatusCode();var json=await response.Content.ReadAsStringAsync(cancel);var updated=PolicyEngine.Verify(json,connection,Volatile.Read(ref snapshot).Policy?.IssuedAt);
                    await File.WriteAllTextAsync(cache+".tmp",json,cancel);File.Move(cache+".tmp",cache,true);
                    Volatile.Write(ref snapshot,Volatile.Read(ref snapshot) with {Policy=updated,LastSync=DateTimeOffset.UtcNow});
                    return "saved";
                    } finally {policyGate.Release();}
                }catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or InvalidDataException or IOException){return "offline";}
            }
            if(policy is null||!(wish.App=="daily_budget"?policy.DailyBudget is not null:policy.Rules.Any(r=>r.App==wish.App&&(r.Mode!="allowed"||policy.DailyBudget is not null))))return "invalid";
            try {
                using var response=await client.PostAsJsonAsync(requestsUri,wish,PolicyEngine.Json,cancel);
                if(response.IsSuccessStatusCode)return "sent";
                return (int)response.StatusCode==429?"wait":"rejected";
            }catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException){return "offline";}
        },stop);
        // Network I/O must never suspend the local enforcement/status heartbeat.
        var polling=PollAsync();
        async Task PollAsync() {
            string? etag=null;
            try { await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0,15)),stop); }
            catch(OperationCanceledException) { return; }
            while(!stop.IsCancellationRequested) {
                var previous=Volatile.Read(ref snapshot);
                try {
                    await policyGate.WaitAsync(stop);
                    try {
                    previous=Volatile.Read(ref snapshot);
                    using var request=new HttpRequestMessage(HttpMethod.Get,uri);
                    request.Headers.TryAddWithoutValidation("X-Playtimely-Account-Safety",InteractiveAccountSafety.Read());
                    if(etag is not null) {
                        request.Headers.TryAddWithoutValidation("If-None-Match",etag);
                        request.Headers.TryAddWithoutValidation("X-Playtimely-Ack",etag.Trim('"'));
                    }
                    var report=Volatile.Read(ref usage);
                    if(report.Day.Length>0){request.Headers.TryAddWithoutValidation("X-Playtimely-Usage-Day",report.Day);request.Headers.TryAddWithoutValidation("X-Playtimely-Usage-Seconds",report.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));}
                    using var response=await client.SendAsync(request,stop);
                    var policy=previous.Policy;
                    if(response.StatusCode!=System.Net.HttpStatusCode.NotModified) {
                        response.EnsureSuccessStatusCode();
                        string json=await response.Content.ReadAsStringAsync(stop);
                        policy=PolicyEngine.Verify(json,connection,previous.Policy?.IssuedAt);
                        await File.WriteAllTextAsync(cache+".tmp",json,stop);File.Move(cache+".tmp",cache,true);
                        etag=response.Headers.ETag?.ToString();
                    }
                    Volatile.Write(ref snapshot,previous with {Policy=policy,LastSync=DateTimeOffset.UtcNow,SubscriptionState=response.Headers.TryGetValues("X-Playtimely-Subscription",out var subscriptionHeaders)&&subscriptionHeaders.FirstOrDefault()=="expired"?"expired":"active"});
                    } finally {policyGate.Release();}
                } catch(OperationCanceledException) when(stop.IsCancellationRequested) { return; }
                catch(Exception ex) { logger.LogWarning("Rule fetch failed ({Type}). Keeping last verified policy.",ex.GetType().Name); }
                try {
                    using var result=await client.GetAsync(requestsUri,stop);
                    if(result.IsSuccessStatusCode) {
                        var state=await result.Content.ReadFromJsonAsync<RequestStatus>(PolicyEngine.Json,stop);
                        if(state?.Requests is {Length:<=10}) Volatile.Write(ref snapshot,Volatile.Read(ref snapshot) with {Requests=state.Requests});
                    }
                } catch(OperationCanceledException) when(stop.IsCancellationRequested) { return; }
                catch(Exception ex) { logger.LogWarning("Request status fetch failed ({Type}).",ex.GetType().Name); }
                try {
                    var report=Volatile.Read(ref historyReport);
                    if(report is not null) {
                        using var payload=new StringContent(report,System.Text.Encoding.UTF8,"application/json");
                        using var response=await client.PostAsync(historyUri,payload,stop);
                        if(!response.IsSuccessStatusCode)logger.LogWarning("Usage report pending retry ({Status}).",(int)response.StatusCode);
                    }
                } catch(OperationCanceledException) when(stop.IsCancellationRequested) { return; }
                catch(Exception ex) { logger.LogWarning("Usage report pending retry ({Type}).",ex.GetType().Name); }
                try { await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(55,66)),stop); }
                catch(OperationCanceledException) { return; }
            }
        }
        var scans=new ProcessScanSchedule();Policy? scannedPolicy=null;
        var tracked=new List<TrackedGame>();
        string savedBudget="";
        var historyCheckpoint=new HistoryCheckpoint();
        var notices=new GameNotices();
        while(!stop.IsCancellationRequested) {
            var state=Volatile.Read(ref snapshot);
            var now=DateTimeOffset.UtcNow;
            current=PolicyEngine.Effective(state.Policy,now);
            var manual=current?.GameStop is {} requestedStop&&requestedStop.Active(now)?requestedStop:null;
            bool choresBlocked=current?.Chores?.Blocks(now,current.Timezone)??false;
            int? budgetRemaining=null;
            Policy? effective=current;
            // Discover new processes at most every 30 seconds (or immediately after a policy change).
            // Between scans, query only already-known PIDs and verify creation time against PID reuse.
            if(scans.IsDue(Environment.TickCount64/1000d,manual is not null&&manual.Action!="notify" || choresBlocked || ProcessScanSchedule.NeedsFastDiscovery(current?.DailyBudget?.Action,current?.DailyBudget is {} nearing ? budget.Remaining(nearing.Minutes(now,current.Timezone)) : null,budget.Exhaustion.BeganAt is not null) || !ReferenceEquals(scannedPolicy?.Rules,current?.Rules) || !ReferenceEquals(scannedPolicy?.DailyBudget,current?.DailyBudget))){
                scannedPolicy=current;tracked.Clear();
                if(current is not null){
                    IEnumerable<Rule> candidates=current.Rules;
                    if(current is not null){
                        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach(var process in Process.GetProcesses())using(process){try{names.Add(process.ProcessName);}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}}
                        candidates=GameCatalog.MatchingProcesses(names)
                            .Select(g=>PolicyEngine.RuleFor(current,g.Key) ?? new Rule(g.Key,g.Processes,"allowed","00:00","23:59",[1,2,3,4,5,6,7])).Concat(current.Rules).DistinctBy(r=>r.App);
                    }
                    foreach(var rule in candidates)WithGameProcesses(rule,p=>tracked.Add(new TrackedGame(rule,p.Id,p.StartTime.ToUniversalTime().Ticks)));
                }
            }
            tracked.RemoveAll(t=>!WithTrackedProcess(t,_=>{}));
            var observedRules=tracked.Select(t=>t.Rule).DistinctBy(r=>r.App).ToArray();
            if(current is not null) {
                history.Tick(now,current.Timezone,Environment.TickCount64/1000d,observedRules.Select(r=>r.App));
                try {
                    // History is informational; budget/exhaustion state remains persisted immediately.
                    // A checkpoint interval of history may be lost on abrupt termination.
                    historyCheckpoint.SaveWhenDue(Environment.TickCount64/1000d,()=>{
                        history.Save(historyPath);
                        if(history.Counters.Count>0)Volatile.Write(ref historyReport,JsonSerializer.Serialize(history.Report()));
                    });
                }catch(Exception ex){logger.LogWarning("Usage history could not be persisted ({Type}).",ex.GetType().Name);}
            }
            budget.ManualStop.Update(manual?.Id??"",manual is null?null:0,manual?.Action??"stop",now,Environment.TickCount64/1000d,tracked.Select(t=>$"{t.Pid}:{t.Started}"));
            if(manual is not null&&current is not null&&current.DailyBudget is null)budget.Tick(now,current.Timezone,Environment.TickCount64/1000d,false);
            var runningGames=observedRules.Select(r=>new RunningGame(r.App,PolicyEngine.UsesBudget(current!,r,now),PolicyEngine.HasExtraTime(r,now))).ToArray();
            if(current?.DailyBudget is {} limit) {
                bool playing=runningGames.Any(game=>game.UsesBudget);
                budget.Tick(now,current.Timezone,Environment.TickCount64/1000d,playing);

                budgetRemaining=budgetHealthy?budget.Remaining(limit.Minutes(now,current.Timezone)):0;
                Volatile.Write(ref usage,new UsageSnapshot(budget.Day,(int)Math.Ceiling(budget.UsedSeconds)));
                budget.Exhaustion.Update(budget.Day,budgetRemaining,budgetHealthy?limit.Action:"stop",now,Environment.TickCount64/1000d,tracked.Select(t=>$"{t.Pid}:{t.Started}"));
                // Public rule projection describes whether NEW sessions can start, not ongoing ones.
                if(budgetRemaining==0 && limit.Action is "stop" or "finish" || budgetRemaining==0 && limit.Action=="grace" && budget.Exhaustion.GraceExpired)
                    effective=current with {Rules=current.Rules.Select(r=>!PolicyEngine.HasExtraTime(r,now)?r with {Mode="blocked"}:r).ToArray()};
            }
            else budget.Exhaustion.Update(budget.Day,null,"stop",now,Environment.TickCount64/1000d,[]);
            try {var serialized=JsonSerializer.Serialize(budget);if(budgetHealthy&&budget.Day.Length>0&&serialized!=savedBudget){budget.Save(budgetPath);savedBudget=serialized;}}catch {budgetHealthy=false;}
            notices.Observe(tracked.GroupBy(t=>$"{t.Pid}:{t.Started}").ToDictionary(g=>g.Key,g=>g.First().Rule.App),now,budgetRemaining==0,budgetHealthy?(int)Math.Ceiling(budget.UsedSeconds):null,current?.DailyBudget?.Minutes(now,current.Timezone),tracked.Where(t=>current is not null&&!PolicyEngine.HasExtraTime(PolicyEngine.RuleFor(current,t.Rule.App)??t.Rule,now)&&PolicyEngine.UsesBudget(current,PolicyEngine.RuleFor(current,t.Rule.App)??t.Rule,now)).Select(t=>$"{t.Pid}:{t.Started}").ToHashSet());
            var closing=new List<ClosingGame>();
            var blocked=new HashSet<(int Pid,long Start)>();
            void StopGame(Process process,string app,string reason,bool force=false){
                long started=process.StartTime.ToUniversalTime().Ticks;
                if(!blocked.Add((process.Id,started)))return;
                notices.Stopping($"{process.Id}:{started}",app,reason,force,now,budgetHealthy?(int)Math.Ceiling(budget.UsedSeconds):null,current?.DailyBudget?.Minutes(now,current.Timezone));
                if(force||shutdown.ShouldForce(process.Id,started,Environment.TickCount64))process.Kill();
                else if(closing.Count<256)closing.Add(new ClosingGame(app,process.Id,started));
            }
            if(current is not null)foreach(var target in tracked){
                var rule=PolicyEngine.RuleFor(current,target.Rule.App) ?? target.Rule;
                var identity=$"{target.Pid}:{target.Started}";
                bool covered=current.DailyBudget is {} activeLimit && (activeLimit.Scope=="all_known"||PolicyEngine.RuleFor(current,rule.App) is not null);
                bool exhausted=covered&&budgetRemaining==0&&!PolicyEngine.HasExtraTime(rule,now);
                bool budgetBlocked=exhausted&&(!budgetHealthy||budget.Exhaustion.Blocks(identity));
                bool manualBlocked=manual is not null&&budget.ManualStop.Blocks(identity);
                if(manualBlocked||choresBlocked||budgetBlocked||!PolicyEngine.IsAllowed(rule,now,current.Timezone))WithTrackedProcess(target,p=>StopGame(p,rule.App,manualBlocked?"parent_stop":choresBlocked?"chores":budgetBlocked?(budgetHealthy?(current.DailyBudget?.Action=="grace"?"budget_grace":"budget"):"repair"):rule.Mode=="blocked"||rule.OverrideMode=="blocked"&&rule.OverrideUntil>now?"paused":"schedule",manualBlocked&&(manual?.Action=="stop"||budget.ManualStop.ForceNow(identity))||budgetBlocked&&budget.Exhaustion.ForceNow(identity)));
            }
            shutdown.RetainOnly(blocked);
            notices.RetainStopping(blocked.Select(p=>$"{p.Pid}:{p.Start}"));
            try {
                var statusPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PlaytimelyStatus","status.json");
                // Installer owns directory ACLs. Never create a public writable fallback.
                if(Directory.Exists(Path.GetDirectoryName(statusPath))) {
                    var display=DisplayStatus.Create(effective,now,state.LastSync,state.Requests);
                    await File.WriteAllTextAsync(statusPath+".tmp",JsonSerializer.Serialize(display with {GameStop=manual,SubscriptionState=Volatile.Read(ref snapshot).SubscriptionState,BudgetReceiptScope=budgetReceiptScope,Notices=notices.Snapshot(),BudgetUsedSeconds=budgetHealthy&&current?.DailyBudget is not null?(int)Math.Ceiling(budget.UsedSeconds):null,BudgetLimitMinutes=current?.DailyBudget?.Minutes(now,current.Timezone),PlayedToday=history.Counters.Where(c=>c.Day==TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(current?.Timezone??"UTC")).ToString("yyyy-MM-dd")&&GameCatalog.Contains(c.App)).OrderByDescending(c=>c.Seconds).Take(100).Select(c=>new PlayedGame(c.App,(int)Math.Floor(c.Seconds))).ToArray(),ClosingGames=closing.ToArray(),RunningGames=runningGames.ToArray(),BudgetScope=current?.DailyBudget?.Scope,BudgetRemainingSeconds=budgetRemaining,BudgetAction=current?.DailyBudget?.Action,BudgetExhaustedAt=budgetRemaining==0?budget.Exhaustion.BeganAt:null,BudgetGraceEndsAt=budgetRemaining==0?budget.Exhaustion.Deadline:null,Rules=display.Rules.Select(r=>r with {BudgetBlocked=budgetRemaining==0 && current!.Rules.Any(original=>original.App==r.App) && r.Mode=="blocked"}).ToArray()},PolicyEngine.Json),stop);
                    File.Move(statusPath+".tmp",statusPath,true);
                }
            } catch(OperationCanceledException) when(stop.IsCancellationRequested) { break; }
            catch(Exception ex) { logger.LogWarning("Status display update failed ({Type}).",ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(2),stop); } catch(OperationCanceledException) { break; }
        }
        try {history.Save(historyPath);}catch(Exception ex){logger.LogWarning("Final usage history save failed ({Type}).",ex.GetType().Name);}
        try { await Task.WhenAll(polling,requestServer); } catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
    }
    private sealed record TrackedGame(Rule Rule,int Pid,long Started);
    private static bool WithTrackedProcess(TrackedGame target,Action<Process> action){
        try {using var p=Process.GetProcessById(target.Pid);if(p.HasExited||p.SessionId==0||p.StartTime.ToUniversalTime().Ticks!=target.Started)return false;action(p);return true;}
        catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException){return false;}
    }
    private sealed record UsageSnapshot(string Day,int Seconds);
    private sealed record AgentSnapshot(Policy? Policy,DateTimeOffset? LastSync,DisplayRequest[] Requests,string? SubscriptionState=null);
    private static void WithGameProcesses(Rule rule,Action<Process> action) {
        foreach(var name in rule.Processes)foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name)))using(process) {
            try {
                if(process.SessionId==0)continue;
                if(name.Equals("Windows10Universal.exe",StringComparison.OrdinalIgnoreCase)&&!(process.MainModule?.FileName.Contains("ROBLOX",StringComparison.OrdinalIgnoreCase)??false))continue;
                if(name.Equals("busybox64.exe",StringComparison.OrdinalIgnoreCase))continue;
                if(name.Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase)) {
                    string? command=null;
                    try {using var item=new ManagementObject("Win32_Process.Handle='"+process.Id+"'");command=item["CommandLine"] as string;}
                    catch(ManagementException){}catch(System.Runtime.InteropServices.COMException){}
                    if(!GameCatalog.IsGameRuntime(rule.App,name,command))continue;
                }
                action(process);
            }catch(Exception ex)when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}
        }
        if(rule.App=="minecraft")StopMinecraftJava(action);
    }
    private static void StopMinecraftJava(Action<Process> stop) {
        try {
            using var search=new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='javaw.exe' OR Name='java.exe'");
            using var results=search.Get();
            foreach(ManagementObject item in results) using(item) {
                var command=item["CommandLine"] as string;
                // Never kill Java globally. Only the explicit Minecraft client main class.
                if(command is null || !command.Contains("net.minecraft.client.main.Main",StringComparison.Ordinal)) continue;
                try { using var process=Process.GetProcessById(Convert.ToInt32(item["ProcessId"]));if(process.SessionId!=0) stop(process); }
                catch(Exception ex) when(ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
            }
        } catch(ManagementException) { } catch(System.Runtime.InteropServices.COMException) { }
    }
}
