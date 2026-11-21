using GameControl;
internal static class GameNoticeText {
 internal static string Duration(int seconds)=>seconds>=3600?$"{seconds/3600} h {seconds/60%60:00} min":$"{Math.Max(0,seconds)/60} min";
 internal static string Describe(GameNotice n){
  string outcome=n.Outcome switch{"closed"=>"Game closed","blocked"=>"New game session blocked","finished"=>"Game session finished",_=>"Game time is over. Closing the game."};
  string reason=n.Reason switch{"chores"=>"Complete today’s to-do list before gaming.","budget_grace"=>"The extra 10 minutes are over.","budget"=>"Today's game budget is used up.","paused"=>"Paused by your parents.","repair"=>"Status unavailable. Ask a parent to check Playtimely.",_=>"Outside allowed hours."};
  return GameCatalog.Name(n.App)+" · "+TrayLanguage.Get(outcome)+"\n"+TrayLanguage.Get(reason)+(n.Reason is "budget" or "budget_grace"?"\n"+Budget(n.UsedSeconds,n.LimitMinutes):"");
 }
 internal static string Budget(int? used,int? limit)=>
  (used is {} seconds?TrayLanguage.Get("Played today: ")+Duration(seconds):TrayLanguage.Get("Waiting for your family’s rules…"))+
  (limit is {} minutes?" · "+TrayLanguage.Get("Daily limit: ")+Duration(minutes*60)+$" ({minutes} min)":"");
}
