using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;

namespace DriverX.Desktop;

public sealed record DriveIconOption(int Id, string Name, string Group = "系统图标")
{
    public ImageSource? Preview => DriveAppearance.PreviewFor(Id);
}

internal static class DriveAppearance
{
    // Dark series ids stay out of the stock-icon range: 1000 + seriesIndex*100 + glyph (1..14).
    // Series order E F G H I J. Glyph order matches DarkGlyphs below.
    static readonly (string Key, string Name)[] DarkSeries = [
        ("E","玄武黑"), ("F","石墨金属"), ("G","黑曜霓虹"), ("H","暗夜线框"), ("I","黑金"), ("J","玄漆朱砂")];
    static readonly (string Key, string Name)[] DarkGlyphs = [
        ("net","网络磁盘"), ("hdd","硬盘"), ("server","服务器"), ("share","共享服务器"),
        ("folder","文件夹"), ("lock","安全锁"), ("shield","安全盾牌"), ("globe","全球网络"),
        ("workstation","工作站"), ("music","音乐"), ("picture","图片"), ("video","视频"),
        ("cloud","云存储"), ("gpu","GPU计算节点")];

    public static readonly DriveIconOption[] Options = BuildOptions();

    static DriveIconOption[] BuildOptions()
    {
        var list = new List<DriveIconOption>
        {
            new(-1,"默认 · Windows 网络磁盘","系统图标"), new(8,"硬盘","系统图标"), new(9,"网络磁盘","系统图标"), new(15,"服务器","系统图标"), new(51,"共享服务器","系统图标"),
            new(3,"文件夹","系统图标"), new(47,"安全锁","系统图标"), new(77,"安全盾牌","系统图标"),
            new(13,"全球网络","系统图标"), new(71,"音乐","系统图标"),
            new(72,"图片","系统图标"), new(73,"视频","系统图标")
        };
        for(var s=0;s<DarkSeries.Length;s++)
            for(var g=0;g<DarkGlyphs.Length;g++)
                list.Add(new(DarkIconId(s,g), $"{DarkSeries[s].Name} · {DarkGlyphs[g].Name}", DarkSeries[s].Name));
        return list.ToArray();
    }

    public static int DarkIconId(int series, int glyph) => 1000 + series * 100 + (glyph + 1);

