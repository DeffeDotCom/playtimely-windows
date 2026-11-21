using System.Text;
namespace GameControl;
public static class InstallerBundle {
 public static string? Read(string path) {
  using var stream=File.OpenRead(path);var magic=Encoding.ASCII.GetBytes("PLAYTIMELY-CONN1");
  if(stream.Length<magic.Length+4)return null;
  stream.Seek(-magic.Length,SeekOrigin.End);var tail=new byte[magic.Length];stream.ReadExactly(tail);
  if(!tail.SequenceEqual(magic))return null;
  stream.Seek(-magic.Length-4,SeekOrigin.End);Span<byte> size=stackalloc byte[4];stream.ReadExactly(size);
  int length=System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(size);
  if(length<2||length>16384||length>stream.Length-magic.Length-4)throw new InvalidDataException("Invalid personal installer. Download it again from your dashboard.");
  stream.Seek(-magic.Length-4-length,SeekOrigin.End);var json=new byte[length];stream.ReadExactly(json);return Encoding.UTF8.GetString(json);
 }
}
