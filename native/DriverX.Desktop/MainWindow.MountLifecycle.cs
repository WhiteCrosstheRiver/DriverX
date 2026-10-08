using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Windows;
using MessageBox=System.Windows.MessageBox;

namespace DriverX.Desktop;

public partial class MainWindow
{
    readonly Dictionary<string,Task> mountCleanups=new(StringComparer.OrdinalIgnoreCase);

    async void ResetMount(object sender,RoutedEventArgs args)
    {
        if((sender as System.Windows.Controls.Button)?.Tag is ConnectionProfile profile)
            await ChangeMountAsync(profile,true);
    }

    async Task ChangeMountAsync(ConnectionProfile profile,bool reset)
    {
        var drive=NormalizeDrive(profile.Drive);
        if(drive.Length!=1||!pendingMountDrives.Add(drive[0]))return;
        try
        {
            var wasMounted=mounts.ContainsKey(profile);
            var staleOwnMapping=reset&&!wasMounted&&string.Equals(NetworkRemotePath(drive),@"\\server\"+VolumeName(profile),StringComparison.OrdinalIgnoreCase);
            if(wasMounted||staleOwnMapping)await UnmountProfileAsync(profile);
            if(wasMounted&&!reset)return;

            if(!await ServerAvailableAsync(profile))
            {
                profile.ConnectionError="服务器不可达，已清理失效盘符";
                UpdateStatus();
                MessageBox.Show($"{profile.Name} 的服务器当前无法连接。旧盘符已清理，恢复网络后可点击“挂载”或“重置”。","DriverX",MessageBoxButton.OK,MessageBoxImage.Warning);
                return;
            }
            await MountProfileAsync(profile);
        }
        catch(Exception ex)
        {
            profile.ConnectionError=ex.Message;
            UpdateStatus();
            MessageBox.Show($"{(reset?"重置":"挂载/卸载")} {profile.Name} 失败：{ex.Message}","DriverX",MessageBoxButton.OK,MessageBoxImage.Error);
        }
        finally {pendingMountDrives.Remove(drive[0]);}
    }

    async Task UnmountProfileAsync(ConnectionProfile profile,bool forgetExplorerLabel=true)
    {
        var drive=NormalizeDrive(profile.Drive);
        if(mounts.TryGetValue(profile,out var process)&&!process.HasExited)
            await Task.Run(()=>StopMountProcess(process));
        if(!mountCleanups.TryGetValue(drive,out var cleanup)||cleanup.IsCompleted)
        {
            cleanup=Task.Run(()=>RemoveWindowsNetworkMount(drive));
            mountCleanups[drive]=cleanup;
        }
        await cleanup.WaitAsync(TimeSpan.FromSeconds(8));
        if(Environment.GetLogicalDrives().Any(root=>NormalizeDrive(root)==drive))
            throw new IOException($"{drive}: 仍被 Windows 占用，未启动新挂载");
        NotifyDriveRemoved(drive);
        // Only a deliberate unmount drops the ##server#<name> label/icon; a failed mount keeps it so the
        // next mount (or a manual rclone mount) still shows "name (X:)" with the DriverX drive icon.
        if(forgetExplorerLabel){RemoveExplorerShareEntry(VolumeName(profile));DriveAppearance.RemoveLetterOverride(drive);}
        ForgetOwnedMount(drive);
        mounts.Remove(profile);
        profile.IsMounted=false;
        profile.ConnectionError=null;
        PromoteDrive(profile);
        UpdateStatus();
    }

    static async Task<bool> ServerAvailableAsync(ConnectionProfile profile)
    {
        var protocol=profile.Protocol.ToLowerInvariant();
        if(protocol is not ("sftp" or "ftp" or "smb" or "webdav"))return true;
        var host=profile.Host;
        var port=protocol=="smb"?445:profile.Port;
        if(protocol=="webdav")
        {
            if(!Uri.TryCreate(host,UriKind.Absolute,out var uri))return false;
            host=uri.Host;
            port=uri.Port;
        }
        try
        {
            using var client=new TcpClient();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host,port,timeout.Token);
            return client.Connected;
        }
        catch {return false;}
    }

    async Task MountProfileAsync(ConnectionProfile profile)
    {
        var drive=NormalizeDrive(profile.Drive);
        if(mounts.Keys.Any(other=>other!=profile&&NormalizeDrive(other.Drive)==drive)||
           Environment.GetLogicalDrives().Any(root=>NormalizeDrive(root)==drive))
            throw new IOException($"盘符 {drive}: 已被占用");
        var rclone=RclonePath()??throw new FileNotFoundException("未找到 rclone");
        var remote="driverx-"+Math.Abs(profile.Name.GetHashCode());
        await RunHidden(BuildConfigArgs(remote,profile));
        DriveAppearance.Apply(@"\\server\"+VolumeName(profile),profile);
        var log=MountLogPath(profile);
        long logStart=0;
        try{if(File.Exists(log))logStart=new FileInfo(log).Length;}catch{}
        var process=Process.Start(MountStart(rclone,BuildMountArgs(remote,profile)))
            ??throw new InvalidOperationException("rclone 挂载进程未启动");
        mounts[profile]=process;
        WatchMountProcess(process,drive);
        RegisterOwnedMount(drive);
        StatusText.Text=$"正在连接 {profile.Name}…";
        // rclone only creates the drive letter after the SSH/SFTP login finishes. Some servers (e.g. gpu02)
        // regularly need 8-10+ s for that, so a fixed 10 s wait killed mounts that were about to succeed.
        // Keep waiting as long as rclone is alive, up to MountAppearTimeout.
        var deadline=DateTime.UtcNow+MountAppearTimeout;
        while(DateTime.UtcNow<deadline)
        {
            if(process.HasExited)break;
            var network=NetworkRemotePath(drive);
            if(network is not null)
            {
                DriveAppearance.Apply(network,profile);
                profile.IsMounted=true;
                profile.ConnectionError=null;
                PromoteDrive(profile);
                UpdateStatus();
                return;
            }
            await Task.Delay(250);
        }
        var exited=process.HasExited;
        await UnmountProfileAsync(profile,false);
        var reason=exited?"rclone 挂载进程已退出":$"盘符在 {(int)MountAppearTimeout.TotalSeconds} 秒内没有出现（服务器登录过慢或无响应）";
        var detail=LastMountLogError(log,logStart);
        throw new IOException(detail is null?reason:$"{reason}：{detail}");
    }

    static readonly TimeSpan MountAppearTimeout=TimeSpan.FromSeconds(45);

    // Last real error rclone logged during this mount attempt, shown in the failure dialog.
    static string? LastMountLogError(string log,long offset)
    {
        try
        {
            using var stream=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            if(offset<0||offset>stream.Length)offset=0;
            stream.Seek(offset,SeekOrigin.Begin);
            using var reader=new StreamReader(stream);
            var line=reader.ReadToEnd().Split('\n').Select(l=>l.Trim())
                .LastOrDefault(l=>(l.Contains(" ERROR ")||l.Contains(" CRITICAL ")||l.Contains("Fatal error"))&&!l.Contains("symlinks not supported"));
            if(line is null)return null;
            var colon=line.IndexOf(" : ",StringComparison.Ordinal);
            if(colon>=0)line=line[(colon+3)..];
            return line.Length>200?line[..200]+"…":line;
        }
        catch{return null;}
    }
}
