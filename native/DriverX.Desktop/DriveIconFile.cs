using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriverX.Desktop;

// Builds a proper multi-resolution Windows .ico (32-bit ARGB with real alpha) for a shell stock icon.
// The old pipeline saved an HICON through System.Drawing.Icon.Save, which writes a single DPI-sized
// image without a usable alpha channel, so Explorer drew black blocks around the drive icon.
internal static class DriveIconFile
{
    public static readonly int[] Sizes=[16,20,24,32,40,48,64,96,128,256];

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct StockIconInfo { public uint Size; public IntPtr Handle; public int SystemIndex; public int ResourceIndex; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Path; }
    [DllImport("shell32.dll")] static extern int SHGetStockIconInfo(int id,uint flags,ref StockIconInfo info);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHDefExtractIcon(string file,int index,uint flags,out IntPtr large,out IntPtr small,uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32.dll")] static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
    [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr data);
    [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module,IntPtr resource);
    public static string LastDiagnostics="";
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);

    /// <summary>Writes the stock icon as a multi-size .ico. Returns false if nothing usable could be extracted.</summary>
    public static bool WriteStockIcon(int stockId,string path)
    {
        // WPF imaging (DrawingVisual/RenderTargetBitmap) needs an STA thread; mounts may call this from the pool.
        if(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA)return WriteStockIconCore(stockId,path);
        var result=false;Exception? error=null;
        var worker=new Thread(()=>{try{result=WriteStockIconCore(stockId,path);}catch(Exception ex){error=ex;}}){IsBackground=true};
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        if(!worker.Join(TimeSpan.FromSeconds(20)))return false;
        if(error is not null)throw error;
        return result;
    }

    static bool WriteStockIconCore(int stockId,string path)
    {
        var info=new StockIconInfo{Size=(uint)Marshal.SizeOf<StockIconInfo>(),Path=""};
        if(SHGetStockIconInfo(stockId,0,ref info)!=0||string.IsNullOrWhiteSpace(info.Path))return false;
        var file=Environment.ExpandEnvironmentVariables(info.Path);
        var images=new List<(int Size,byte[] Bgra)>();
        var module=info.ResourceIndex<0?LoadLibraryEx(file,IntPtr.Zero,0x2|0x20):IntPtr.Zero;
        try
        {
            // Sizes drawn by Windows itself are taken verbatim; the rest (e.g. 96/128) are downscaled from the
            // largest native image with high-quality filtering instead of letting the loader stretch them.
            var native=NativeSizes(module,info.ResourceIndex);
            LastDiagnostics=$"{file},{info.ResourceIndex} native=[{string.Join(",",native)}]";
            BitmapSource? largest=null;
            foreach(var size in Sizes.OrderByDescending(s=>s))
            {
                var exact=native.Count==0||native.Contains(size);
                BitmapSource? source=exact||largest is null?Extract(file,info.ResourceIndex,size):null;
                if(source is not null&&(source.PixelWidth!=size||source.PixelHeight!=size))
                    source=Scale(largest is not null&&largest.PixelWidth>=size?largest:source,size);
                if(source is null&&largest is not null)source=Scale(largest,size);
                if(source is null)continue;
                if(largest is null||source.PixelWidth>largest.PixelWidth)largest=source;
                images.Add((size,Pixels(source)));
            }
        }
        finally{if(module!=IntPtr.Zero)FreeLibrary(module);}
        if(images.Count==0)return false;
        images.Sort((a,b)=>a.Size.CompareTo(b.Size));
        var directory=Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
        var temp=path+".tmp";
        File.WriteAllBytes(temp,BuildIco(images));
        File.Move(temp,path,true);
        return true;
    }

    // Reads the RT_GROUP_ICON directory to learn which square sizes the resource really contains.
    static HashSet<int> NativeSizes(IntPtr module,int index)
    {
        var sizes=new HashSet<int>();
        if(module==IntPtr.Zero||index>=0)return sizes;
        try
        {
            var resource=FindResource(module,(IntPtr)(-index),(IntPtr)14);
            if(resource==IntPtr.Zero)return sizes;
            var length=(int)SizeofResource(module,resource);
            var pointer=LockResource(LoadResource(module,resource));
            if(pointer==IntPtr.Zero||length<6)return sizes;
            var data=new byte[length];
            Marshal.Copy(pointer,data,0,length);
            int count=BitConverter.ToUInt16(data,4);
            for(var i=0;i<count&&6+14*i+14<=length;i++)
            {
                var entry=6+14*i;
                var width=data[entry]==0?256:data[entry];
                var height=data[entry+1]==0?256:data[entry+1];
                if(width==height&&BitConverter.ToUInt16(data,entry+6)>=32)sizes.Add(width);
            }
        }
        catch{sizes.Clear();}
        return sizes;
    }

