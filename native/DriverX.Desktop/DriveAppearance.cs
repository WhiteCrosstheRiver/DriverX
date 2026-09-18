using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace DriverX.Desktop;

public sealed record DriveIconOption(int Id, string Name)
{
    public ImageSource? Preview => DriveAppearance.Preview(Id < 0 ? 9 : Id);
}

internal static class DriveAppearance
{
    public static readonly DriveIconOption[] Options = [
        new(-1,"默认 · Windows 网络磁盘"), new(8,"硬盘"), new(9,"网络磁盘"),
        new(7,"移动磁盘"), new(15,"服务器"), new(51,"共享服务器"),
        new(3,"文件夹"), new(47,"安全锁"), new(77,"安全盾牌"),
        new(13,"全球网络"), new(94,"工作站"), new(71,"音乐"),
        new(72,"图片"), new(73,"视频")];

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct StockIcon { public uint Size; public IntPtr Handle; public int SystemIndex; public int ResourceIndex; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Path; }
    [DllImport("shell32.dll")] static extern int SHGetStockIconInfo(int id,uint flags,ref StockIcon info);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern void SHChangeNotify(uint id,uint flags,string path,IntPtr other);

    public static ImageSource? Preview(int id)
    {
        var info=new StockIcon {Size=(uint)Marshal.SizeOf<StockIcon>(),Path=""};
        if(SHGetStockIconInfo(id,0x100,ref info)!=0)return null;
        try { var image=Imaging.CreateBitmapSourceFromHIcon(info.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image; }
        finally { if(info.Handle!=IntPtr.Zero)DestroyIcon(info.Handle); }
    }

    public static void Apply(string remote,ConnectionProfile profile)
    {
        if(!remote.StartsWith(@"\\server\",StringComparison.OrdinalIgnoreCase))return;
        // Per-share overrides avoid changing another application's use of the same letter.
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\"+remote.Replace('\\','#'));
        key.SetValue("_LabelFromReg",profile.Name.Trim());
        if(profile.DriveIconId<0)key.DeleteSubKeyTree("DefaultIcon",false);
        else if(Options.Any(option=>option.Id==profile.DriveIconId))
        {
            var info=new StockIcon {Size=(uint)Marshal.SizeOf<StockIcon>(),Path=""};
            if(SHGetStockIconInfo(profile.DriveIconId,0,ref info)==0)
            {
                using var icon=key.CreateSubKey("DefaultIcon");
                icon.SetValue("",$"\"{info.Path}\",{info.ResourceIndex}");
            }
        }
        SHChangeNotify(0x2000,0x1005,profile.Drive.TrimEnd(':')+@":\",IntPtr.Zero);
    }
}
