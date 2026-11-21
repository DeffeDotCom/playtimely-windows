namespace GameControl;
// Only informational history is batched. Never use this for budget or stop-state persistence.
public sealed class HistoryCheckpoint {
 double next=double.NegativeInfinity;
 public void SaveWhenDue(double uptime,Action save){if(uptime<next)return;save();next=uptime+15;}
}