    public static ListCollectionView CreateIconView()
    {
        var view = new ListCollectionView(Options);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DriveIconOption.Group)));
        return view;
    }

    // Legacy ids removed from the picker because they matched other options on this Windows:
    // 7 (removable) == 8 (hard disk), 94 (workstation) == 15 (server) via imageres -109.
    public static int NormalizeDriveIconId(int id) => id switch
    {
        7 => 8,
        94 => 15,
        _ => id
    };

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct StockIcon { public uint Size; public IntPtr Handle; public int SystemIndex; public int ResourceIndex; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Path; }
    [DllImport("shell32.dll")] static extern int SHGetStockIconInfo(int id,uint flags,ref StockIcon info);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern uint ExtractIconEx(string file,int index,out IntPtr large,out IntPtr small,uint count);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern void SHChangeNotify(uint id,uint flags,string path,IntPtr other);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint id,uint flags,IntPtr item1,IntPtr item2);

    static readonly Dictionary<int, ImageSource?> previewCache = new();

    public static ImageSource? PreviewFor(int id)
    {
        var key = id < 0 ? 9 : id;
        lock(previewCache)
        {
            if(previewCache.TryGetValue(key, out var cached)) return cached;
        }
        ImageSource? image;
        try { image = TryParseDarkIcon(key, out var series, out var glyph) ? LoadDarkPreview(series, glyph) : Preview(key); }
        catch { image = null; }
        if(image is not null) lock(previewCache) previewCache[key] = image;
        return image;
    }

    public static ImageSource? Preview(int id)
    {
        var info=new StockIcon {Size=(uint)Marshal.SizeOf<StockIcon>(),Path=""};
        if(SHGetStockIconInfo(id,0x100,ref info)!=0)return null;
        try { var image=Imaging.CreateBitmapSourceFromHIcon(info.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image; }
        finally { if(info.Handle!=IntPtr.Zero)DestroyIcon(info.Handle); }
    }

    // Setting "强制使用 DriverX 图标" (default on). Explorer consults the per-letter override
    // HKCU\Software\Classes\Applications\Explorer.exe\Drives\<L>\DefaultIcon before the per-share
    // MountPoints2 icon, so leftovers from other tools (e.g. RaiDrive pointing at a deleted exe) would hide
    // the DriverX icon. When on, DriverX writes that override itself and marks it as owned.
    public static bool ForceDriveIcon {get;set;}=true;
    const string LetterIconRoot=@"Software\Classes\Applications\Explorer.exe\Drives";
    const string OwnedMarker="DriverXOwned";
    const string PreviousValue="DriverXPrevious";

    public static void Apply(string remote,ConnectionProfile profile)
    {
        if(!remote.StartsWith(@"\\server\",StringComparison.OrdinalIgnoreCase))return;
        var letter=Letter(profile.Drive);
        try
        {
            // Register before the mount appears so Explorer's first icon lookup sees the right image.
            // Per-share overrides avoid changing another application's use of the same letter.
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\"+remote.Replace('\\','#'));
            if(key is null)return;
            key.SetValue("_LabelFromReg",profile.Name.Trim());
            var iconId=profile.DriveIconId<0?9:NormalizeDriveIconId(profile.DriveIconId);
            string? path=null;
            if(TryParseDarkIcon(iconId,out var series,out var glyph))path=SaveDarkIcon(series,glyph);
            else if(Options.Any(option=>option.Id==iconId))path=SaveStockIcon(iconId);
            if(path is not null)
            {
                var value=$"\"{path}\",0";
                using var icon=key.CreateSubKey("DefaultIcon");
                icon?.SetValue("",value);
                if(ForceDriveIcon&&letter is not null)ApplyLetterOverride(letter,value);
            }
            if(!ForceDriveIcon&&letter is not null)RemoveLetterOverride(letter,false);
            if(letter is not null)WarnMachineOverride(letter);
            SHChangeNotify(0x2000,0x1005,profile.Drive.TrimEnd(':')+@":\",IntPtr.Zero);
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        }
        catch(Exception) { /* A cosmetic failure must never prevent a mount. */ }
    }

    static bool TryParseDarkIcon(int id, out int series, out int glyph)
    {
        series=0; glyph=0;
        if(id<1000)return false;
        var rel=id-1000;
        series=rel/100;
        var oneBased=rel%100;
        glyph=oneBased-1;
        return series>=0&&series<DarkSeries.Length&&glyph>=0&&glyph<DarkGlyphs.Length;
    }

    static string DarkResourceName(int series, int glyph) => $"DriverX.dark.{DarkSeries[series].Key}-{DarkGlyphs[glyph].Key}.ico";
    static string DarkFileName(int series, int glyph) => $"dark-{DarkSeries[series].Key.ToLowerInvariant()}-{DarkGlyphs[glyph].Key}-v1.ico";

    static string? SaveDarkIcon(int series, int glyph)
    {
        var name=DarkFileName(series,glyph);
        var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DriverX","icons");
        var path=Path.Combine(directory,name);
        if(File.Exists(path)&&IsDarkIco(path)&&HasValidIcon(path))return path;
        try
        {
            using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream(DarkResourceName(series,glyph));
            if(resource is null){Log($"缺少深色图标资源 {DarkResourceName(series,glyph)}");return null;}
            using var memory=new MemoryStream();
            resource.CopyTo(memory);
            var bytes=memory.ToArray();
            if(!IsDarkIcoBytes(bytes)){Log($"深色图标资源无效 {DarkResourceName(series,glyph)}");return null;}
            Directory.CreateDirectory(directory);
            var temp=path+".tmp";
            File.WriteAllBytes(temp,bytes);
            File.Move(temp,path,true);
            if(HasValidIcon(path)){Log($"已释放深色图标 {path}");return path;}
            Log($"深色图标无法被系统读取 {path}");
        }
        catch(Exception ex){Log($"释放深色图标失败 {name}: {ex.Message}");}
        return null;
    }

    static ImageSource? LoadDarkPreview(int series, int glyph)
    {
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream(DarkResourceName(series,glyph));
        if(resource is null)return null;
        using var memory=new MemoryStream();
        resource.CopyTo(memory);
        if(!TrySlicePng(memory.ToArray(),32,out var png)&&!TrySlicePng(memory.ToArray(),48,out png))return null;
        using var pngStream=new MemoryStream(png);
        var frame=new PngBitmapDecoder(pngStream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }

    static bool TrySlicePng(byte[] bytes, int size, out byte[] png)
    {
        png=Array.Empty<byte>();
        if(!IcoEntries(bytes,out var entries))return false;
        foreach(var entry in entries)
        {
            if(entry.Width!=size)continue;
            png=new byte[entry.Length];
            Buffer.BlockCopy(bytes,entry.Offset,png,0,entry.Length);
            return png.Length>=8&&png[0]==0x89&&png[1]==0x50;
        }
        return false;
    }

    readonly record struct IcoEntry(int Width, int Length, int Offset);
    static bool IcoEntries(byte[] bytes, out List<IcoEntry> entries)
    {
        entries=new();
        if(bytes.Length<6||BitConverter.ToUInt16(bytes,0)!=0||BitConverter.ToUInt16(bytes,2)!=1)return false;
        int count=BitConverter.ToUInt16(bytes,4);
        if(count<5||bytes.Length<6+16*count)return false;
        for(var i=0;i<count;i++)
        {
            var entry=6+16*i;
            if(BitConverter.ToUInt16(bytes,entry+6)!=32)return false;
            var width=bytes[entry]==0?256:bytes[entry];
            var length=BitConverter.ToInt32(bytes,entry+8);
            var start=BitConverter.ToInt32(bytes,entry+12);
            if(length<8||start<0||(long)start+length>bytes.Length)return false;
            entries.Add(new(width,length,start));
        }
        return true;
    }

    static bool IsDarkIcoBytes(byte[] bytes)
    {
        if(!IcoEntries(bytes,out var entries))return false;
        var sizes=entries.Select(e=>e.Width).ToHashSet();
        if(!sizes.IsSupersetOf([16,32,48,64,256]))return false;
        foreach(var entry in entries)
            if(bytes[entry.Offset]!=0x89||bytes[entry.Offset+1]!=0x50)return false;
        return true;
    }

    static bool IsDarkIco(string path)
    {
        try{return IsDarkIcoBytes(File.ReadAllBytes(path));}
        catch{return false;}
    }

    static string? Letter(string drive)
    {
        var trimmed=drive?.Trim()??"";
        return trimmed.Length>0&&char.IsAsciiLetter(trimmed[0])?char.ToUpperInvariant(trimmed[0]).ToString():null;
    }

    static void ApplyLetterOverride(string letter,string value)
    {
        try
        {
            using var key=Registry.CurrentUser.CreateSubKey($@"{LetterIconRoot}\{letter}\DefaultIcon");
            if(key is null)return;
            var current=key.GetValue("",null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            if(key.GetValue(OwnedMarker) is null)
            {
                // Another tool's override: keep it for restoring later only if it still points at a real icon file;
                // dead leftovers (like RaiDrive.exe after uninstall) are simply replaced.
                if(!string.IsNullOrWhiteSpace(current))
                {
                    if(IconTargetExists(current)){key.SetValue(PreviousValue,current);Log($"{letter}: 已备份其他程序的盘符图标覆盖 {current}，卸载/退出时恢复");}
                    else Log($"{letter}: 替换失效的盘符图标覆盖 {current}");
                }
                key.SetValue(OwnedMarker,1,RegistryValueKind.DWord);
            }
            if(!string.Equals(current,value,StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue("",value);
                Log($"{letter}: 已写入 DriverX 盘符图标 {value}");
            }
        }
        catch(Exception ex){Log($"{letter}: 写入盘符图标失败 {ex.Message}");}
    }

    // Removes the per-letter override only if DriverX wrote it; restores a backed-up foreign value.
    public static bool RemoveLetterOverride(string drive,bool notify=true)
    {
        var letter=Letter(drive);
        if(letter is null)return false;
        try
        {
            string? previous;
            using(var icon=Registry.CurrentUser.OpenSubKey($@"{LetterIconRoot}\{letter}\DefaultIcon",true))
            {
                if(icon?.GetValue(OwnedMarker) is null)return false;
                previous=icon.GetValue(PreviousValue,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if(!string.IsNullOrWhiteSpace(previous))
                {
                    icon.SetValue("",previous);
                    icon.DeleteValue(PreviousValue,false);
                    icon.DeleteValue(OwnedMarker,false);
                }
            }
            if(string.IsNullOrWhiteSpace(previous))
            {
                bool empty;
                using(var letterKey=Registry.CurrentUser.OpenSubKey($@"{LetterIconRoot}\{letter}",true))
                {
                    if(letterKey is null)return false;
                    letterKey.DeleteSubKeyTree("DefaultIcon",false);
                    empty=letterKey.SubKeyCount==0&&letterKey.ValueCount==0;
                }
                if(empty){using var drives=Registry.CurrentUser.OpenSubKey(LetterIconRoot,true);drives?.DeleteSubKey(letter,false);}
                Log($"{letter}: 已移除 DriverX 写入的盘符图标");
            }
            else Log($"{letter}: 已恢复原有盘符图标覆盖 {previous}");
            if(notify)NotifyIconChanged(letter);
            return true;
        }
        catch(Exception ex){Log($"{letter}: 移除盘符图标失败 {ex.Message}");return false;}
    }

    // Drops every DriverX-owned per-letter override except the given letters (still mounted by DriverX).
    public static int RemoveOwnedOverrides(IEnumerable<string> keep)
    {
        var keepSet=keep.Select(Letter).Where(l=>l is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] letters;
        try{using var drives=Registry.CurrentUser.OpenSubKey(LetterIconRoot);letters=drives?.GetSubKeyNames()??[];}
        catch{return 0;}
        var removed=0;
        foreach(var name in letters)
            if(name.Length==1&&char.IsAsciiLetter(name[0])&&!keepSet.Contains(name)&&RemoveLetterOverride(name,false)){removed++;SHChangeNotify(0x2000,0x1005,name.ToUpperInvariant()+@":\",IntPtr.Zero);}
        if(removed>0)SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        return removed;
    }

    static void NotifyIconChanged(string letter)
    {
        SHChangeNotify(0x2000,0x1005,letter+@":\",IntPtr.Zero);
        SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
    }

    // HKLM DriveIcons needs admin rights to change; only report it.
    static readonly HashSet<string> warnedMachineOverrides=new(StringComparer.OrdinalIgnoreCase);
    static void WarnMachineOverride(string letter)
    {
        try
        {
            using var key=Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons\{letter}\DefaultIcon");
            if(key?.GetValue("") is string value&&!string.IsNullOrWhiteSpace(value)&&warnedMachineOverrides.Add(letter))
                Log($@"{letter}: 检测到系统级盘符图标 HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons\{letter} = {value}（需要管理员权限，DriverX 未修改）");
        }
        catch{}
    }

    static bool IconTargetExists(string value)
    {
        try
        {
            var text=value.Trim();
            string file;
            if(text.StartsWith('"')){var end=text.IndexOf('"',1);file=end>0?text[1..end]:text.Trim('"');}
            else{var comma=text.LastIndexOf(',');file=comma>0?text[..comma]:text;}
            file=Environment.ExpandEnvironmentVariables(file.Trim());
            return file.Length>0&&File.Exists(file);
        }
        catch{return false;}
    }

    static readonly object LogLock=new();
    static void Log(string message)
    {
        try
        {
            lock(LogLock)
            {
                var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DriverX","logs");
                Directory.CreateDirectory(folder);
                var file=Path.Combine(folder,"drive-icons.log");
                if(File.Exists(file)&&new FileInfo(file).Length>256*1024)File.Delete(file);
                File.AppendAllText(file,$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch{}
    }

    // v3: proper multi-size 32-bit ARGB icon (see DriveIconFile). A new file name also keeps Explorer's
    // icon cache from serving the broken single-image v2 file.
    static string? SaveStockIcon(int id)
    {
        var name=id==9?"windows-network-drive-v3.ico":$"stock-{id}-v3.ico";
        var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DriverX","icons");
        var path=Path.Combine(directory,name);
        if(File.Exists(path)&&DriveIconFile.IsComplete(path)&&HasValidIcon(path))return path;
        try
        {
            if(DriveIconFile.WriteStockIcon(id,path)&&HasValidIcon(path)){Log($"已生成多尺寸图标 {path}");return path;}
            Log($"生成图标失败 {path}");
        }
        catch(Exception ex){Log($"生成图标失败 {path}: {ex.Message}");}
        return null;
    }
    static bool HasValidIcon(string path)
    {
        var count=ExtractIconEx(path,0,out var large,out var small,1);
        var valid=count>0&&(large!=IntPtr.Zero||small!=IntPtr.Zero);
        if(large!=IntPtr.Zero)DestroyIcon(large);
        if(small!=IntPtr.Zero)DestroyIcon(small);
        return valid;
    }
}
