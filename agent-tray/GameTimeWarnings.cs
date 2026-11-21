namespace GameControl;

// A warning is informational only: it never opens/activates a window or changes enforcement.
public sealed class GameTimeWarnings {
    string? finishKey;
    double? nextFinishReminder;
    // Monotonic time: changing the wall clock cannot spam reminders. No catch-up bursts.
    public string[] FinishReminders(DateTimeOffset? began,string? action,int? remaining,IEnumerable<RunningGame> running,bool fresh,double uptime){
        if(!fresh)return [];
        var games=running.Where(g=>g.UsesBudget&&!g.HasExtraTime).Select(g=>g.App).Distinct().Order().ToArray();
        if(action!="finish"||remaining!=0||began is null||games.Length==0){finishKey=null;nextFinishReminder=null;return [];}
        var key=began.Value.ToString("O");
        if(key!=finishKey||nextFinishReminder is null){finishKey=key;nextFinishReminder=uptime+900;return [];}
        if(uptime<nextFinishReminder)return [];
        nextFinishReminder=uptime+900;return games;
    }
    private string? budgetWarningDay;
    private string? budgetEndKey;
    public bool ShouldNotifyEnd(DateTimeOffset? began,string? action,bool fresh) {
        if(!fresh||began is null)return false;
        var key=$"{began:O}:{action}";if(key==budgetEndKey)return false;budgetEndKey=key;return true;
    }
    public bool ShouldNotifyBudget(int? seconds,DateTimeOffset now,bool fresh) {
        var day=now.ToString("yyyy-MM-dd");
        if(!fresh||seconds is null||seconds<=0||seconds>300||budgetWarningDay==day)return false;
        budgetWarningDay=day;return true;
    }
    readonly Dictionary<string,DateTimeOffset> shown=new();
    public bool ShouldNotify(string app,DateTimeOffset? closing,DateTimeOffset now,bool fresh,bool allowed){
        foreach(var key in shown.Where(x=>x.Value<=now).Select(x=>x.Key).ToArray())shown.Remove(key);
        if(!fresh||!allowed||closing is not {} end||end<=now||end-now>TimeSpan.FromMinutes(5))return false;
        if(shown.TryGetValue(app,out var previous)&&previous==end)return false;
        shown[app]=end;return true;
    }
}
