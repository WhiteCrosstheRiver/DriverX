using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Net.Sockets;

namespace DriverX.Desktop;
public partial class MainWindow
{
    System.Windows.Point dragOrigin;
    ConnectionProfile? dragProfile;
    readonly DispatcherTimer healthTimer=new(){Interval=TimeSpan.FromSeconds(15)};
    bool checkingHealth;
    void InitializeDriveInteraction()
    {
        ConnectionList.AllowDrop=true;
        ConnectionList.PreviewMouseLeftButtonDown+=(_,e)=>{
            dragProfile=null;
            var node=e.OriginalSource as DependencyObject;
            while(node is not null){if(node is System.Windows.Controls.Primitives.ButtonBase)return;node=VisualTreeHelper.GetParent(node);}
            dragOrigin=e.GetPosition(ConnectionList);
            dragProfile=(ItemsControl.ContainerFromElement(ConnectionList,e.OriginalSource as DependencyObject) as FrameworkElement)?.DataContext as ConnectionProfile;
        };
        ConnectionList.MouseMove+=(_,e)=>{
            if(e.LeftButton!=MouseButtonState.Pressed||dragProfile is null)return;
            var point=e.GetPosition(ConnectionList);
            if(Math.Abs(point.X-dragOrigin.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-dragOrigin.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            var profile=dragProfile;dragProfile=null;System.Windows.DragDrop.DoDragDrop(ConnectionList,new System.Windows.DataObject(typeof(ConnectionProfile),profile),System.Windows.DragDropEffects.Move);
        };
        ConnectionList.Drop+=(_,e)=>{
            if(e.Data.GetData(typeof(ConnectionProfile)) is not ConnectionProfile source)return;
            var target=(ItemsControl.ContainerFromElement(ConnectionList,e.OriginalSource as DependencyObject) as FrameworkElement)?.DataContext as ConnectionProfile;
            if(target is null||source==target)return;
            // Explicit manual positioning takes precedence over automatic grouping.
            driveView.MountedFirst=false;if(mountedFirstChoice is not null)mountedFirstChoice.IsChecked=false;
            SortDriveView();profiles.Move(profiles.IndexOf(source),profiles.IndexOf(target));SaveProfiles();SaveDriveView();e.Handled=true;
        };
        healthTimer.Tick+=async(_,_)=>await CheckMountHealth();healthTimer.Start();
        Closed+=(_,_)=>healthTimer.Stop();
    }
    async Task CheckMountHealth()
    {
        if(checkingHealth||shuttingDown)return;checkingHealth=true;
        try{
            foreach(var entry in mounts.ToArray()){
                var profile=entry.Key;string? error=null;
                if(entry.Value.HasExited)error="挂载进程已退出";
                else if(profile.Protocol is "sftp" or "ftp" or "smb"){
                    try{using var client=new TcpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));await client.ConnectAsync(profile.Host,profile.Protocol=="smb"?445:profile.Port,timeout.Token);}
                    catch{error="服务器连接失败";}
                }
                if(shuttingDown)return;
                if(mounts.TryGetValue(profile,out var current)&&current==entry.Value)profile.ConnectionError=error;
            }
            ConnectionList.Items.Refresh();
        }finally{checkingHealth=false;}
    }
}
