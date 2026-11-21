using System.Text.Json;
using System.Diagnostics;
using GameControl;
// Local user preference; never changes the service or enforcement.
internal sealed class NotificationConsent : FlowLayoutPanel {
 internal record Choice(bool Enabled,DateTimeOffset DeclinedAt);
 static string PreferencePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playtimely","notifications.json");
 static Choice Read(){try{return JsonSerializer.Deserialize<Choice>(File.ReadAllText(PreferencePath))??new(false,DateTimeOffset.MinValue);}catch{return new(false,DateTimeOffset.MinValue);}}
 internal static bool Enabled=>Read().Enabled;
 readonly Label explanation;readonly Button later;readonly CheckBox enabled;bool painting;
 internal event Action? Changed;
 internal NotificationConsent(){
  AutoSize=true;FlowDirection=FlowDirection.TopDown;WrapContents=false;Padding=new Padding(12);Margin=new Padding(0,12,0,12);BackColor=Color.FromArgb(233,241,222);Visible=false;
  explanation=new Label{Text=TrayLanguage.Get("Get game-time warnings? If you say no, we will ask again after 45 days."),AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,0,0,10)};
  enabled=new CheckBox{Text=TrayLanguage.Get("Enable notifications"),AutoSize=true,MinimumSize=new Size(0,40)};
  later=new StatusActionButton{Text=TrayLanguage.Get("Not now"),Height=44,Tag="full-width"};
  var settings=new StatusActionButton{Text=TrayLanguage.Get("Windows notification settings"),Height=44,Tag="full-width"};
  settings.Click+=(_,_)=>{try{Process.Start(new ProcessStartInfo("ms-settings:notifications"){UseShellExecute=true});}catch{}};
  enabled.CheckedChanged+=(_,_)=>{if(!painting)Save(enabled.Checked);};later.Click+=(_,_)=>Save(false);
  Controls.Add(explanation);Controls.Add(enabled);Controls.Add(later);Controls.Add(settings);
 }
 void Save(bool allow){try{Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);File.WriteAllText(PreferencePath,JsonSerializer.Serialize(new Choice(allow,DateTimeOffset.UtcNow)));UpdateConnection(true);Changed?.Invoke();}catch(IOException){MessageBox.Show(TrayLanguage.Get("Could not save. Please try again."));}catch(UnauthorizedAccessException){MessageBox.Show(TrayLanguage.Get("Could not save. Please try again."));}}
 internal void UpdateConnection(bool connected){
  Visible=connected;if(!connected)return;var choice=Read();painting=true;enabled.Checked=choice.Enabled;painting=false;
  var due=!choice.Enabled&&DateTimeOffset.UtcNow-choice.DeclinedAt>=TimeSpan.FromDays(45);explanation.Visible=due;later.Visible=due;
 }
}
