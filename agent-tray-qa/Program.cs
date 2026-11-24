using GameControl;
internal static class Program {
 static void TestChoreChecklist(){
  TrayLanguage.Select("en");using var window=new StatusWindow();window.Show();var now=DateTimeOffset.UtcNow;var id=Guid.NewGuid().ToString();int day=(int)now.DayOfWeek;if(day==0)day=7;int tomorrow=day==7?1:day+1;var second=Guid.NewGuid().ToString();
  var chores=new ChoreList(7,now.ToString("yyyy-MM-dd"),[],[new(id,day,"Finish homework"),new(second,tomorrow,"Tidy your things")]);
  var status=new DisplayStatus(1,now,now,"UTC",[],true,Chores:chores);window.Render(status,now);Application.DoEvents();
  var boxes=Children(window).OfType<CheckBox>().Where(c=>c.Text=="Finish homework").ToArray();if(boxes.Length!=1||!boxes[0].Enabled||boxes[0].Checked)throw new Exception("Today's incomplete checkbox missing");
  window.Render(status with {Chores=chores with {CompletedIds=[id]}},now);Application.DoEvents();if(!Children(window).OfType<CheckBox>().Single(c=>c.Text=="Finish homework").Checked)throw new Exception("Saved checkmark not rendered");
  var next=now.Date.AddDays(1);var future=new DateTimeOffset(next,TimeSpan.Zero);window.Render(status with {UpdatedAt=future},future);Application.DoEvents();var nextBox=Children(window).OfType<CheckBox>().Single(c=>c.Text=="Tidy your things");if(nextBox.Checked)throw new Exception("Yesterday's checkmark unlocked tomorrow");
  window.Render(status with {UpdatedAt=now.AddMinutes(-2)},now);Application.DoEvents();if(Children(window).OfType<CheckBox>().Single(c=>c.Text=="Finish homework").Enabled)throw new Exception("Unresponsive service allowed checkmark");
  window.FitToWorkArea(new Rectangle(0,0,640,600));Application.DoEvents();Capture(window,"en","chores");File.WriteAllText("qa-artifacts/windows-chores-ui.txt","PASS: today-only tasks, saved checkmark, midnight reset and unresponsive-service disabling");
 }
 static void TestDailyBudgetRequest(){
  TrayLanguage.Select("en");using var window=new StatusWindow();window.Show();var now=DateTimeOffset.UtcNow;
  var status=new DisplayStatus(1,now,now,"UTC",[],true,BudgetRemainingSeconds:0,BudgetScope:"all_known",BudgetLimitMinutes:300);
  window.Render(status,now);Application.DoEvents();
  var ask=Children(window).OfType<Button>().Single(b=>b.Text=="Ask for more time today");
  if(!ask.Visible||!ask.Enabled)throw new Exception("Ruleless exhausted budget needs request button");
  bool sawDialog=false;Exception? failure=null;
  using var timer=new System.Windows.Forms.Timer{Interval=200};timer.Tick+=(_,_)=>{
   var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Ask for more time");if(dialog is null)return;
   try {var combos=Children(dialog).OfType<ComboBox>().ToArray();if(combos.Length!=2||combos[0].Items.Count!=3||combos[1].Items.Count!=4)throw new Exception("Budget reasons/options missing");
    var message=Children(dialog).OfType<TextBox>().Single();if(!message.Multiline||message.MaxLength!=300)throw new Exception("Bounded optional message missing");
    message.Text="Homework is done";Capture(dialog,"en","budget-request");sawDialog=true;
   }catch(Exception ex){failure=ex;}finally {timer.Stop();dialog.DialogResult=DialogResult.Cancel;dialog.Close();}
  };timer.Start();ask.PerformClick();if(failure is not null)throw failure;if(!sawDialog)throw new Exception("Budget request dialog missing");
  window.Render(status with {Requests=[new("daily_budget","pending","30")]},now);if(ask.Enabled)throw new Exception("Duplicate pending request enabled");
  window.Render(status with {UpdatedAt=now.AddMinutes(-2)},now);if(ask.Enabled)throw new Exception("Offline request enabled");
  File.WriteAllText("qa-artifacts/windows-budget-request.txt","PASS: ruleless budget request, three durations, four reasons, bounded optional message, pending/offline disabling");
 }
 static void TestBudgetGrantNotification(){
  TrayLanguage.Select("en");var path=Path.Combine(Path.GetTempPath(),"grant-qa-"+Guid.NewGuid()+".json");
  try{
   using var window=new StatusWindow(grantReceiptPath:path);using var sentinel=new Form{Text="Extra-time focus sentinel"};
   using var icon=new NotifyIcon{Icon=SystemIcons.Information,Visible=true};
   sentinel.Show();sentinel.Activate();Application.DoEvents();var foreground=GetForegroundWindow();
   // Windows may deny fixture activation under its foreground lock. Preserve the
   // actual foreground owner (often the QA console), never assume Activate succeeded.
   if(foreground==IntPtr.Zero||foreground==window.Handle)throw new Exception("No independent foreground window for extra-time focus check");
   var now=DateTimeOffset.UtcNow;var status=new DisplayStatus(1,now,now,"UTC",[],true,BudgetRemainingSeconds:600,BudgetReceiptScope:"qa",BudgetExtraMinutes:0,BudgetDay:now.ToString("yyyy-MM-dd"));
   int count=0;window.BackgroundNotice+=message=>{count++;if(!message.Contains("10 extra minutes"))throw new Exception("Incorrect added time");TrayContext.ShowBackgroundNotice(icon,message);};
   window.Render(status,now);window.Render(status with{BudgetExtraMinutes=10},now);window.Render(status with{BudgetExtraMinutes=10},now);
   for(int i=0;i<10;i++){Application.DoEvents();Thread.Sleep(50);if(GetForegroundWindow()!=foreground)throw new Exception("Extra-time notification stole focus");}
   if(count!=1||window.Visible)throw new Exception("Extra time must notify once without opening the app");
   using var restarted=new StatusWindow(grantReceiptPath:path);int replay=0;restarted.BackgroundNotice+=_=>replay++;restarted.Render(status with{BudgetExtraMinutes=10},now);
   if(replay!=0||!Children(restarted).OfType<Label>().Any(l=>l.Text.Contains("10 extra minutes")))throw new Exception("Grant receipt missing or replayed after restart");
   restarted.Show();Application.DoEvents();Capture(restarted,"en","extra-time-confirmation");
   File.WriteAllText("qa-artifacts/windows-extra-time-notification.txt","PASS: 10-minute grant notifies once; no form opens or steals focus; confirmation remains after restart without replay.");
  }finally{File.Delete(path);}
 }
 static void TestGameNotices(){
  TestBudgetGrantNotification();
  TestChoreChecklist();
  TestDailyBudgetRequest();
  TrayLanguage.Select("sv");using var window=new StatusWindow();window.Show();var now=DateTimeOffset.UtcNow;
  var notice=new GameNotice("qa-stop","fortnite","budget","closed",now,18000,300);
  var status=new DisplayStatus(1,now,now,"Europe/Stockholm",[],true,BudgetRemainingSeconds:0,BudgetUsedSeconds:18000,BudgetLimitMinutes:300,PlayedToday:[new("fortnite",18000)],Notices:[notice]);
  int count=0;window.OperationalNotice+=_=>count++;window.Render(status,now);window.Render(status,now);Application.DoEvents();
  if(count!=1)throw new Exception("Operational notice duplicated");
  var icon=Children(window).OfType<PictureBox>().Single(p=>p.AccessibleName=="Fortnite");
  if(icon.Image is null || icon.Image.Width!=64)throw new Exception("Fortnite artwork missing from daily summary");
  window.FitToWorkArea(new Rectangle(0,0,400,800));Application.DoEvents();
  Capture(window,"sv","game-artwork");
  var labels=Children(window).OfType<Label>().Select(l=>l.Text).ToArray();
  if(!labels.Any(t=>t.Contains("5 h 00 min")&&t.Contains("300 min"))||!labels.Any(t=>t.Contains("Fortnite — 5 h 00 min")))throw new Exception("Budget summary missing");
  window.ScrollToSummary();Application.DoEvents();
  var summaryLabel=Children(window).OfType<Label>().Single(l=>l.Parent is GameStatusCard && l.Text.Contains("5 h 00 min")&&l.Text.Contains("300 min"));
  if(!window.RectangleToScreen(window.ClientRectangle).Contains(summaryLabel.PointToScreen(Point.Empty)))throw new Exception("Summary not visible after navigation");
  Capture(window,"sv","notice-summary");
  foreach(var action in new[]{"notify","finish","grace","stop"}){
   using var modeWindow=new StatusWindow();string? message=null;modeWindow.OperationalNotice+=text=>message=text;
   modeWindow.Render(status with {Notices=[],BudgetAction=action,BudgetExhaustedAt=now,BudgetGraceEndsAt=action=="grace"?now.AddMinutes(10):null},now);
   if(message is null||!message.Contains("300 min"))throw new Exception("Budget action lacks measured explanation: "+action);
   if(action=="grace"&&message.Contains("De 10 extra minuterna är slut."))throw new Exception("Grace must not be declared over before deadline");
   if(action=="stop"&&message.Contains("Stänger"))throw new Exception("Budget exhaustion alone must not claim a game was closed");
  }

  using var popup=new GameNoticePopup();int clicked=0;popup.SummaryRequested+=()=>{clicked++;window.Show();window.ScrollToSummary();};popup.Present(GameNoticeText.Describe(notice));Application.DoEvents();
  Capture(popup,"sv","notice-popup");Children(popup).OfType<Button>().Single().PerformClick();if(clicked!=1||popup.Visible)throw new Exception("Notice click did not open summary");
  using(var reminderWindow=new StatusWindow()){
   string? reminder=null;int reminders=0;reminderWindow.OperationalNotice+=m=>{reminder=m;reminders++;};
   var reminderStatus=status with {Notices=[],BudgetExhaustedAt=now,BudgetAction="finish",RunningGames=[new("fortnite",true),new("minecraft",true,true)]};
   reminderWindow.Render(reminderStatus,now,0);reminders=0;
   reminderWindow.Render(reminderStatus with {UpdatedAt=now.AddSeconds(899)},now.AddSeconds(899),899);
   if(reminders!=0)throw new Exception("Quarter-hour reminder early");
   reminderWindow.Render(reminderStatus with {UpdatedAt=now.AddSeconds(900)},now.AddSeconds(900),900);
   if(reminders!=1||reminder is null||!reminder.Contains("Avsluta Fortnite")||reminder.Contains("Minecraft"))throw new Exception("Quarter-hour reminder incorrect");
  }
  window.Render(status with {UpdatedAt=now.AddMinutes(-1)},now);if(count!=1)throw new Exception("Stale notice repeated");
  TrayLanguage.Select("en");File.WriteAllText("qa-artifacts/windows-notice-summary.txt","PASS: operational notice dedup, Swedish measured hours/limit, played-game summary, popup click event and stale suppression.");
 }
 static void TestBudgetRefresh(){
  TrayLanguage.Select("en");using var window=new StatusWindow();window.Show();
  var now=DateTimeOffset.UtcNow;
  var status=new DisplayStatus(1,now,now,"Europe/Stockholm",[],true,BudgetRemainingSeconds:5400);
  window.Render(status,now);Application.DoEvents();
  var label=Children(window).OfType<Label>().Single(l=>l.Text.Contains("90 minutes left today"));
  int changes=0,resizes=0;label.TextChanged+=(_,_)=>changes++;label.SizeChanged+=(_,_)=>resizes++;
  for(int i=1;i<=10;i++){window.Render(status with {UpdatedAt=now.AddSeconds(i),BudgetRemainingSeconds=5400-i},now.AddSeconds(i));Application.DoEvents();}
  if(changes!=0||resizes!=0)throw new Exception($"Unchanged budget flickered: {changes} text changes, {resizes} resizes");
  window.Render(status with {BudgetRemainingSeconds=5340},now);Application.DoEvents();
  if(changes!=1||!label.Text.Contains("89 minutes left today"))throw new Exception("Minute transition must update once");
  window.Render(status with {BudgetRemainingSeconds=null},now);Application.DoEvents();
  if(changes!=2||label.Text.Contains("minutes left today"))throw new Exception("Disabled budget text remained");
  var observed=status with {RunningGames=[new RunningGame("fortnite",true),new RunningGame("minecraft",false)]};
  window.Render(observed,now);Application.DoEvents();
  var running=Children(window).OfType<Label>().Single(l=>l.Text.StartsWith("Running now:"));
  if(!running.Text.Contains("Fortnite — Using daily budget")||!running.Text.Contains("Minecraft — Not using daily budget"))throw new Exception("Running game budget labels missing");
  int runningChanges=0;running.TextChanged+=(_,_)=>runningChanges++;
  window.Render(observed,now.AddSeconds(2));Application.DoEvents();
  if(runningChanges!=0)throw new Exception("Unchanged running games repainted");
  Capture(window,"en","budget-running");
  window.Render(observed with {RunningGames=[]},now);Application.DoEvents();
  if(running.Text!="No controlled games running.")throw new Exception("Exited games remained visible");
  window.Render(observed,now.AddSeconds(30));Application.DoEvents();
  if(running.Text.Length!=0)throw new Exception("Stale status claims games are running now");
  File.WriteAllText("qa-artifacts/windows-budget-refresh.txt","PASS: 10 budget refreshes caused no text/size changes; minute change and budget removal each updated once.");
 }
 static void TestBrandAlignment(){
  using var icon=new Icon("agent-tray/playtimely.ico");
  using var brand=new StatusBrand(icon.ToBitmap()){Width=800};
  foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
   using var bitmap=new Bitmap(800,140);using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.White);brand.DrawHeader(graphics,scale);
   int leftTop=140,leftBottom=-1,rightTop=140,rightBottom=-1;
   for(int y=0;y<140;y++)for(int x=0;x<800;x++){
    var pixel=bitmap.GetPixel(x,y);if(pixel.R>245&&pixel.G>245&&pixel.B>245)continue;
    if(x<(int)(40*scale)){leftTop=Math.Min(leftTop,y);leftBottom=Math.Max(leftBottom,y);}
    else if(x>=(int)(52*scale)){rightTop=Math.Min(rightTop,y);rightBottom=Math.Max(rightBottom,y);}
   }
   if(Math.Abs(leftTop-rightTop)>2||Math.Abs(leftBottom-rightBottom)>2)throw new Exception($"Brand ink misaligned at {scale}: logo {leftTop}-{leftBottom}, text {rightTop}-{rightBottom}");
   bitmap.Save($"qa-artifacts/windows-brand-{(int)(scale*100)}.png");
  }
  File.WriteAllText("qa-artifacts/windows-brand-alignment.txt","PASS: logo and visible title/version ink aligned at 100/125/150/200 percent scale.");
 }
 static void TestNotificationConsent(){
  var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playtimely","notifications.json");
  var previous=File.Exists(path)?File.ReadAllBytes(path):null;Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  try{
   File.Delete(path);TrayLanguage.Select("en");
   using(var form=new Form())using(var panel=new NotificationConsent()){
    form.Controls.Add(panel);form.Show();panel.UpdateConnection(true);Application.DoEvents();
    var later=panel.Controls.OfType<Button>().Single(b=>b.Text=="Not now");var checkbox=panel.Controls.OfType<CheckBox>().Single();
    if(!later.Visible||checkbox.Checked)throw new Exception("First notification consent missing");
    later.PerformClick();if(NotificationConsent.Enabled||later.Visible)throw new Exception("Decline was not saved");
    checkbox.Checked=true;if(!NotificationConsent.Enabled)throw new Exception("Notification enable was not saved");
    checkbox.Checked=false;if(NotificationConsent.Enabled)throw new Exception("Notification disable was not saved");
   }
   using(var form=new Form())using(var panel=new NotificationConsent()){
    form.Controls.Add(panel);form.Show();panel.UpdateConnection(true);Application.DoEvents();
    var later=panel.Controls.OfType<Button>().Single(b=>b.Text=="Not now");
    if(later.Visible)throw new Exception("Notification consent repeated after reopen");
    File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new NotificationConsent.Choice(false,DateTimeOffset.UtcNow.AddDays(-44))));panel.UpdateConnection(true);
    if(later.Visible)throw new Exception("Notification consent repeated before 45 days");
    File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new NotificationConsent.Choice(false,DateTimeOffset.UtcNow.AddDays(-46))));panel.UpdateConnection(true);
    if(!later.Visible)throw new Exception("Notification consent missing after 45 days");
   }
   File.WriteAllText("qa-artifacts/windows-notification-consent.txt","PASS: first offer, decline and enable/disable persist, recreated window stays quiet, 44-day no prompt and 46-day reminder.");
  }finally{if(previous is null)File.Delete(path);else File.WriteAllBytes(path,previous);}
 }
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processId);
 [STAThread] static int Main(string[] args){
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory("qa-artifacts");TestBrandAlignment();
  if(args.Length>=2&&args[0]=="--close-fixture"){
   using var game=new Form{Text="Harmless game close fixture"};
   game.Shown+=(_,_)=>{
    // A newly opened fixture legitimately owns foreground focus. Cooperatively
    // return activation permission to its parent before testing WM_CLOSE;
    // Form.Activate alone can be denied by Windows' foreground lock.
    if(uint.TryParse(Environment.GetEnvironmentVariable("PLAYTIMELY_QA_FOCUS_OWNER"),out var owner)&&!AllowSetForegroundWindow(owner))throw new Exception("Fixture could not grant parent foreground permission");
    File.WriteAllText(args[1]+".ready","shown");
   };
   game.FormClosing+=(_,e)=>{File.WriteAllText(args[1],"WM_CLOSE received");if(args.Length>2)e.Cancel=true;};
   Application.Run(game);return 0;
  }
  if(args.Contains("--notices-only")){
   TestGameNotices();
   using var sentinel=new Form{Text="Focus sentinel"};sentinel.Show();sentinel.Activate();Application.DoEvents();var before=GetForegroundWindow();
   using var popup=new GameNoticePopup();popup.Present("Game-time explanation focus probe");Application.DoEvents();
   if(GetForegroundWindow()!=before)throw new Exception($"Notice focus changed: expected {before}, actual {GetForegroundWindow()}, popup {popup.Handle}, visible {popup.Visible}");
   Console.WriteLine("PASS: operational notice content, click and foreground preservation");return 0;
  }
  TestNotificationConsent();TestBudgetRefresh();TestGameNotices();
  var welcomeRoot=Path.Combine(Path.GetTempPath(),"Playtimely-welcome-qa-"+Guid.NewGuid());
  Directory.CreateDirectory(welcomeRoot);
  foreach(var language in TrayLanguage.Supported){
   TrayLanguage.Select(language);var pref=Path.Combine(welcomeRoot,language+".txt");
   using(var window=new StatusWindow(pref)){
    window.Show();Application.DoEvents();var notice=Children(window).OfType<InstallationWelcome>().Single();
    if(notice.Visible)throw new Exception("Welcome shown before connection");
    var now=DateTimeOffset.UtcNow;
    window.Render(new DisplayStatus(1,now,now,"Europe/Stockholm",[],true),now);Application.DoEvents();
    if(!notice.Visible)throw new Exception("Welcome missing after connection");
    window.FitToWorkArea(new Rectangle(0,0,640,600));Application.DoEvents();Check(window,language+" welcome");Capture(window,language,"welcome");
    var done=Children(notice).OfType<Button>().Single();
    if(done.Height<30 || done.Width<40)throw new Exception("Welcome action collapsed");
    done.PerformClick();if(notice.Visible||!File.Exists(pref))throw new Exception("Welcome acknowledgement not saved");
   }
   using(var reopened=new StatusWindow(pref)){
    reopened.Show();var now=DateTimeOffset.UtcNow;reopened.Render(new DisplayStatus(1,now,now,"Europe/Stockholm",[],true),now);Application.DoEvents();
    if(Children(reopened).OfType<InstallationWelcome>().Single().Visible)throw new Exception("Welcome returned after restart");
   }
  }
  Directory.Delete(welcomeRoot,true);
  File.WriteAllText("qa-artifacts/windows-welcome.txt","PASS: all supported languages; hidden before connection, shown after fresh policy; small-screen layout; dismiss persisted across recreated windows.");
  TestGracefulClose();
  using(var foreground=new Form {Text="Game focus sentinel"})using(var icon=new NotifyIcon {Icon=SystemIcons.Information,Visible=true}){
   foreground.Show();foreground.Activate();Application.DoEvents();var before=GetForegroundWindow();
   if(before!=foreground.Handle)throw new Exception("Focus sentinel not foreground; cannot verify warning focus");
   using var operational=new GameNoticePopup();operational.Present("QA operational explanation");
   Application.DoEvents();if(GetForegroundWindow()!=before)throw new Exception("Operational explanation stole focus");
   operational.Hide();
   TrayContext.ShowTimeWarning(icon,"roblox");
   for(int i=0;i<10;i++){Application.DoEvents();Thread.Sleep(50);if(GetForegroundWindow()!=before)throw new Exception("Warning stole focus");}
   File.WriteAllText("qa-artifacts/windows-warning-focus.txt","PASS: native notification preserved foreground window. Fullscreen game visibility depends on Windows notification settings.");
  }
  foreach(var language in TrayLanguage.Supported){
   TrayLanguage.Select(language);using var window=new StatusWindow();window.Show();window.RefreshStatus();Application.DoEvents();
   Check(window,language+" status");Capture(window,language,"status");
   var ask=Children(window).OfType<Button>().FirstOrDefault(b=>b.Text==TrayLanguage.Get("Ask for more time…"))??throw new Exception("Missing request button");
   bool checkedDialog=false;
   using var timer=new System.Windows.Forms.Timer{Interval=300};timer.Tick+=(_,_)=>{
    var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=window);
    if(dialog is null)return;
    timer.Stop();Check(dialog,language+" request");Capture(dialog,language,"request");
    var send=Children(dialog).OfType<Button>().Single(b=>b.DialogResult==DialogResult.OK);
    if(send.Text!=TrayLanguage.Get("Send request"))throw new Exception("Untranslated send button");
    if(send.Bottom>send.Parent!.ClientSize.Height-send.Parent.Padding.Bottom)throw new Exception(language+" send button clipped");
    checkedDialog=true;dialog.Close();
   };
   timer.Start();ask.PerformClick();if(!checkedDialog)throw new Exception("Dialog not tested");window.Hide();
  }
  File.WriteAllText("qa-artifacts/windows-languages.txt","PASS: 20 status windows and request dialogs rendered; translated send buttons, horizontal containment, visible send actions; screenshots for en/sv/de/ja/zh. No requests submitted.");
  TrayLanguage.Select("en");
  if (int.TryParse(Environment.GetEnvironmentVariable("PLAYTIMELY_QA_SHELL_PID"), out var shellPid)) {
   foreach(var p in System.Diagnostics.Process.GetProcessesByName("Playtimely.Tray")) { p.Kill();p.WaitForExit();p.Dispose(); }
   var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"GameControl","Playtimely.Tray.exe");
   using(var shellFixture=System.Diagnostics.Process.GetProcessById(shellPid)){if(shellFixture.HasExited)throw new Exception("Standard-user shell fixture expired before launch check");}
   if(!InteractiveLaunch.StartForShell(exe,shellPid))throw new Exception("Unelevated tray launch failed");
   System.Diagnostics.Process? launched=null;
   for(int i=0;i<100;i++){Thread.Sleep(100);launched=System.Diagnostics.Process.GetProcessesByName("Playtimely.Tray").FirstOrDefault();if(launched is not null){launched.Refresh();if(launched.MainWindowHandle!=IntPtr.Zero)break;}}
   if(launched is null||launched.HasExited||launched.MainWindowHandle==IntPtr.Zero)throw new Exception("Auto-launched game-times window not visible");
   if(launched.SessionId!=System.Diagnostics.Process.GetCurrentProcess().SessionId)throw new Exception("Wrong launch session");
   File.WriteAllText("qa-artifacts/windows-direct-launch.txt","PASS: installed tray launched using standard-user desktop token in current session, visible game-times window without manual launch. PID="+launched.Id);
   launched.Kill();launched.WaitForExit();launched.Dispose();
  }

  using(var window=new StatusWindow()){
   var start=DateTimeOffset.Parse("2026-10-30T12:00:10Z");var until=start.AddSeconds(20);
   var snapshot=new DisplayStatus(1,start,start,"Europe/Stockholm",[new DisplayRule("roblox","schedule","18:00","21:00",[1,2,3,4,5,6,7],until)],true);
   window.Render(snapshot,start);window.Render(snapshot,until.AddSeconds(1));
   var expected="Opens "+TimeZoneInfo.ConvertTime(DateTimeOffset.Parse("2026-10-30T17:00:00Z"),TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm")).ToString("ddd d MMM HH:mm",System.Globalization.CultureInfo.GetCultureInfo("en"));
   if(!Children(window).OfType<Label>().Any(l=>l.Text.Contains(expected)))throw new Exception("Expired grant still shows past opening time");
  }
  // Reproduce one game's approval adding lines, then a constrained laptop screen.
  TrayLanguage.Select("sv");
  using(var window=new StatusWindow()) {
   var now=DateTimeOffset.UtcNow;
   var initial=new DisplayStatus(1,now,now,"Europe/Stockholm",[new DisplayRule("starcraft","schedule","18:00","21:00",[1,2,3,4,5,6,7],null)],true);
   window.Show();window.Render(initial,now);Application.DoEvents();
   int before=window.Height;
   window.Render(initial with {Rules=[initial.Rules[0] with {AllowedUntil=now.AddMinutes(15)}],Requests=[new DisplayRequest("starcraft","approved","15")]},now);Application.DoEvents();
   Check(window,"approved extra time");
   if(window.Height<before)throw new Exception("Extra-time content shrank the window");
   Capture(window,"sv","approved-autosize");
   var area=new Rectangle(window.Left,window.Top,480,520);
   window.FitToWorkArea(area);Application.DoEvents();
   if(window.Height>area.Height||window.Width>area.Width)throw new Exception("Window exceeds available screen");
   var viewport=window.Controls.OfType<Panel>().Single();
   if(!viewport.VerticalScroll.Visible)throw new Exception("Small screen must offer vertical scrolling");
   var languages=Children(window).OfType<ComboBox>().Single();
   viewport.ScrollControlIntoView(languages);Application.DoEvents();
   var bottom=viewport.PointToClient(languages.PointToScreen(new Point(0,languages.Height)));
   if(bottom.Y>viewport.ClientSize.Height)throw new Exception("Language selector cannot be reached by scrolling");
   Check(window,"small-screen status");Capture(window,"sv","small-screen-scrolled");
  }
  TrayLanguage.Select("en");
  File.WriteAllText("qa-artifacts/windows-expiry.txt","PASS: next opening recomputed immediately after sub-minute extra-time expiry.");
  using(var setup=new Setup(true)){
   setup.Show();Application.DoEvents();Check(setup,"installer upgrade");
   if(!Children(setup).OfType<Button>().Any(b=>b.Text=="Update Playtimely"))throw new Exception("Existing computer did not offer connection-free update");
   Capture(setup,"en","installer-upgrade");
   var action=Children(setup).OfType<Button>().Single(b=>b.Text=="Update Playtimely");
   action.Text="Installed";action.Enabled=false;Application.DoEvents();
   Capture(setup,"en","installer-completed");
   using(var bitmap=new Bitmap(action.Width,action.Height)) {
    action.DrawToBitmap(bitmap,new Rectangle(Point.Empty,action.Size));
    if(bitmap.GetPixel(5,5).GetBrightness()<0.5f)throw new Exception("Disabled installer button still has a dark background");
   }
   setup.Close();
  }
  var connectionBackup=File.ReadAllBytes(Setup.ConnectionPath);
  try{
   File.Delete(Setup.ConnectionPath);
   using var missing=new Setup(true);missing.Show();Application.DoEvents();Check(missing,"missing connection recovery");
   var retry=Children(missing).OfType<Button>().Single(b=>b.Text=="Update Playtimely");retry.PerformClick();Application.DoEvents();
   if(!Children(missing).Any(c=>c.Text.Contains("Setup cannot read")))throw new Exception("Missing connection must remain in safe update retry mode");
   if(File.Exists(Setup.ConnectionPath))throw new Exception("Missing connection was replaced unexpectedly");
   if(Children(missing).Any(c=>c.Text.Contains("connection.json")||c.Text.Contains("Choose connection file")))throw new Exception("Technical connection picker exposed");
   Capture(missing,"en","installer-recovery");missing.Close();
  }finally{File.WriteAllBytes(Setup.ConnectionPath,connectionBackup);}
  if(Setup.TryOpenDashboard(()=>throw new System.ComponentModel.Win32Exception(unchecked((int)0x80040154),"Class not registered")))throw new Exception("Browser launch failure not handled");
  if(!Setup.TryOpenDashboard(()=>{}))throw new Exception("Successful browser launch rejected");
  File.WriteAllText("qa-artifacts/windows-installer-ui.txt","PASS: existing installation offers Update without connection picker; layout rendered without horizontal clipping.");
  return 0;
 }
 static void TestGracefulClose(){
  using var sentinel=new Form{Text="Foreground must stay here"};sentinel.Show();
  foreach(bool ignore in new[]{false,true}){
   string marker=Path.Combine(Path.GetTempPath(),"pt-close-"+Guid.NewGuid()+".txt");
   var start=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
   start.Environment["PLAYTIMELY_QA_FOCUS_OWNER"]=Environment.ProcessId.ToString();
   if(Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(Program).Assembly.Location);
   start.ArgumentList.Add("--close-fixture");start.ArgumentList.Add(marker);if(ignore)start.ArgumentList.Add("ignore");
   using var child=System.Diagnostics.Process.Start(start)!;
   try{
    var readyDeadline=DateTime.UtcNow.AddSeconds(15);
    while(!File.Exists(marker+".ready")&&!child.HasExited&&DateTime.UtcNow<readyDeadline){Application.DoEvents();Thread.Sleep(50);}
    if(!File.Exists(marker+".ready"))throw new Exception("Fixture window did not start");
    sentinel.Activate();Application.DoEvents();var foreground=GetForegroundWindow();
    if(foreground!=sentinel.Handle)throw new Exception("Close focus sentinel not foreground");
    long identity=child.StartTime.ToUniversalTime().Ticks;
    if(GracefulGameCloser.RequestClose(child.Id,identity+1))throw new Exception("Accepted reused process identity");
    if(!GracefulGameCloser.RequestClose(child.Id,identity))throw new Exception("WM_CLOSE was not delivered");
    var deadline=DateTime.UtcNow.AddSeconds(5);
    while(!File.Exists(marker)&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(50);}
    if(!File.Exists(marker))throw new Exception("Game did not receive normal close message");
    if(GetForegroundWindow()!=foreground)throw new Exception("Close request stole foreground focus");
    if(!ignore&&!child.WaitForExit(5000))throw new Exception("Cooperative game did not exit normally");
    if(ignore&&child.HasExited)throw new Exception("Graceful helper forced an unresponsive game to exit");
   }finally{if(!child.HasExited)child.Kill();File.Delete(marker);File.Delete(marker+".ready");}
  }
  File.WriteAllText("qa-artifacts/windows-graceful-close.txt","PASS: normal WM_CLOSE exits cooperative game; refusing game is not killed by tray; mismatched process identity rejected; foreground focus preserved. Service force deadline is tested separately.");
 }
 static IEnumerable<Control> Children(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in Children(child))yield return nested;}}
 static void Check(Form form,string label){foreach(var control in Children(form)){if(control.Visible&&control is Button or PictureBox&&control.Height<20)throw new Exception(label+": collapsed "+control.GetType().Name+" "+control.Text);if(control.Visible&&control.Parent is {} parent&&control.Right>parent.ClientSize.Width-parent.Padding.Right+4)throw new Exception(label+": horizontal clipping "+control.GetType().Name+" "+control.Text);}}
 static void Capture(Form form,string language,string name){if(!new[]{"en","sv","de","ja","zh"}.Contains(language))return;using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save("qa-artifacts/windows-"+language+"-"+name+".png");}
}
