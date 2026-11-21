using GameControl;
using Playtimely.Desktop;
// Operational explanation, not an optional reminder. Never takes keyboard focus.
internal sealed class GameNoticePopup:Form {
 readonly BrandButton button=new(){Height=44,Tag="full-width"};
 readonly Label text=new(){AutoSize=true,MaximumSize=new Size(350,0),Margin=new Padding(0,0,0,12)};
 readonly System.Windows.Forms.Timer timeout=new(){Interval=15000};
 internal event Action? SummaryRequested;
 protected override bool ShowWithoutActivation=>true;
 protected override CreateParams CreateParams {get {var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}} // WS_EX_NOACTIVATE, including TopMost positioning.
 protected override void WndProc(ref Message m){if(m.Msg==0x0021){m.Result=(IntPtr)3;return;}base.WndProc(ref m);} // MA_NOACTIVATE: clicks still open the summary explicitly.
 internal GameNoticePopup(){
  Text="Playtimely";ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.FixedToolWindow;StartPosition=FormStartPosition.Manual;
  Font=new Font("Segoe UI",10);BackColor=Color.White;ForeColor=Color.FromArgb(24,61,52);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(390,180);
  var layout=new ContentWindow(this,16);layout.Content.Controls.Add(text);
  button.Text=TrayLanguage.Get("Show today's summary");
  button.Click+=(_,_)=>{Hide();SummaryRequested?.Invoke();};text.Click+=(_,_)=>{Hide();SummaryRequested?.Invoke();};layout.Content.Controls.Add(button);
  timeout.Tick+=(_,_)=>{timeout.Stop();Hide();};FormClosing+=(_,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
 }
 internal void Present(string message){button.Text=TrayLanguage.Get("Show today's summary");text.Text=message;ClientSize=new Size(390,Math.Min(320,text.PreferredHeight+100));var area=Screen.PrimaryScreen!.WorkingArea;Location=new Point(area.Right-Width-16,area.Bottom-Height-16);CreateControl();ShowWindow(Handle,4);SetWindowPos(Handle,(IntPtr)(-1),Left,Top,Width,Height,0x0010|0x0040);timeout.Stop();timeout.Start();}
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle,int command);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle,IntPtr after,int x,int y,int width,int height,uint flags);
 public new void Hide(){if(IsHandleCreated)ShowWindow(Handle,0);base.Hide();}
 protected override void Dispose(bool disposing){if(disposing)timeout.Dispose();base.Dispose(disposing);}
}
