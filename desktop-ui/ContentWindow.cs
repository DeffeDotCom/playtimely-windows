using System.Drawing;
using System.Windows.Forms;
namespace Playtimely.Desktop;

// One scrolling viewport, with an unconstrained vertical content stack inside it.
// FlowLayoutPanel.AutoScroll alone can clip TopDown/NoWrap content on Windows.
internal sealed class ContentWindow {
    readonly Form form;
    readonly Panel viewport;
    internal FlowLayoutPanel Content { get; }
    bool arranging;
    int lastPreferredHeight=-1;
    public ContentWindow(Form form, int padding=22) {
        this.form=form;
        viewport=new Panel {Dock=DockStyle.Fill,AutoScroll=true};
        Content=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(padding),Margin=Padding.Empty};
        viewport.Controls.Add(Content);form.Controls.Add(viewport);
        form.Shown+=(_,_)=>Fit();
        form.Resize+=(_,_)=>Arrange();
        form.DpiChanged+=(_,_)=>{lastPreferredHeight=-1;form.BeginInvoke(()=>Fit());};
    }
    void Widths(FlowLayoutPanel panel,int width) {
        panel.MinimumSize=new Size(width,0);panel.MaximumSize=new Size(width,0);panel.Width=width;
        foreach(Control child in panel.Controls) {
            int available=Math.Max(1,width-panel.Padding.Horizontal-child.Margin.Horizontal);
            if(child is FlowLayoutPanel stack) Widths(stack,available);
            else {
                if(child is Label) {child.MaximumSize=new Size(available,0);child.AutoSize=true;}
                else if(child is ComboBox || child.Tag as string=="full-width") child.Width=available;
                else if(child.Width>available)child.Width=available;
            }
        }
        panel.PerformLayout();
    }
    void Arrange() {
        if(arranging)return;arranging=true;
        try {Widths(Content,Math.Max(1,viewport.ClientSize.Width-SystemInformation.VerticalScrollBarWidth));}
        finally {arranging=false;}
    }
    internal void Fit(Rectangle? workArea=null) {
        if(arranging||form.IsDisposed||form.WindowState==FormWindowState.Minimized)return;
        var area=workArea??Screen.FromControl(form).WorkingArea;
        // Leave a little breathing room; never force a minimum larger than this monitor.
        int margin=(int)Math.Round(12*form.DeviceDpi/96d);
        int maxWidth=Math.Max(1,area.Width-margin*2),maxHeight=Math.Max(1,area.Height-margin*2);
        form.MinimumSize=Size.Empty;
        if(form.Width>maxWidth)form.Width=maxWidth;
        Arrange();
        int preferred=Content.PreferredSize.Height;
        if(preferred!=lastPreferredHeight || form.Height>maxHeight) {
            lastPreferredHeight=preferred;
            int chrome=form.Height-form.ClientSize.Height;
            form.Height=Math.Min(maxHeight,preferred+chrome);
            Arrange();
        }
        if(form.Visible)form.Location=new Point(Math.Clamp(form.Left,area.Left,Math.Max(area.Left,area.Right-form.Width)),Math.Clamp(form.Top,area.Top,Math.Max(area.Top,area.Bottom-form.Height)));
    }
}

// Native disabled buttons ignore ForeColor. Draw all states explicitly so a busy
// or completed green action never becomes black-on-green.
internal sealed class BrandButton:Button {
    protected override void OnPaint(PaintEventArgs e) {
        var background=SystemInformation.HighContrast?SystemColors.Control:Enabled?Color.FromArgb(23,59,51):Color.FromArgb(219,232,224);
        var foreground=SystemInformation.HighContrast?SystemColors.ControlText:Enabled?Color.White:Color.FromArgb(23,59,51);
        e.Graphics.Clear(background);
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,foreground,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),foreground,background);
    }
}
