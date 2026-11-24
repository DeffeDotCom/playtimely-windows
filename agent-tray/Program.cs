using Playtimely.Desktop;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using GameControl;
internal static class Program {
 [STAThread] static void Main(string[] args){
  using var mutex=new Mutex(true,@"Local\PlaytimelyTray",out bool first);if(!first)return;
  ApplicationConfiguration.Initialize();Application.Run(new TrayContext(args.Contains("--show")));
 }
}
sealed class TrayContext:ApplicationContext {
 readonly GameNoticePopup popup=new();
 readonly NotifyIcon tray;readonly DesktopUpdater updater;StatusWindow window;readonly System.Windows.Forms.Timer timer=new(){Interval=1000};
 public TrayContext(bool show = false){
  LanguagePreference.Load();window=CreateWindow();
  var menu=new ContextMenuStrip();menu.Items.Add(L("Show game times"),null,(_,_)=>Show());
  menu.Items.Add(L("Hide icon (rules stay active)"),null,(_,_)=>ExitThread());
  using var iconStream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayIcon")!;
  using var loadedIcon=new Icon(iconStream);
  tray=new NotifyIcon{Icon=(Icon)loadedIcon.Clone(),Text="Playtimely v"+DesktopRelease.CurrentVersion,Visible=true,ContextMenuStrip=menu};
  updater=new DesktopUpdater(menu,tray);
  popup.SummaryRequested+=Show;tray.BalloonTipClicked+=(_,_)=>Show();
  tray.MouseClick+=(_,e)=>{if(e.Button==MouseButtons.Left)Show();};

  timer.Tick+=(_,_)=>window.RefreshStatus();timer.Start();window.RefreshStatus();
  if(show) Show();
 }
 StatusWindow CreateWindow(){var form=new StatusWindow();form.BackgroundNotice+=message=>{if(NotificationConsent.Enabled)ShowBackgroundNotice(tray,message);};form.OperationalNotice+=message=>{popup.Present(message);if(NotificationConsent.Enabled)tray.ShowBalloonTip(10000,"Playtimely",message,ToolTipIcon.Info);};form.TimeWarning+=(app)=>{if(NotificationConsent.Enabled)ShowTimeWarning(tray,app);};form.FormClosing+=(_,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;form.Hide();}};form.LanguageChanged+=()=>{
  var old=window;window=CreateWindow();old.Dispose();
  tray.ContextMenuStrip!.Items[0].Text=L("Show game times");tray.ContextMenuStrip.Items[2].Text=L("Hide icon (rules stay active)");Show();
 };return form;}
 internal static void ShowBackgroundNotice(NotifyIcon icon,string message)=>icon.ShowBalloonTip(10000,"Playtimely",message,ToolTipIcon.Info);
 internal static void ShowTimeWarning(NotifyIcon icon,string app)=>icon.ShowBalloonTip(10000,"Playtimely · "+GameCatalog.Name(app),L("5 minutes or less left. Save your game before time runs out."),ToolTipIcon.Info);
 void Show(){window.RefreshStatus();window.Show();window.ScrollToSummary();window.WindowState=FormWindowState.Normal;window.Activate();}
 protected override void ExitThreadCore(){timer.Stop();timer.Dispose();updater.Dispose();tray.Visible=false;tray.Icon?.Dispose();tray.Dispose();window.Dispose();popup.Dispose();base.ExitThreadCore();}
 internal static string L(string en)=>TrayLanguage.Get(en);
}
sealed class StatusWindow:Form {
 public event Action? LanguageChanged;
 public event Action<string>? TimeWarning;
 public event Action<string>? GameClosing;

