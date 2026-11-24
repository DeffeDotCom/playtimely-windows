using System.Collections.Concurrent;
using System.Net.Http;
using GameControl;

// Artwork is decorative: loading never delays status rendering or enforcement.
internal static class GameArtwork {
 static readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8),MaxResponseContentBufferSize=262144};
 static readonly ConcurrentDictionary<string,Task<byte[]?>> downloads=new();
 internal static TableLayoutPanel Row(string app,string text,Font font){
  var row=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=1,Width=410,Tag="full-width",BackColor=Color.White,Margin=new Padding(0,8,0,8)};
  row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,50));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  var picture=new PictureBox{Width=40,Height=40,SizeMode=PictureBoxSizeMode.Zoom,Margin=new Padding(0,0,10,0),AccessibleName=GameCatalog.Name(app)};
  var label=new Label{Text=text,Font=font,AutoSize=true,MaximumSize=new Size(350,0),BackColor=Color.White,Margin=new Padding(0,8,0,0)};
  row.Controls.Add(picture,0,0);row.Controls.Add(label,1,0);
  row.Layout+=(_,_)=>{var width=Math.Max(1,row.ClientSize.Width-50);if(label.MaximumSize.Width!=width)label.MaximumSize=new Size(width,0);};
  picture.Disposed+=(_,_)=>{picture.Image?.Dispose();};
  using var embedded=typeof(GameArtwork).Assembly.GetManifestResourceStream("Playtimely.GameArtwork."+app+".png");
  if(embedded is not null){using var original=Image.FromStream(embedded);picture.Image=new Bitmap(original);}
  else {picture.Image=SystemIcons.Application.ToBitmap();picture.HandleCreated+=async(_,_)=>await Load(picture,app);}
  return row;
 }
 static async Task Load(PictureBox picture,string app){
  if(!GameCatalog.Contains(app)||downloads.Count>=1000&&!downloads.ContainsKey(app))return;
  var bytes=await downloads.GetOrAdd(app,Fetch);
  if(bytes is null||picture.IsDisposed)return;
  try {using var stream=new MemoryStream(bytes);using var original=Image.FromStream(stream);var image=new Bitmap(original);var old=picture.Image;picture.Image=image;old?.Dispose();}
  catch(ArgumentException){}catch(OutOfMemoryException){}
 }
 static async Task<byte[]?> Fetch(string app){
  try {using var response=await client.GetAsync("https://playtimely.com/game-icons/"+Uri.EscapeDataString(app)+".png");if(!response.IsSuccessStatusCode||response.Content.Headers.ContentType?.MediaType!="image/png")return null;var bytes=await response.Content.ReadAsByteArrayAsync();return bytes.Length<=262144?bytes:null;}
  catch(HttpRequestException){return null;}catch(TaskCanceledException){return null;}
 }
}
