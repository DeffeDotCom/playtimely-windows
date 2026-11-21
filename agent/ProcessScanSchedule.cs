namespace GameControl;

// Expensive discovery is separate from the lightweight clock/enforcement heartbeat.
public sealed class ProcessScanSchedule {
 public const double IntervalSeconds=30;
 double next=double.NegativeInfinity;
 // Notification-only exhaustion never needs accelerated discovery. Enforcement modes do.
 public static bool NeedsFastDiscovery(string? action,int? remaining,bool exhausted) =>
  action is not null && action!="notify" && (exhausted || remaining is <=30);
 public bool IsDue(double uptime,bool policyChanged=false) {
  if(!policyChanged&&uptime<next)return false;
  next=uptime+IntervalSeconds;return true;
 }
}