 public event Action<string>? BackgroundNotice;
 readonly BudgetGrantNotifications grants;
 readonly PlaytimelyTray.SubscriptionNoticeReceipt subscriptionReceipt=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playtimely","subscription-receipt.json"));
 readonly Label grantConfirmation=new(){AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,8,0,12),Visible=false};
 public event Action<string>? OperationalNotice;
 readonly HashSet<string> seenNotices=[];
 readonly FlowLayoutPanel choresPanel=new(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,MaximumSize=new Size(420,0),Margin=new Padding(0,12,0,12)};
 string? choresFingerprint;bool choresSending;
 readonly Label explanation=new(){AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,8,0,12)};
 readonly Label summary=new(){AutoSize=true,MaximumSize=new Size(410,0),BackColor=Color.White};
 readonly FlowLayoutPanel played=new(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.White};
 string? playedFingerprint;
 readonly GracefulGameCloser closer=new();
 string? lastManualStop;
 readonly GameTimeWarnings warnings=new();
 readonly ContentWindow layout;
 readonly InstallationWelcome welcome;
 readonly NotificationConsent notificationConsent=new();
 readonly Label service=new(){AutoSize=true,MaximumSize=new Size(420,0)};
 readonly Label controlHelp;
 readonly Button budgetAsk=new BrandButton{Text=L("Ask for more time today"),Height=44,Tag="full-width",Visible=false};
 readonly Label budgetRequestStatus=new(){AutoSize=true,MaximumSize=new Size(410,0)};
 readonly Label budgetStatus=new(){AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,12,0,8),Font=new Font("Segoe UI",11,FontStyle.Bold)};
 readonly Label running=new(){AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,8,0,0)};
 readonly Label sync=new(){AutoSize=true,MaximumSize=new Size(420,0),ForeColor=Color.DimGray};
 readonly FlowLayoutPanel games=new(){AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
 readonly Dictionary<string,(Label State,Label Detail,Label Request,Button Ask)> rows=new();
 DisplayStatus? snapshot;string? fingerprint;long minute=-1;
 readonly Dictionary<string,DateTimeOffset?> transitions=new();
 static string L(string en)=>TrayLanguage.Get(en);
 public StatusWindow(string? welcomePath = null,string? grantReceiptPath = null){
  grants=new BudgetGrantNotifications(grantReceiptPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playtimely","budget-grant-receipt.json"));
  closer.Closing+=(app)=>GameClosing?.Invoke(app);
  Text="Playtimely · "+L("My game time");ClientSize=new Size(560,620);StartPosition=FormStartPosition.CenterScreen;
  BackColor=Color.FromArgb(250,249,245);ForeColor=Color.FromArgb(24,61,52);Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
  layout=new ContentWindow(this);
  var content=layout.Content;
  welcome=new InstallationWelcome(welcomePath);welcome.Dismissed+=()=>layout.Fit();content.Controls.Add(welcome);
  notificationConsent.Changed+=()=>layout.Fit();content.Controls.Add(notificationConsent);
  content.Controls.Add(new Label{Text=L("Your game time"),AutoSize=true,MaximumSize=new Size(420,0),Font=new Font(Font.FontFamily,20,FontStyle.Bold),Margin=new Padding(0,0,0,8)});
  content.Controls.Add(service);content.Controls.Add(sync);content.Controls.Add(explanation);content.Controls.Add(choresPanel);
  var summaryCard=new GameStatusCard{Width=440};summaryCard.Controls.Add(new Label{Text=L("Today’s game summary"),AutoSize=true,Font=new Font(Font.FontFamily,14,FontStyle.Bold),BackColor=Color.White,Margin=new Padding(0,0,0,12)});summaryCard.Controls.Add(summary);summaryCard.Controls.Add(played);content.Controls.Add(summaryCard);
  content.Controls.Add(budgetStatus);content.Controls.Add(grantConfirmation);budgetAsk.Click+=async(_,_)=>await Ask("daily_budget");content.Controls.Add(budgetAsk);content.Controls.Add(budgetRequestStatus);content.Controls.Add(running);content.Controls.Add(games);
  controlHelp=new Label{Text=L("Other games are allowed. Only your parents can change these rules.\nSave your game before time runs out.\nDaily budgets count while a controlled game is running, including in the background."),AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,16,0,0),ForeColor=Color.DimGray};content.Controls.Add(controlHelp);
  using var logoStream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayIcon")!;
  using var logo=new Icon(logoStream);Icon=(Icon)logo.Clone();
  var brand=new StatusBrand(logo.ToBitmap());
  content.Controls.Add(brand);content.Controls.SetChildIndex(brand,0);
  var languages=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=300,AccessibleName=L("Language")};
  languages.Items.Add("Windows");foreach(var code in TrayLanguage.Supported)languages.Items.Add(CultureInfo.GetCultureInfo(code).NativeName);
  languages.SelectedIndex=LanguagePreference.Override is {} choice?Array.IndexOf(TrayLanguage.Supported,choice)+1:0;
  languages.SelectedIndexChanged+=(_,_)=>{LanguagePreference.Save(languages.SelectedIndex==0?null:TrayLanguage.Supported[languages.SelectedIndex-1]);BeginInvoke(()=>LanguageChanged?.Invoke());};
  content.Controls.Add(new Label{Text=L("Language"),AutoSize=true,Margin=new Padding(0,18,0,4)});content.Controls.Add(languages);

 }
 internal void FitToWorkArea(Rectangle area)=>layout.Fit(area);
 internal void ScrollToSummary(){if(layout.Content.Parent is ScrollableControl viewport)viewport.ScrollControlIntoView(snapshot?.Chores?.Blocks(DateTimeOffset.UtcNow,snapshot.Timezone??"UTC")==true?choresPanel:summary.Parent!);}
 public void RefreshStatus(){
  try {
   var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PlaytimelyStatus","status.json");
   if(new FileInfo(path).Length>131072)throw new InvalidDataException();
   using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
   snapshot=JsonSerializer.Deserialize<DisplayStatus>(file,PolicyEngine.Json);
   if(snapshot is null||snapshot.SchemaVersion!=1||snapshot.Rules.Length>30)throw new InvalidDataException();
   closer.Apply(snapshot,DateTimeOffset.UtcNow);
   Render(snapshot);
  } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException or FormatException){
   welcome.UpdateConnection(false);
   service.Text=L("Status unavailable. Ask a parent to check Playtimely.");service.ForeColor=Color.DarkOrange;sync.Text="";budgetStatus.Text="";budgetAsk.Visible=false;budgetRequestStatus.Text="";summary.Text=L("Status unavailable. Ask a parent to check Playtimely.");played.Controls.Clear();playedFingerprint=null;running.Text="";games.Controls.Clear();rows.Clear();fingerprint=null;layout.Fit();
  }
 }
 internal void Render(DisplayStatus s,DateTimeOffset? testTime=null,double? testUptime=null){
  var now=testTime??DateTimeOffset.UtcNow;bool fresh=now-s.UpdatedAt<TimeSpan.FromSeconds(20)&&s.UpdatedAt<=now.AddSeconds(5);
  if(fresh&&s.GameStop is {} manual&&manual.Active(now)&&lastManualStop!=manual.Id){lastManualStop=manual.Id;var message=TrayLanguage.Current.StartsWith("sv")?manual.Action switch {"notify"=>"En förälder ber dig att avsluta spelandet.","finish"=>"Avsluta det pågående spelet. Nya spelstarter är stoppade.","grace"=>"Du har tio minuter att avsluta och spara. Därefter stängs spelen.",_=>"En förälder har stoppat alla spel."}:manual.Action switch {"notify"=>"A parent asks you to stop playing.","finish"=>"Finish your current game. New game starts are paused.","grace"=>"You have ten minutes to finish and save. Games will then close.",_=>"A parent has stopped all games."};OperationalNotice?.Invoke(message);}
  var addedMinutes=grants.Observe(s,fresh);
  bool expired=s.SubscriptionState=="expired";
  if(s.HasPolicy&&subscriptionReceipt.Observe(s.SubscriptionState,s.BudgetReceiptScope,fresh))BackgroundNotice?.Invoke(L("Your free trial has ended. Game limits are paused. A parent can choose a plan at playtimely.com to restore the saved rules."));
  grantConfirmation.Visible=fresh&&s.HasPolicy&&grants.LatestMinutes>0;
  grantConfirmation.Text=grants.LatestMinutes>0?string.Format(L("{0} extra minutes added for today. Schedules and blocks still apply."),grants.LatestMinutes):"";
  if(addedMinutes>0)BackgroundNotice?.Invoke(string.Format(L("{0} extra minutes added for today. Schedules and blocks still apply."),addedMinutes));
  RenderChores(s,now,fresh);
  var recent=(s.Notices??[]).Where(n=>n.At<=now.AddSeconds(5)&&now-n.At<TimeSpan.FromMinutes(5)).TakeLast(20).ToArray();
  var latest=recent.LastOrDefault();explanation.Text=latest is null?"":GameNoticeText.Describe(latest);
  if(fresh){foreach(var n in recent){if(seenNotices.Add(n.Id))OperationalNotice?.Invoke(GameNoticeText.Describe(n));}seenNotices.IntersectWith(recent.Select(n=>n.Id));}
  summary.Text=expired?L("Trial ended — saved game limits are paused until a subscription is active."):(s.BudgetLimitMinutes is null?L("No daily limit."):GameNoticeText.Budget(s.BudgetUsedSeconds,s.BudgetLimitMinutes))+(fresh?"":"\n"+L("Agent is not responding. These are the last known rules."));
  var today=(s.PlayedToday??[]).Where(g=>GameCatalog.Contains(g.App)&&g.Seconds>0).Take(100).ToArray();
  var playedKey=JsonSerializer.Serialize(today.Select(g=>new {g.App,Minutes=g.Seconds/60}));
  if(playedKey!=playedFingerprint){foreach(Control child in played.Controls.Cast<Control>().ToArray())child.Dispose();played.Controls.Clear();foreach(var g in today)played.Controls.Add(GameArtwork.Row(g.App,GameCatalog.Name(g.App)+" — "+GameNoticeText.Duration(g.Seconds),Font));played.Controls.Add(new Label{Text=L("Includes background games. Simultaneous games may overlap."),AutoSize=true,MaximumSize=new Size(390,0),Margin=new Padding(0,12,0,4),BackColor=Color.White,ForeColor=Color.DimGray});playedFingerprint=playedKey;}
  welcome.UpdateConnection(fresh && s.HasPolicy);notificationConsent.UpdateConnection(fresh && s.HasPolicy);
  service.Text=!fresh?L("Agent is not responding. These are the last known rules."):!s.HasPolicy?L("Waiting for your family’s rules…"):L("Your family’s rules are active.");
  service.ForeColor=fresh?ForeColor:Color.DarkOrange;
  var syncText=s.LastSyncAt is {} last?L("Last sync: ")+last.ToLocalTime().ToString("g",CultureInfo.GetCultureInfo(TrayLanguage.Current))+(now-last>TimeSpan.FromMinutes(3)?L(" · Using saved rules"):""):L("Using saved rules / waiting for connection");
  if(s.BudgetRemainingSeconds is {} remaining)syncText+="\n"+(remaining==0?L("Today's game budget is used up."):Math.Ceiling(remaining/60d)+" "+L("minutes left today"));
  if(s.BudgetRemainingSeconds is not null)syncText+="\n"+L(s.BudgetScope=="all_known"?"All known games":"Selected games only");
  // Assign the complete label once: removing/readding its second line each tick
  // triggers AutoSize layout and visible flicker even when nothing changed.
  if(sync.Text!=syncText)sync.Text=syncText;
  controlHelp.Visible=!expired&&s.BudgetScope!="all_known";
  var runningText=s.RunningGames is null||!fresh||!s.HasPolicy ? "" : s.RunningGames.Length==0 ? L("No controlled games running.") :
   L("Running now:")+"\n"+string.Join("\n",s.RunningGames.Select(g=>GameCatalog.Name(g.App)+" — "+L(g.UsesBudget?"Using daily budget":"Not using daily budget")));
  if(running.Text!=runningText)running.Text=runningText;
  if(!s.HasPolicy){games.Controls.Clear();rows.Clear();fingerprint=null;layout.Fit();return;}
  if(s.Timezone is null)throw new InvalidDataException();
  var budgetText=!fresh||s.BudgetRemainingSeconds is null?"":s.BudgetRemainingSeconds>0?L("Time left: ")+Remaining(TimeSpan.FromSeconds(s.BudgetRemainingSeconds.Value)):
   L(s.BudgetAction switch {"finish"=>"Today's time is used up. You can finish this session, but cannot start another game.","grace"=>"Today's time is used up. You have 10 extra minutes to save and finish. Games will then close.","notify"=>"Today's time is used up. Your family has chosen reminder only.",_=>"Today's game budget is used up."});
  if(s.BudgetRemainingSeconds==0 && s.BudgetGraceEndsAt is {} deadline)budgetText=deadline>now?L("Time left: ")+Remaining(deadline-now)+" · "+L("Today's time is used up. You have 10 extra minutes to save and finish. Games will then close."):L("The extra 10 minutes are over.");
  budgetAsk.Visible=s.HasPolicy&&s.BudgetRemainingSeconds is not null;
  var dailyRequest=s.Requests?.FirstOrDefault(r=>r.App=="daily_budget");
  budgetAsk.Enabled=fresh&&dailyRequest?.Status!="pending";
  budgetRequestStatus.Text=dailyRequest?.Status switch {"pending"=>L("Request sent · waiting for a parent"),"approved"=>L("Extra time approved for today"),"denied"=>L("Not now — your parent declined"),_=>""};
  if(budgetStatus.Text!=budgetText)budgetStatus.Text=budgetText;
  if(warnings.ShouldNotifyEnd(s.BudgetExhaustedAt,s.BudgetAction,fresh)){var message=budgetText+"\n"+GameNoticeText.Budget(s.BudgetUsedSeconds,s.BudgetLimitMinutes);explanation.Text=message;OperationalNotice?.Invoke(message);}
  var reminders=warnings.FinishReminders(s.BudgetExhaustedAt,s.BudgetAction,s.BudgetRemainingSeconds,s.RunningGames??[],fresh&&s.HasPolicy,testUptime??Environment.TickCount64/1000d);
  if(reminders.Length>0){var message=string.Format(L("Your game time is over. Please finish {0}."),string.Join(", ",reminders.Select(GameCatalog.Name)))+"\n"+GameNoticeText.Budget(s.BudgetUsedSeconds,s.BudgetLimitMinutes);explanation.Text=message;OperationalNotice?.Invoke(message);}
  if(warnings.ShouldNotifyBudget(s.BudgetRemainingSeconds,TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(s.Timezone)),fresh)) {
   var game=s.Rules.FirstOrDefault(r=>r.Mode!="blocked");if(game is not null)TimeWarning?.Invoke(game.App);
  }
  string key=JsonSerializer.Serialize(new{s.Timezone,s.Rules,s.BudgetScope});
  if(key!=fingerprint){
   foreach(Control control in games.Controls.Cast<Control>().ToArray())control.Dispose();games.Controls.Clear();rows.Clear();transitions.Clear();minute=-1;fingerprint=key;
   foreach(var rule in s.Rules){
    var panel=new GameStatusCard{Width=440};
    var title=GameArtwork.Row(rule.App,GameCatalog.Name(rule.App),new Font(Font.FontFamily,13,FontStyle.Bold));
    var state=new Label{AutoSize=true,MaximumSize=new Size(410,0),Font=new Font(Font.FontFamily,13,FontStyle.Bold),Padding=new Padding(9,6,9,6),Margin=new Padding(0,0,0,10)};
    var detail=new Label{AutoSize=true,MaximumSize=new Size(410,0),ForeColor=Color.FromArgb(73,88,82),Margin=new Padding(0,0,0,4)};
    panel.Controls.Add(title);panel.Controls.Add(state);panel.Controls.Add(detail);games.Controls.Add(panel);var ask=new StatusActionButton{Text=L("Ask for more time…"),Height=42,Tag="full-width",FlatStyle=FlatStyle.Flat,Margin=new Padding(0,12,0,0)};
    ask.Click+=async (_,_)=>await Ask(rule.App);
    var requestLabel=new Label{AutoSize=true,MaximumSize=new Size(410,0),ForeColor=Color.FromArgb(73,88,82),Margin=new Padding(0,8,0,0)};
    panel.Controls.Add(ask);panel.Controls.Add(requestLabel);rows[rule.App]=(state,detail,requestLabel,ask);
   }
   if(s.Rules.Length==0 && s.BudgetScope!="all_known")games.Controls.Add(new Label{Text=L("No games are restricted on this computer."),AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,20,0,0)});
  }
  if(minute!=now.ToUnixTimeSeconds()/60||transitions.Values.Any(t=>t is {} end&&end<=now)){minute=now.ToUnixTimeSeconds()/60;foreach(var rule in s.Rules)transitions[rule.App]=ScheduleStatus.NextChange(rule.AsRule(),now,s.Timezone);}
  foreach(var rule in s.Rules){
   if(warnings.ShouldNotify(rule.App,transitions.GetValueOrDefault(rule.App),now,fresh,PolicyEngine.IsAllowed(rule.AsRule(),now,s.Timezone)))TimeWarning?.Invoke(rule.App);
   var row=rows[rule.App];bool allowed=PolicyEngine.IsAllowed(rule.AsRule(),now,s.Timezone);var change=transitions[rule.App];
   ((GameStatusCard)row.State.Parent!).Accent=allowed?Color.FromArgb(102,145,75):Color.FromArgb(183,100,71);
   row.Detail.BackColor=Color.White;row.Request.BackColor=Color.White;
   row.State.BackColor=allowed?Color.FromArgb(220,237,210):Color.FromArgb(249,223,209);
   row.State.ForeColor=allowed?Color.FromArgb(49,108,57):Color.FromArgb(156,65,45);
   row.State.Text=!allowed?L("Locked"):s.BudgetRemainingSeconds is >0 && !(rule.AllowedUntil>now) && !(rule.OverrideMode=="allowed"&&rule.OverrideUntil>now)?L("Time left: ")+Remaining(TimeSpan.FromSeconds(Math.Min(s.BudgetRemainingSeconds.Value,change.HasValue?Math.Max(0,(change.Value-now).TotalSeconds):s.BudgetRemainingSeconds.Value))):change is {} end?L("Time left: ")+Remaining(end-now):L("Allowed — no time limit");
   string detail=rule.Mode=="blocked"?L("Paused by your parents."):rule.Mode=="allowed"?L("Your parents have allowed this game."):L("Schedule: ")+string.Join(", ",rule.Days.Select(d=>TrayLanguage.Day(d)))+" "+rule.Start+"–"+rule.End+"\n"+s.Timezone;
   if(change is {} next){var local=TimeZoneInfo.ConvertTime(next,TimeZoneInfo.FindSystemTimeZoneById(s.Timezone));detail+="\n"+(allowed?L("Closes "):L("Opens "))+local.ToString("ddd d MMM HH:mm",CultureInfo.GetCultureInfo(TrayLanguage.Current));}
   if(rule.AllowedUntil is {} extra&&extra>now)detail+="\n"+L("Extra time approved until ")+TimeZoneInfo.ConvertTime(extra,TimeZoneInfo.FindSystemTimeZoneById(s.Timezone)).ToString("HH:mm");
   if(rule.Windows is {Length:>0})foreach(var w in rule.Windows)detail+="\n"+string.Join(", ",w.Days.Select(TrayLanguage.Day))+" "+w.Start+"–"+w.End;
   if(rule.BudgetBlocked)detail=L("Today's game budget is used up.");
   row.Detail.Text=detail;
   var request=s.Requests?.FirstOrDefault(r=>r.App==rule.App);
   row.Ask.Enabled=fresh&&rule.Mode!="allowed"&&request?.Status!="pending";
   row.Request.Text=request is null?"":request.Status switch {
    "pending"=>L("Request sent · waiting for a parent"),
    "approved"=>L("Your parent said yes!"),
    "denied"=>L("Not this time. Ask your parent if you have questions."),
    "revoked"=>L("Extra time ended. Your normal rules apply."),
    "expired"=>L("The request or extra time has expired."),_=>""};
   row.Request.Visible=row.Request.Text.Length>0;
  }
  // Hidden windows still process warnings/close requests, but need no layout pass.
  if(Visible)layout.Fit();
 }
 void RenderChores(DisplayStatus s,DateTimeOffset now,bool fresh){
  if(choresSending)return;
  var c=s.Chores;var today=c?.Today(now,s.Timezone??"UTC")??[];
  var key=JsonSerializer.Serialize(new{c?.Revision,c?.CompletedDay,Today=now.ToString("yyyy-MM-dd"),Tasks=today,Done=c?.CompletedIds,Fresh=fresh});
  if(key==choresFingerprint)return;choresFingerprint=key;
  foreach(Control control in choresPanel.Controls.Cast<Control>().ToArray())control.Dispose();choresPanel.Controls.Clear();choresPanel.Visible=c is not null;if(c is null)return;
  choresPanel.Controls.Add(new Label{Text=L("To do"),AutoSize=true,Font=new Font(Font.FontFamily,15,FontStyle.Bold)});
  choresPanel.Controls.Add(new Label{Text=L("Check off today’s tasks before gaming. Normal gaming limits still apply."),AutoSize=true,MaximumSize=new Size(410,0)});
  if(today.Length==0)choresPanel.Controls.Add(new Label{Text=L("No tasks today."),AutoSize=true});
  foreach(var task in today){var box=new CheckBox{Text=task.Label,Checked=c.Done(task.Id,now,s.Timezone!),Enabled=fresh,AutoSize=true,MaximumSize=new Size(410,0),Margin=new Padding(0,10,0,6)};box.CheckedChanged+=async(_,_)=>await CheckChore(task.Id,box.Checked,c,s.Timezone!);choresPanel.Controls.Add(box);}
  choresPanel.Controls.Add(new Label{Text=L("Connect to the internet to save checkmarks. They reset at local midnight."),AutoSize=true,MaximumSize=new Size(410,0),ForeColor=Color.DimGray});layout.Fit();
 }
 async Task CheckChore(string id,bool done,ChoreList list,string timezone){
  if(choresSending)return;choresSending=true;foreach(Control c in choresPanel.Controls)c.Enabled=false;
  try{using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(14));using var pipe=new NamedPipeClientStream(".","Playtimely.TimeRequests.v1",PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(deadline.Token);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);
   var day=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(timezone)).ToString("yyyy-MM-dd");
   await writer.WriteLineAsync(JsonSerializer.Serialize(new {app="chores",option=done?"done":"undo",reason="",task_id=id,day,revision=list.Revision},PolicyEngine.Json));var result=await reader.ReadLineAsync(deadline.Token);
   if(result!="saved")MessageBox.Show(this,L("Could not save. Connect to the internet and refresh today’s list."),"Playtimely",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }catch(Exception ex)when(ex is IOException or OperationCanceledException or UnauthorizedAccessException){MessageBox.Show(this,L("Could not save. Connect to the internet and refresh today’s list."),"Playtimely");}
  finally{choresSending=false;choresFingerprint=null;RefreshStatus();}
 }
 async Task Ask(string app){
  using var dialog=new Form{Text=L("Ask for more time"),ClientSize=new Size(460,390),StartPosition=FormStartPosition.CenterParent,Font=Font,BackColor=BackColor,ForeColor=ForeColor,AutoScaleMode=AutoScaleMode.Dpi,FormBorderStyle=FormBorderStyle.Sizable,MaximizeBox=false,MinimizeBox=false};
  var requestLayout=new ContentWindow(dialog,18);var panel=requestLayout.Content;
  panel.Controls.Add(new Label{Text=L("What would you like?"),AutoSize=true});
  var option=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=410};option.Items.AddRange(new object[]{L("15 more minutes"),L("30 more minutes"),L("60 more minutes"),L("Unlock for today")});if(app=="daily_budget")option.Items.RemoveAt(3);option.SelectedIndex=0;panel.Controls.Add(option);
  panel.Controls.Add(new Label{Text=L("Why?"),AutoSize=true,Margin=new Padding(0,14,0,0)});
  var reason=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=410};reason.Items.AddRange(new object[]{L("Finish this round"),L("Playing with friends"),L("Homework is done"),L("A little more time")});reason.SelectedIndex=0;panel.Controls.Add(reason);
  panel.Controls.Add(new Label{Text=L("Message to your parent (optional)"),AutoSize=true,Margin=new Padding(0,14,0,0)});
  var message=new TextBox{Multiline=true,MaxLength=300,Height=70,Width=410};panel.Controls.Add(message);
  panel.Controls.Add(new Label{Text=L("A parent must approve. Your current rules apply until then."),AutoSize=true,MaximumSize=new Size(410,0),Margin=new Padding(0,14,0,14)});
  var send=new BrandButton{Text=L("Send request"),Height=44,Tag="full-width",DialogResult=DialogResult.OK};panel.Controls.Add(send);dialog.AcceptButton=send;
  if(dialog.ShowDialog(this)!=DialogResult.OK)return;
  string result;
  try {
   using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
   using var pipe=new NamedPipeClientStream(".","Playtimely.TimeRequests.v1",PipeDirection.InOut,PipeOptions.Asynchronous);
   await pipe.ConnectAsync(3000,deadline.Token);
   var payload=JsonSerializer.Serialize(new {app,option=new[]{"15","30","60","today"}[option.SelectedIndex],reason=new[]{"finish_round","friends","homework_done","other"}[reason.SelectedIndex],message=message.Text.Trim()});
   await pipe.WriteAsync(Encoding.UTF8.GetBytes(payload+"\n"),deadline.Token);
   var buffer=new byte[32];int count=0;while(count<buffer.Length){int n=await pipe.ReadAsync(buffer.AsMemory(count,1),deadline.Token);if(n==0)break;count+=n;if(buffer[count-1]==10)break;}
   result=Encoding.UTF8.GetString(buffer,0,count).Trim();
  }catch(Exception ex)when(ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException){result="offline";}
  if(IsDisposed)return;
  MessageBox.Show(this,result switch {
   "sent"=>L("Sent! Your parent can approve in Playtimely. The answer reaches this computer in about a minute."),
   "wait"=>L("Please wait before asking again."),
   "rejected"=>L("This request could not be sent. Refresh your rules and try again."),
   _=>L("Could not send. Check your internet connection and that Playtimely is running. Check request status before trying again.")},"Playtimely",MessageBoxButtons.OK,MessageBoxIcon.Information);
 }
 static string Remaining(TimeSpan remaining){int seconds=Math.Max(0,(int)Math.Ceiling(remaining.TotalSeconds));return $"{seconds/3600:00}:{seconds/60%60:00}:{seconds%60:00}";}
}
