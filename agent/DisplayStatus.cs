namespace GameControl;
// Public projection only. No credentials, URLs, keys, executable paths or command lines.
// Closing intents contain only service-selected process identity, never executable commands.
public record DisplayRule(string App,string Mode,string Start,string End,int[] Days,DateTimeOffset? AllowedUntil=null,TimeWindow[]? Windows=null,string? OverrideMode=null,DateTimeOffset? OverrideUntil=null,bool BudgetBlocked=false) {
    public Rule AsRule()=>new(App,[],Mode,Start,End,Days,AllowedUntil,Windows,OverrideMode,OverrideUntil);
}
public record ClosingGame(string App,int ProcessId,long StartedAtUtcTicks);
public record DisplayRequest(string App,string Status,string Option);
public record RequestStatus(DisplayRequest[] Requests);
public record RunningGame(string App,bool UsesBudget,bool HasExtraTime=false);
public record DisplayStatus(int SchemaVersion,DateTimeOffset UpdatedAt,DateTimeOffset? LastSyncAt,string? Timezone,DisplayRule[] Rules,bool HasPolicy,DisplayRequest[]? Requests=null,ClosingGame[]? ClosingGames=null,int? BudgetRemainingSeconds=null,RunningGame[]? RunningGames=null,string? BudgetScope=null,string? BudgetAction=null,DateTimeOffset? BudgetExhaustedAt=null,DateTimeOffset? BudgetGraceEndsAt=null,int? BudgetUsedSeconds=null,int? BudgetLimitMinutes=null,PlayedGame[]? PlayedToday=null,GameNotice[]? Notices=null,ChoreList? Chores=null,string? BudgetReceiptScope=null,int? BudgetExtraMinutes=null,string? BudgetDay=null,string? SubscriptionState=null,GameStop? GameStop=null) {
    public static DisplayStatus Create(Policy? policy,DateTimeOffset now,DateTimeOffset? sync,DisplayRequest[]? requests=null)=>new(1,now,sync,policy?.Timezone,policy?.Rules.Select(r=>new DisplayRule(r.App,r.Mode,r.Start,r.End,r.Days,r.AllowedUntil,r.Windows,r.OverrideMode,r.OverrideUntil)).ToArray()??[],policy is not null,requests,Chores:policy?.Chores,BudgetExtraMinutes:policy is null?null:policy.DailyBudget?.ExtraDay==TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(policy.Timezone)).ToString("yyyy-MM-dd")?policy.DailyBudget.ExtraMinutes:0,BudgetDay:policy is null?null:TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(policy.Timezone)).ToString("yyyy-MM-dd"));
}
public static class ScheduleStatus {
    // Evaluate UTC minutes with the enforcement engine: handles overnight and DST
    // skipped/repeated local minutes without a separate, divergent schedule algorithm.
    public static DateTimeOffset? NextChange(Rule rule,DateTimeOffset now,string timezone) {
        bool allowed=PolicyEngine.IsAllowed(rule,now,timezone);
        var candidates=new[]{rule.OverrideUntil,rule.AllowedUntil}.Where(x=>x.HasValue&&x.Value>now).Select(x=>x!.Value).ToList();
        var next=DateTimeOffset.FromUnixTimeSeconds((now.ToUnixTimeSeconds()/60+1)*60);
        for(int i=0;i<8*24*60;i++,next=next.AddMinutes(1))
            if(PolicyEngine.IsAllowed(rule,next,timezone)!=allowed){candidates.Add(next);break;}
        foreach(var candidate in candidates.Order())if(PolicyEngine.IsAllowed(rule,candidate,timezone)!=allowed)return candidate;
        return null;
    }
}
