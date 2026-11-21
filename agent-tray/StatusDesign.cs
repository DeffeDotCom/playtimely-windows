using System.Drawing.Drawing2D;

// Presentation only: no policy, request or service logic lives in these controls.
internal static class StatusDesign {
 internal static readonly Color Ink=Color.FromArgb(24,61,52);
 internal static GraphicsPath Round(Rectangle r,int radius){
  var path=new GraphicsPath();int d=radius*2;
  path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);
  path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;
 }
}
internal sealed class StatusBrand:Control {
 readonly Image logo;
 internal StatusBrand(Image logo){this.logo=logo;Height=76;Tag="full-width";Margin=new Padding(0,0,0,18);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleName="Playtimely v"+GameControl.DesktopRelease.CurrentVersion;}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);DrawHeader(e.Graphics,DeviceDpi/96f);
  using var pen=new Pen(SystemInformation.HighContrast?SystemColors.WindowText:Color.FromArgb(219,226,215));
  e.Graphics.DrawLine(pen,0,Height-1,Width,Height-1);
 }
 internal void DrawHeader(Graphics graphics,float scale){
  int size=(int)(40*scale),gap=(int)(12*scale);
  graphics.DrawImage(logo,new Rectangle(0,0,size,size));
  graphics.SmoothingMode=SmoothingMode.AntiAlias;
  DrawInk(graphics,"Playtimely",FontStyle.Bold,new RectangleF(size+gap,0,Math.Max(1,Width-size-gap),24*scale),SystemInformation.HighContrast?SystemColors.WindowText:StatusDesign.Ink);
  DrawInk(graphics,"v"+GameControl.DesktopRelease.CurrentVersion,FontStyle.Regular,new RectangleF(size+gap,28*scale,Math.Max(1,Width-size-gap),12*scale),SystemInformation.HighContrast?SystemColors.WindowText:Color.FromArgb(78,99,87));
 }
 // Align visible glyph bounds, not font line boxes (which include ascent/descent padding).
 static void DrawInk(Graphics graphics,string text,FontStyle style,RectangleF box,Color color){
  using var family=new FontFamily("Segoe UI");using var path=new GraphicsPath();
  path.AddString(text,family,(int)style,32,PointF.Empty,StringFormat.GenericTypographic);
  var bounds=path.GetBounds();float factor=Math.Min(box.Height/bounds.Height,box.Width/bounds.Width);
  using var matrix=new Matrix(factor,0,0,factor,box.X-bounds.X*factor,box.Y-bounds.Y*factor);path.Transform(matrix);
  using var brush=new SolidBrush(color);graphics.FillPath(brush,path);
 }
 protected override void Dispose(bool disposing){if(disposing)logo.Dispose();base.Dispose(disposing);}
}
internal sealed class GameStatusCard:FlowLayoutPanel {
 Color accent=StatusDesign.Ink;
 [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
 internal Color Accent{get=>accent;set{if(accent!=value){accent=value;Invalidate();}}}
 internal GameStatusCard(){DoubleBuffered=true;BackColor=Color.Transparent;Padding=new Padding(20);Margin=new Padding(0,14,0,0);AutoSize=true;FlowDirection=FlowDirection.TopDown;WrapContents=false;}
 protected override void OnPaintBackground(PaintEventArgs e){
  base.OnPaintBackground(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  using var path=StatusDesign.Round(new Rectangle(0,0,Math.Max(25,Width-1),Math.Max(25,Height-1)),12);
  using var fill=new SolidBrush(SystemInformation.HighContrast?SystemColors.Window:Color.White);e.Graphics.FillPath(fill,path);
  using var border=new Pen(SystemInformation.HighContrast?SystemColors.WindowText:Color.FromArgb(220,227,216));e.Graphics.DrawPath(border,path);
  using var marker=new Pen(SystemInformation.HighContrast?SystemColors.Highlight:accent,3){StartCap=LineCap.Round,EndCap=LineCap.Round};
  e.Graphics.DrawLine(marker,2,22,2,Math.Max(23,Height-22));
 }
}
internal sealed class StatusActionButton:Button {
 internal StatusActionButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(SystemInformation.HighContrast?SystemColors.Window:Color.White);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  var background=SystemInformation.HighContrast?SystemColors.Control:Enabled?StatusDesign.Ink:Color.FromArgb(237,241,234);
  var foreground=SystemInformation.HighContrast?SystemColors.ControlText:Enabled?Color.White:Color.FromArgb(78,99,87);
  using var path=StatusDesign.Round(new Rectangle(0,0,Math.Max(20,Width-1),Math.Max(20,Height-1)),8);using var brush=new SolidBrush(background);e.Graphics.FillPath(brush,path);
  TextRenderer.DrawText(e.Graphics,Text,Font,Rectangle.Inflate(ClientRectangle,-8,-3),foreground,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),foreground,background);
 }
}
