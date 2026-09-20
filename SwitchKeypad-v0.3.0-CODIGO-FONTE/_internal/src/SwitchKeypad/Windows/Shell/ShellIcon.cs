using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SwitchKeypad.Windows.Shell;
public static class ShellIcon
{
    private static readonly Dictionary<string,ImageSource?> Cache=new(StringComparer.OrdinalIgnoreCase);
    public static ImageSource? Read(string? path)
    {
        lock(Cache)
        {
            try{return ReadCore(path);}catch{return null;}
        }
    }
    private static ImageSource? ReadCore(string? path)
    {
        if(!string.IsNullOrWhiteSpace(path)&&!Path.IsPathRooted(path)&&!File.Exists(path))
            path=(Environment.GetEnvironmentVariable("PATH")??"").Split(';').Prepend(Environment.SystemDirectory)
                .Select(folder=>Path.Combine(folder.Trim(),path)).FirstOrDefault(File.Exists);
        if(string.IsNullOrWhiteSpace(path)||(!File.Exists(path)&&!Directory.Exists(path)))return null;
        string key=path.ToLowerInvariant()+"|"+File.GetLastWriteTimeUtc(path).Ticks;
        if(Cache.TryGetValue(key,out var value))return value;
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
        string cachePath=Path.Combine(App.ConfigStore.RootPath,"Cache","Icons",hash+".png");
        try
        {
            if(File.Exists(cachePath))
            {
                var stored=new BitmapImage();stored.BeginInit();stored.CacheOption=BitmapCacheOption.OnLoad;stored.UriSource=new Uri(cachePath);stored.EndInit();stored.Freeze();
                Cache[key]=stored;return stored;
            }
        }catch{ /* A corrupt cache must not prevent extracting a fresh icon. */ }
        var info=new Info();
        if(SHGetFileInfo(path,0,ref info,(uint)Marshal.SizeOf<Info>(),0x100)==0||info.Icon==0){Cache[key]=null;return null;}
        try{
            var image=Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));
            image.Freeze();Cache[key]=image;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream=File.Create(cachePath);encoder.Save(stream);
            }catch(Exception error){ System.Diagnostics.Trace.WriteLine("Icon cache: "+error.Message); }
            return image;
        }finally{DestroyIcon(info.Icon);}
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Info{
        public nint Icon;public int Index;public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string TypeName;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern nint SHGetFileInfo(string path,uint attributes,ref Info info,uint size,uint flags);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(nint icon);
}