    static BitmapSource? Extract(string file,int index,int size)
    {
        var handle=IntPtr.Zero;
        try
        {
            if(SHDefExtractIcon(file,index,0,out var large,out var small,(uint)size)==0)
            {
                handle=large;
                if(small!=IntPtr.Zero)DestroyIcon(small);
            }
            if(handle==IntPtr.Zero)return null;
            var image=Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
            converted.Freeze();
            return converted;
        }
        catch{return null;}
        finally{if(handle!=IntPtr.Zero)DestroyIcon(handle);}
    }

    static BitmapSource Scale(BitmapSource source,int size)
    {
        var visual=new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var context=visual.RenderOpen())context.DrawImage(source,new Rect(0,0,size,size));
        var target=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);
        target.Render(visual);
        var converted=new FormatConvertedBitmap(target,PixelFormats.Bgra32,null,0);
        converted.Freeze();
        return converted;
    }

    static byte[] Pixels(BitmapSource source)
    {
        var pixels=new byte[source.PixelWidth*source.PixelHeight*4];
        source.CopyPixels(pixels,source.PixelWidth*4,0);
        return pixels; // top-down, straight-alpha BGRA
    }

    static byte[] Png(int size,byte[] bgra)
    {
        var bitmap=BitmapSource.Create(size,size,96,96,PixelFormats.Bgra32,null,bgra,size*4);
        var encoder=new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // Classic 32-bpp DIB entry: BITMAPINFOHEADER (height doubled), bottom-up BGRA, then a 1-bpp AND mask.
    static byte[] Dib(int size,byte[] bgra)
    {
        var maskStride=((size+31)/32)*4;
        using var stream=new MemoryStream();
        using var writer=new BinaryWriter(stream);
        writer.Write(40);writer.Write(size);writer.Write(size*2);writer.Write((short)1);writer.Write((short)32);
        writer.Write(0);writer.Write(size*size*4+maskStride*size);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
        for(var y=size-1;y>=0;y--)writer.Write(bgra,y*size*4,size*4);
        for(var y=size-1;y>=0;y--)
        {
            var row=new byte[maskStride];
            for(var x=0;x<size;x++)if(bgra[(y*size+x)*4+3]==0)row[x>>3]|=(byte)(0x80>>(x&7));
            writer.Write(row);
        }
        writer.Flush();
        return stream.ToArray();
    }

    static byte[] BuildIco(List<(int Size,byte[] Bgra)> images)
    {
        var entries=images.Select(i=>(i.Size,Data:i.Size>=256?Png(i.Size,i.Bgra):Dib(i.Size,i.Bgra))).ToList();
        using var stream=new MemoryStream();
        using var writer=new BinaryWriter(stream);
        writer.Write((short)0);writer.Write((short)1);writer.Write((short)entries.Count);
        var offset=6+16*entries.Count;
        foreach(var (size,data) in entries)
        {
            writer.Write((byte)(size>=256?0:size));writer.Write((byte)(size>=256?0:size));
            writer.Write((byte)0);writer.Write((byte)0);writer.Write((short)1);writer.Write((short)32);
            writer.Write(data.Length);writer.Write(offset);
            offset+=data.Length;
        }
        foreach(var (_,data) in entries)writer.Write(data);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>True when the file is a multi-size 32-bpp icon written by this builder.</summary>
    public static bool IsComplete(string path)
    {
        try
        {
            var bytes=File.ReadAllBytes(path);
            if(bytes.Length<6||BitConverter.ToUInt16(bytes,0)!=0||BitConverter.ToUInt16(bytes,2)!=1)return false;
            int count=BitConverter.ToUInt16(bytes,4);
            if(count<Sizes.Length||bytes.Length<6+16*count)return false;
            for(var i=0;i<count;i++)
            {
                var entry=6+16*i;
                if(BitConverter.ToUInt16(bytes,entry+6)!=32)return false;
                var length=BitConverter.ToInt32(bytes,entry+8);var start=BitConverter.ToInt32(bytes,entry+12);
                if(length<=0||start<0||(long)start+length>bytes.Length)return false;
            }
            return true;
        }
        catch{return false;}
    }
}