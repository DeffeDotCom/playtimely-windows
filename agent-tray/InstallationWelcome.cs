using GameControl;

// Per-user presentation preference only; never changes service or family rules.
internal sealed class InstallationWelcome : FlowLayoutPanel {
 readonly string path;
 readonly Label error;
 bool acknowledged;
 internal event Action? Dismissed;
 internal InstallationWelcome(string? preferencePath = null) {
  path=preferencePath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playtimely","installation-welcome-v1.txt");
  acknowledged=File.Exists(path);
  AutoSize=true;FlowDirection=FlowDirection.TopDown;WrapContents=false;Padding=new Padding(16);Margin=new Padding(0,0,0,16);
  BackColor=SystemInformation.HighContrast?SystemColors.Window:Color.FromArgb(233,241,222);
  Visible=false;
  Controls.Add(new Label{Text=TrayLanguage.Get("Installation complete!"),AutoSize=true,MaximumSize=new Size(420,0),Font=new Font("Segoe UI",17,FontStyle.Bold),Margin=new Padding(0,0,0,10)});
  Controls.Add(new Label{Text=TrayLanguage.Get("This computer is connected to your family.\n\nNext: a parent can add games and set limits at playtimely.com/dashboard or in the Playtimely parent app.\n\nYou can close this window. Playtimely keeps running in the background."),AutoSize=true,MaximumSize=new Size(420,0),Margin=new Padding(0,0,0,12)});
  var done=new StatusActionButton{Text=TrayLanguage.Get("Got it"),Height=44,Tag="full-width"};
  Controls.Add(done);
  error=new Label{AutoSize=true,MaximumSize=new Size(420,0),Visible=false};Controls.Add(error);
  done.Click+=(_,_)=>Acknowledge();
 }
 internal void UpdateConnection(bool connected){Visible=connected&&!acknowledged;}
 internal bool Acknowledge(){
  try{
   Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"acknowledged");
   acknowledged=true;Visible=false;Dismissed?.Invoke();return true;
  }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){
   error.Text=TrayLanguage.Get("Could not save. Please try again.");error.Visible=true;return false;
  }
 }
}
