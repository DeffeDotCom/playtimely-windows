using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace GameControl;
public record Connection(int SchemaVersion, long DeviceId, string PolicyUrl, string Token, string PublicKey);
public record Envelope(string Payload,string Signature,string Algorithm);
public record BudgetLimit(int WeekdayMinutes,int WeekendMinutes,string Scope="selected",string Action="stop",int ExtraMinutes=0,string? ExtraDay=null) {
 public int Minutes(DateTimeOffset now,string timezone){var local=TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(timezone));return Math.Max(0,(local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday?WeekendMinutes:WeekdayMinutes)+(local.ToString("yyyy-MM-dd")==ExtraDay?ExtraMinutes:0));}
}
public record Policy(int SchemaVersion,long DeviceId,DateTimeOffset IssuedAt,string Timezone,int PollSeconds,Rule[] Rules,BudgetLimit? DailyBudget=null,TemporarySchedule? TemporarySchedule=null,ChoreList? Chores=null,GameStop? GameStop=null);
public record GameStop(string Id,string Action,DateTimeOffset StartsAt,DateTimeOffset EndsAt) {public bool Active(DateTimeOffset now)=>now>=StartsAt&&now<EndsAt;}
public record ChoreTask(string Id,int Day,string Label);
public record ChoreList(long Revision,string CompletedDay,string[] CompletedIds,ChoreTask[] Tasks) {
 public ChoreTask[] Today(DateTimeOffset now,string timezone){var local=TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(timezone));int day=(int)local.DayOfWeek;if(day==0)day=7;return Tasks.Where(t=>t.Day==day).ToArray();}
 public bool Done(string id,DateTimeOffset now,string timezone)=>TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(timezone)).ToString("yyyy-MM-dd")==CompletedDay&&CompletedIds.Contains(id);
 public bool Blocks(DateTimeOffset now,string timezone)=>Today(now,timezone).Any(t=>!Done(t.Id,now,timezone));
 public void Validate(){if(Revision<1||Tasks is null||Tasks.Length>70||CompletedIds is null||CompletedIds.Length>70||!DateOnly.TryParseExact(CompletedDay,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _))throw new InvalidDataException("Invalid checklist");var ids=new HashSet<string>();foreach(var t in Tasks)if(!Guid.TryParse(t.Id,out _)||!ids.Add(t.Id)||t.Day is <1 or >7||string.IsNullOrWhiteSpace(t.Label)||t.Label.Length>160)throw new InvalidDataException("Invalid checklist task");if(CompletedIds.Any(id=>!ids.Contains(id))||CompletedIds.Distinct().Count()!=CompletedIds.Length)throw new InvalidDataException("Invalid checklist completion");}
}
public record TemporarySchedule(DateTimeOffset StartsAt,DateTimeOffset EndsAt,Rule[] Rules,BudgetLimit? DailyBudget=null);
public record TimeWindow(string Start,string End,int[] Days);
public record Rule(string App,string[] Processes,string Mode,string Start,string End,int[] Days,DateTimeOffset? AllowedUntil=null,TimeWindow[]? Windows=null,string? OverrideMode=null,DateTimeOffset? OverrideUntil=null);
public static class PolicyEngine {
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive=false, UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    // Fixed allowlist is intentional: even a signed policy cannot target arbitrary Windows processes.
    public static Policy Verify(string input,Connection connection,DateTimeOffset? minimumIssuedAt=null) {
        if(input.Length>262144) throw new InvalidDataException("Policy too large");
        var e=JsonSerializer.Deserialize<Envelope>(input,Json) ?? throw new InvalidDataException("Missing envelope");
        if(e.Algorithm!="RS256") throw new InvalidDataException("Unsupported signature algorithm");
        byte[] raw=Convert.FromBase64String(e.Payload);byte[] signature=Convert.FromBase64String(e.Signature);
        using var rsa=RSA.Create();rsa.ImportFromPem(connection.PublicKey);
        if(!rsa.VerifyData(raw,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)) throw new InvalidDataException("Invalid signature");
        var p=JsonSerializer.Deserialize<Policy>(raw,Json) ?? throw new InvalidDataException("Missing policy");
        if(p.SchemaVersion!=1||p.DeviceId!=connection.DeviceId||p.PollSeconds!=60||p.Rules is null||p.Rules.Length>30) throw new InvalidDataException("Unsupported policy");
        if(p.IssuedAt>DateTimeOffset.UtcNow.AddMinutes(5)|| (minimumIssuedAt.HasValue&&p.IssuedAt<minimumIssuedAt)) throw new InvalidDataException("Policy time rejected");
        _=TimeZoneInfo.FindSystemTimeZoneById(p.Timezone);
        if(p.TemporarySchedule is {} temporary) {
            if(temporary.StartsAt>=temporary.EndsAt || temporary.EndsAt-temporary.StartsAt>TimeSpan.FromDays(91) || temporary.StartsAt>p.IssuedAt.AddDays(91) || temporary.Rules is null || temporary.Rules.Length>30)throw new InvalidDataException("Invalid temporary routine");
            ValidateRules(temporary.Rules,temporary.DailyBudget,p.IssuedAt);
        }
        if(p.GameStop is {} stop && (!Guid.TryParse(stop.Id,out _) || stop.Action is not ("stop" or "notify" or "finish" or "grace") || stop.StartsAt>=stop.EndsAt || stop.EndsAt-stop.StartsAt>TimeSpan.FromHours(26) || stop.StartsAt>p.IssuedAt.AddMinutes(5)))throw new InvalidDataException("Invalid game stop");
        p.Chores?.Validate();
        ValidateRules(p.Rules,p.DailyBudget,p.IssuedAt);
        return p;
    }
    private static void ValidateRules(Rule[] rules,BudgetLimit? budget,DateTimeOffset issuedAt) {
        if(budget is {} b && (b.WeekdayMinutes is <15 or >720 || b.WeekendMinutes is <15 or >720 || b.ExtraMinutes is < -720 or >720 || (b.ExtraDay is not null && !DateOnly.TryParseExact(b.ExtraDay,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _)) || (b.ExtraMinutes!=0 && b.ExtraDay is null) || b.Scope is not ("selected" or "all_known") || b.Action is not ("stop" or "notify" or "finish" or "grace")))throw new InvalidDataException("Invalid daily budget");
        var keys=new HashSet<string>();
        foreach(var rule in rules) {
            if((rule.OverrideMode is not (null or "blocked" or "allowed")) || (rule.OverrideMode is null)!=(rule.OverrideUntil is null) || rule.OverrideUntil>issuedAt.AddHours(26)) throw new InvalidDataException("Invalid temporary control");
            if(rule.Windows is {} windows) {
                if(windows.Length>14)throw new InvalidDataException("Too many windows");
                foreach(var w in windows)if(w.Days is null||w.Days.Length is <1 or >7||w.Days.Any(d=>d<1||d>7)||w.Days.Distinct().Count()!=w.Days.Length||!TimeOnly.TryParseExact(w.Start,"HH:mm",out var ws)||!TimeOnly.TryParseExact(w.End,"HH:mm",out var we)||ws==we)throw new InvalidDataException("Invalid window");
            }
            if(rule.AllowedUntil is {} until && until>issuedAt.AddHours(26)) throw new InvalidDataException("Invalid temporary permission");
            if(rule.App=="all_known" && !keys.Add(rule.App))throw new InvalidDataException("Duplicate shared rule");
            if(rule.App=="all_known" && (rule.Processes is null || rule.Processes.Length!=0))throw new InvalidDataException("Invalid shared rule");
            if(rule.App!="all_known" && (rule.App is null||!GameCatalog.Contains(rule.App)||!keys.Add(rule.App)||rule.Processes is null||rule.Processes.Length==0||rule.Processes.Length>10||rule.Processes.Any(x=>!GameCatalog.Allows(rule.App,x)))) throw new InvalidDataException("Unknown application");
            if(rule.Mode is not ("blocked" or "schedule" or "allowed")||rule.Days is null||rule.Days.Length==0||rule.Days.Length>7||rule.Days.Any(d=>d<1||d>7)||rule.Days.Distinct().Count()!=rule.Days.Length) throw new InvalidDataException("Invalid rule");
            if(!TimeOnly.TryParseExact(rule.Start,"HH:mm",out var start)||!TimeOnly.TryParseExact(rule.End,"HH:mm",out var end)||start==end) throw new InvalidDataException("Invalid schedule");
        }
    }
    public static Policy? Effective(Policy? policy,DateTimeOffset now) => policy?.TemporarySchedule is {} temporary && now>=temporary.StartsAt && now<temporary.EndsAt
        ? policy with {Rules=temporary.Rules,DailyBudget=temporary.DailyBudget,TemporarySchedule=null} : policy;
    public static Rule? RuleFor(Policy policy,string app) => policy.Rules.FirstOrDefault(r=>r.App==app) ?? (GameCatalog.Contains(app) && policy.Rules.FirstOrDefault(r=>r.App=="all_known") is {} shared ? shared with {App=app,Processes=GameCatalog.All.First(g=>g.Key==app).Processes} : null);
    public static bool UsesBudget(Policy policy,Rule? rule,DateTimeOffset now) => policy.DailyBudget is {} budget &&
        (budget.Scope=="all_known" || rule is not null && RuleFor(policy,rule.App) is not null) && (rule is null || IsAllowed(rule,now,policy.Timezone));
    public static bool HasExtraTime(Rule? rule,DateTimeOffset now) => rule is not null &&
        ((rule.OverrideMode=="allowed" && rule.OverrideUntil>now) || rule.AllowedUntil>now);
    public static bool IsAllowed(Rule rule,DateTimeOffset now,string timezone) {
        if(rule.OverrideUntil is {} overrideEnd && now<overrideEnd)return rule.OverrideMode=="allowed";
        if(rule.AllowedUntil is {} until && now<until) return true;
        if(rule.Mode=="allowed") return true;
        if(rule.Mode=="blocked") return false;
        if(rule.Windows is {Length:>0})return IsAllowed(rule with {Windows=null},now,timezone)||rule.Windows.Any(w=>IsAllowed(new Rule(rule.App,rule.Processes,"schedule",w.Start,w.End,w.Days),now,timezone));
        var local=TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(timezone));
        int day=((int)local.DayOfWeek+6)%7+1;
        var time=TimeOnly.FromDateTime(local.DateTime);
        var start=TimeOnly.ParseExact(rule.Start,"HH:mm");var end=TimeOnly.ParseExact(rule.End,"HH:mm");
        if(start<end) return rule.Days.Contains(day)&&time>=start&&time<end;
        var yesterday=day==1?7:day-1;
        return (rule.Days.Contains(day)&&time>=start)||(rule.Days.Contains(yesterday)&&time<end);
    }
}
