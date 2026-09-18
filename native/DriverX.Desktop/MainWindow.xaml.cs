using Control = System.Windows.Controls.Control;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
using System.Reflection;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using System.Text.Json.Serialization;

namespace DriverX.Desktop;
public partial class MainWindow : Window
{
 readonly ObservableCollection<ConnectionProfile> profiles=[]; readonly Dictionary<ConnectionProfile,Process> mounts=[]; readonly Forms.NotifyIcon trayIcon; bool shuttingDown;
 static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
 public MainWindow(){InitializeComponent();Microsoft.Win32.SystemEvents.UserPreferenceChanged+=SystemPreferenceChanged;trayIcon=CreateTrayIcon();LoadTheme();LoadProfiles();ConnectionList.ItemsSource=profiles;RenderProtocols();UpdateStatus();Loaded+=async(_,_)=>await RefreshMountStateAsync(true);}
 Forms.NotifyIcon CreateTrayIcon(){var exe=Process.GetCurrentProcess().MainModule?.FileName;var appIcon=exe is not null?System.Drawing.Icon.ExtractAssociatedIcon(exe):System.Drawing.SystemIcons.Application;var icon=new Forms.NotifyIcon{Icon=appIcon,Text="DriverX · 远程磁盘",Visible=true};var menu=new Forms.ContextMenuStrip();menu.Items.Add("打开 DriverX",null,(_,_)=>ShowFromTray());menu.Items.Add("刷新并清理遗留盘符",null,(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{_ = RefreshMountStateAsync(false);})));menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("完全退出并清理所有 DriverX 盘符",null,(_,_)=>{shuttingDown=true;Close();});icon.ContextMenuStrip=menu;icon.DoubleClick+=(_,_)=>ShowFromTray();return icon;}
 void ShowFromTray(){Show();WindowState=WindowState.Normal;Activate();}
 void TitleBarDrag(object s,MouseButtonEventArgs e){if(e.LeftButton==MouseButtonState.Pressed)DragMove();}
 void MinimizeWindow(object s,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
 void MaximizeWindow(object s,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
 void CloseWindow(object s,RoutedEventArgs e){shuttingDown=true;Close();}
 void WindowClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(shuttingDown)ShutdownMounts();else{shuttingDown=true;ShutdownMounts();}Microsoft.Win32.SystemEvents.UserPreferenceChanged-=SystemPreferenceChanged;trayIcon.Visible=false;trayIcon.Dispose();}
 void ShutdownMounts(){var drives=profiles.Select(p=>p.Drive).Concat(mounts.Keys.Select(p=>p.Drive)).ToArray();StopTrackedMounts();CleanupDriverXMounts(drives,true);mounts.Clear();foreach(var profile in profiles)profile.IsMounted=false;try{SaveProfiles();}catch{}}
 static string ActiveMountsPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX","active-mounts.json");
 [DllImport("mpr.dll",CharSet=CharSet.Unicode)] static extern int WNetGetConnection(string localName,StringBuilder remoteName,ref int length);
 [DllImport("mpr.dll",CharSet=CharSet.Unicode)] static extern int WNetCancelConnection2(string name,int flags,bool force);
 [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
 const int ConnectUpdateProfile=1;

 async void RefreshMountState(object s,RoutedEventArgs e)=>await RefreshMountStateAsync(false);
 async Task RefreshMountStateAsync(bool startup)
 {
  var active=mounts.Keys.Select(p=>p.Drive).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var candidates=profiles.Where(p=>startup||!active.Contains(p.Drive)).Select(p=>p.Drive).ToArray();
  StatusText.Text=startup?"正在清理上次遗留的 DriverX 挂载…":"正在刷新并清理遗留挂载…";
  var cleaned=await Task.Run(()=>CleanupDriverXMounts(candidates,startup,startup?[]:active));
  if(startup){foreach(var profile in profiles)profile.IsMounted=false;try{SaveProfiles();}catch{}}
  foreach(var item in mounts.Where(x=>cleaned.Drives.Contains(NormalizeDrive(x.Key.Drive))).ToArray()){try{if(!item.Value.HasExited)item.Value.Kill(true);}catch{}mounts.Remove(item.Key);item.Key.IsMounted=false;}
  UpdateStatus();
  if(!startup)MessageBox.Show(cleaned.Count==0?"未发现 DriverX 遗留盘符。":"已清理 "+cleaned.Count+" 个 DriverX 遗留盘符："+string.Join("、",cleaned.Drives.Select(d=>d+":")),"DriverX 刷新",MessageBoxButton.OK,MessageBoxImage.Information);
 }

 static CleanupResult CleanupDriverXMounts(IEnumerable<string> configuredDrives,bool includeConfigured,IEnumerable<string>? protectedDrives=null)
 {
  var candidates=new HashSet<string>(LoadOwnedMounts(),StringComparer.OrdinalIgnoreCase);
  if(includeConfigured)foreach(var drive in configuredDrives)candidates.Add(NormalizeDrive(drive));
  foreach(var drive in FindLegacyDriverXMounts())candidates.Add(drive);
  foreach(var drive in protectedDrives??[])candidates.Remove(NormalizeDrive(drive));
  var cleaned=new List<string>();
  foreach(var drive in candidates.Where(d=>d.Length==1&&char.IsLetter(d[0])))
  {
   var wasOwned=LoadOwnedMounts().Contains(drive,StringComparer.OrdinalIgnoreCase)||IsLegacyDriverXMount(drive);
   if(!includeConfigured&&!wasOwned)continue;
   StopDriverXMountProcesses(drive);
   if(IsOwnedNetworkMount(drive)||wasOwned)RemoveWindowsNetworkMount(drive);
   if(!Environment.GetLogicalDrives().Any(root=>NormalizeDrive(root)==drive))cleaned.Add(drive);
  }
  ClearExplorerDriverXHistory();
  var remaining=LoadOwnedMounts();foreach(var drive in cleaned)remaining.Remove(drive);SaveOwnedMounts(remaining);
  return new CleanupResult(cleaned);
 }
 void StopTrackedMounts(){foreach(var item in mounts.ToArray()){try{if(!item.Value.HasExited){item.Value.Kill(true);item.Value.WaitForExit(3000);}}catch{}}}
 static void StopDriverXMountProcesses(string drive)
 {
  foreach(var id in FindDriverXMountProcessIds(drive))try{using var process=Process.GetProcessById(id);if(!process.HasExited){process.Kill(true);process.WaitForExit(3000);}}catch{}
 }
 static IEnumerable<int> FindDriverXMountProcessIds(string drive)
 {
  try
  {
   const string query="Get-CimInstance Win32_Process -Filter \"Name='rclone.exe'\" | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress";
   var shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
   var psi=HiddenStart(shell,["-NoProfile","-NonInteractive","-Command",query]);
   using var process=Process.Start(psi);if(process is null)return [];
   var output=process.StandardOutput.ReadToEnd();process.WaitForExit(5000);if(string.IsNullOrWhiteSpace(output))return [];
   using var json=JsonDocument.Parse(output);var rows=json.RootElement.ValueKind==JsonValueKind.Array?json.RootElement.EnumerateArray().ToArray():[json.RootElement];
   return rows.Where(row=>row.TryGetProperty("CommandLine",out var command)&&TargetsDrive(command.GetString(),drive)&&row.TryGetProperty("ProcessId",out var id)).Select(row=>row.GetProperty("ProcessId").GetInt32()).ToArray();
  }
  catch{return [];}
 }
 static bool TargetsDrive(string? command,string drive)
 {
  if(string.IsNullOrWhiteSpace(command)||!command.Contains(" mount ",StringComparison.OrdinalIgnoreCase))return false;
  var target=NormalizeDrive(drive)+":";
  return command.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Any(token=>string.Equals(token.Trim('"'),target,StringComparison.OrdinalIgnoreCase));
 }
 static void RemoveWindowsNetworkMount(string drive)
 {
  try{WNetCancelConnection2(drive+":",ConnectUpdateProfile,true);}catch{}
  try{using var net=Process.Start(HiddenStart("net.exe",["use",drive+":","/delete","/y"]));net?.WaitForExit(3000);}catch{}
  try{WNetCancelConnection2(drive+":",ConnectUpdateProfile,true);}catch{}
 }
 static void ClearExplorerDriverXHistory()
 {
  try
  {
   const string path=@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2";
   using var points=Registry.CurrentUser.OpenSubKey(path,true);if(points is null)return;
   var stale=points.GetSubKeyNames().Where(name=>name.StartsWith("##server#driverx-",StringComparison.OrdinalIgnoreCase)||name.StartsWith("##server#sftp",StringComparison.OrdinalIgnoreCase)).ToArray();
   foreach(var name in stale)points.DeleteSubKeyTree(name,false);
   if(stale.Length>0)SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
  }
  catch{}
 }
 static string NormalizeDrive(string drive)=>string.IsNullOrWhiteSpace(drive)?string.Empty:char.ToUpperInvariant(drive.Trim()[0]).ToString();
 static IEnumerable<string> FindLegacyDriverXMounts()=>Environment.GetLogicalDrives().Select(NormalizeDrive).Where(IsLegacyDriverXMount);
 static bool IsLegacyDriverXMount(string drive){var remote=NetworkRemotePath(drive);return remote is not null&&(remote.StartsWith(@"\\server\driverx-",StringComparison.OrdinalIgnoreCase)||remote.StartsWith(@"\\server\sftp",StringComparison.OrdinalIgnoreCase));}
 static bool IsOwnedNetworkMount(string drive){var remote=NetworkRemotePath(drive);return remote is not null&&remote.StartsWith(@"\\server\",StringComparison.OrdinalIgnoreCase);}
 static string? NetworkRemotePath(string drive){try{var length=2048;var value=new StringBuilder(length);return WNetGetConnection(drive+":",value,ref length)==0?value.ToString():null;}catch{return null;}}
 static HashSet<string> LoadOwnedMounts(){try{return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(ActiveMountsPath))?.Select(NormalizeDrive).Where(d=>d.Length==1).ToHashSet(StringComparer.OrdinalIgnoreCase)??new(StringComparer.OrdinalIgnoreCase);}catch{return new(StringComparer.OrdinalIgnoreCase);}}
 static void SaveOwnedMounts(IEnumerable<string> drives){try{Directory.CreateDirectory(Path.GetDirectoryName(ActiveMountsPath)!);File.WriteAllText(ActiveMountsPath,JsonSerializer.Serialize(drives.Select(NormalizeDrive).Where(d=>d.Length==1).Distinct()));}catch{}}
 static void RegisterOwnedMount(string drive){var owned=LoadOwnedMounts();owned.Add(NormalizeDrive(drive));SaveOwnedMounts(owned);}
 static void ForgetOwnedMount(string drive){var owned=LoadOwnedMounts();owned.Remove(NormalizeDrive(drive));SaveOwnedMounts(owned);}
 sealed record CleanupResult(IReadOnlyList<string> Drives){public int Count=>Drives.Count;}
 void LoadProfiles(){var user=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX","profiles.json");var imported=Path.Combine(AppContext.BaseDirectory,"import","raidrive_connections.json");var file=File.Exists(user)?user:imported;if(!File.Exists(file))return;try{foreach(var p in JsonSerializer.Deserialize<List<ConnectionProfile>>(File.ReadAllText(file),JsonOptions)??[])profiles.Add(p);}catch(Exception e){MessageBox.Show($"读取连接失败：{e.Message}");}}
 void SaveProfiles(){var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"profiles.json"),JsonSerializer.Serialize(profiles,JsonOptions));}
 static string? RclonePath(){var embedded=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DriverX","bin","rclone.exe");try{if(!File.Exists(embedded)){Directory.CreateDirectory(Path.GetDirectoryName(embedded)!);using var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("DriverX.rclone.exe");if(input is not null){using var output=File.Create(embedded);input.CopyTo(output);}}}catch{}if(File.Exists(embedded))return embedded;var bundled=Path.Combine(AppContext.BaseDirectory,"rclone.exe");if(File.Exists(bundled))return bundled;var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WinGet","Links","rclone.exe");if(File.Exists(p))return p;return Environment.GetEnvironmentVariable("PATH")?.Split(';').Select(x=>Path.Combine(x,"rclone.exe")).FirstOrDefault(File.Exists);}
 void UpdateStatus(){CountText.Text=$"{profiles.Count} 个连接  ·  {mounts.Count} 个挂载";StatusText.Text=RclonePath() is null?"未找到 rclone":"rclone + WinFsp 就绪 · 后台静默运行";StatusDot.Fill=new SolidColorBrush(RclonePath() is null?Color.FromRgb(230,81,74):Color.FromRgb(32,178,107));DriveTotalText.Text=profiles.Count.ToString();DriveMountedText.Text=mounts.Count.ToString();DriveProtocolText.Text=profiles.Select(p=>p.Protocol).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString();DriveLatencyText.Text=$"目录 {dirCacheTime} · 属性 {attrTimeout}";ConnectionList.Items.Refresh();}
 void SetPage(string title,string sub,UIElement page){PageTitle.Text=title;PageSubtitle.Text=sub;DrivesPage.Visibility=ProtocolsPage.Visibility=SettingsPage.Visibility=InfoPage.Visibility=Visibility.Collapsed;page.Visibility=Visibility.Visible;AddButton.Visibility=page==DrivesPage?Visibility.Visible:Visibility.Collapsed;foreach(var item in new[]{(DrivesNav,(UIElement)DrivesPage),(ProtocolsNav,(UIElement)ProtocolsPage),(ActivityNav,(UIElement)InfoPage),(SettingsNav,(UIElement)SettingsPage)}){item.Item1.SetResourceReference(Control.ForegroundProperty,item.Item2==page?"Accent":"TextPrimary");item.Item1.SetResourceReference(Control.BackgroundProperty,item.Item2==page?"AccentSoft":"SidebarBackground");}}
 void ShowDrives(object s,RoutedEventArgs e)=>SetPage("我的磁盘","连接远程存储，像本地磁盘一样使用。",DrivesPage);
 void ShowProtocols(object s,RoutedEventArgs e)=>SetPage("协议中心","从一个界面管理常用远程存储协议。",ProtocolsPage);
 void ShowSettings(object s,RoutedEventArgs e)=>SetPage("设置","调整 DriverX 的外观和运行方式。",SettingsPage);
 void ShowActivity(object s,RoutedEventArgs e){InfoText.Text=$"当前 {mounts.Count} 个后台挂载进程正在运行。\n\n所有 rclone 进程均通过 CreateNoWindow 启动。";SetPage("活动与状态","查看挂载进程和运行状态。",InfoPage);}
 void RenderProtocols(){ProtocolCards.Children.Clear();var items=new[]{("SFTP","SSH 加密传输","密码 / 私钥","Linux、服务器、NAS","基础支持"),("WebDAV","HTTPS 文件访问","TLS + 账号","NAS、网盘、协作服务","基础支持"),("FTP","传统文件传输","账号或匿名","旧服务器与设备","基础支持"),("SMB","Windows 文件共享","账号 + 局域网","Windows、NAS、共享目录","基础支持"),("S3","对象存储接口","Access / Secret Key","云存储与备份桶","基础支持"),("OneDrive","Microsoft 云盘","OAuth 授权","个人与企业云盘","授权开发中"),("Google Drive","Google 云端硬盘","OAuth 授权","个人与团队文件","授权开发中"),("Dropbox","Dropbox 云存储","OAuth 授权","个人与团队文件","授权开发中")};foreach(var p in items){var panel=new StackPanel();var title=new TextBlock{Text=p.Item1,FontWeight=FontWeights.SemiBold};title.SetResourceReference(TextBlock.FontSizeProperty,"CardTitleSize");panel.Children.Add(title);foreach(var line in new[]{p.Item2,$"认证：{p.Item3}",$"适用：{p.Item4}"}){var text=new TextBlock{Text=line,Margin=new(0,6,0,0),TextWrapping=TextWrapping.Wrap};text.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");text.SetResourceReference(TextBlock.FontSizeProperty,"CaptionSize");panel.Children.Add(text);}var status=new TextBlock{Text="● "+p.Item5,Margin=new(0,12,0,0),FontWeight=FontWeights.SemiBold};status.SetResourceReference(TextBlock.ForegroundProperty,p.Item5=="基础支持"?"Accent":"TextSecondary");panel.Children.Add(status);var card=new Border{Width=280,MinHeight=166,Margin=new(0,0,16,16),Padding=new(18),CornerRadius=new(8),BorderThickness=new(1),Child=panel};card.SetResourceReference(Border.BackgroundProperty,"CardBackground");card.SetResourceReference(Border.BorderBrushProperty,"BorderBrush");ProtocolCards.Children.Add(card);}}
 void AddConnection(object s,RoutedEventArgs e){try{var d=new ConnectionDialog(null,profiles.Select(p=>p.Drive)){Owner=this};if(d.ShowDialog()==true){profiles.Add(d.Profile);SaveProfiles();UpdateStatus();}}catch(Exception ex){MessageBox.Show($"连接未保存：{ex.Message}","DriverX",MessageBoxButton.OK,MessageBoxImage.Error);}}
 void EditConnection(object s,RoutedEventArgs e){if((s as Button)?.Tag is not ConnectionProfile current||current.IsMounted)return;try{var d=new ConnectionDialog(current,profiles.Where(p=>p!=current).Select(p=>p.Drive)){Owner=this};if(d.ShowDialog()==true){profiles[profiles.IndexOf(current)]=d.Profile;SaveProfiles();UpdateStatus();}}catch(Exception ex){MessageBox.Show($"连接未保存：{ex.Message}","DriverX",MessageBoxButton.OK,MessageBoxImage.Error);}}
 void DeleteConnection(object s,RoutedEventArgs e){if((s as Button)?.Tag is not ConnectionProfile p)return;if(p.IsMounted){MessageBox.Show("请先卸载此连接。","DriverX");return;}if(MessageBox.Show($"确定删除“{p.Name}”？","删除连接",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){profiles.Remove(p);SaveProfiles();UpdateStatus();}}
 async void ToggleMount(object s,RoutedEventArgs e){if((s as Button)?.Tag is not ConnectionProfile p)return;if(mounts.TryGetValue(p,out var running)){try{await RunHidden("unmount",$"{p.Drive}:");if(!running.HasExited)running.Kill(true);}catch{}ForgetOwnedMount(p.Drive);mounts.Remove(p);p.IsMounted=false;UpdateStatus();return;}var driveLetter=char.ToUpperInvariant(p.Drive[0]);var systemConflict=Environment.GetLogicalDrives().Any(d=>char.ToUpperInvariant(d[0])==driveLetter);var profileConflict=profiles.Any(other=>other!=p&&!string.IsNullOrWhiteSpace(other.Drive)&&char.ToUpperInvariant(other.Drive[0])==driveLetter);if(systemConflict||profileConflict){MessageBox.Show($"盘符 {p.Drive.TrimEnd(':').ToUpperInvariant()}: 当前已被 Windows 或其他 DriverX 连接占用。请编辑连接并选择空闲盘符。","盘符冲突",MessageBoxButton.OK,MessageBoxImage.Warning);return;}var r=RclonePath();if(r is null){MessageBox.Show("未找到 rclone，请先安装。");return;}try{var remote="driverx-"+Math.Abs(p.Name.GetHashCode());await RunHidden(BuildConfigArgs(remote,p));var psi=HiddenStart(r,BuildMountArgs(remote,p));mounts[p]=Process.Start(psi)!;RegisterOwnedMount(p.Drive);p.IsMounted=true;UpdateStatus();}catch(Exception ex){MessageBox.Show($"挂载失败：{ex.Message}");}}
 string[] BuildMountArgs(string remote,ConnectionProfile p)=>["mount",$"{remote}:{p.Path}",$"{p.Drive}:","--network-mode","--volname",VolumeName(p),"--dir-cache-time",dirCacheTime,"--attr-timeout",attrTimeout,"--poll-interval","0","--vfs-cache-mode",vfsCacheMode,"--buffer-size",bufferSize,"--transfers",transfers,"--checkers",transfers,"--vfs-read-chunk-size",readChunkSize,"--vfs-read-chunk-size-limit","64M"];
 string VolumeName(ConnectionProfile p){var forbidden=Path.GetInvalidFileNameChars().Concat(['\\','/']).ToHashSet();var clean=new string(p.Name.Trim().Where(c=>!forbidden.Contains(c)).ToArray()).Trim();if(string.IsNullOrWhiteSpace(clean))clean="DriverX";if(clean.Length>48)clean=clean[..48];return profiles.Count(other=>other!=p&&string.Equals(other.Name.Trim(),p.Name.Trim(),StringComparison.OrdinalIgnoreCase))>0?$"{clean}-{p.Drive.TrimEnd(':').ToUpperInvariant()}":clean;}
 static string[] BuildConfigArgs(string remote,ConnectionProfile p){var a=new List<string>{"config","create",remote,p.Protocol};if(p.Protocol is "sftp" or "ftp" or "smb"){a.AddRange(["host",p.Host,"user",p.User,"port",p.Port.ToString()]);if(!string.IsNullOrWhiteSpace(p.Password))a.AddRange(["pass",p.Password]);}if(p.Protocol=="sftp"&&!string.IsNullOrWhiteSpace(p.Keyfile))a.AddRange(["key_file",Environment.ExpandEnvironmentVariables(p.Keyfile)]);if(p.Protocol=="webdav")a.AddRange(["url",p.Host,"vendor","other","user",p.User,"pass",p.Password]);return[..a];}
 static ProcessStartInfo HiddenStart(string file,IEnumerable<string> args){var p=new ProcessStartInfo(file){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true,RedirectStandardInput=true};foreach(var a in args)p.ArgumentList.Add(a);return p;}
 static async Task RunHidden(params string[] args){var p=Process.Start(HiddenStart(RclonePath()!,args))!;await p.WaitForExitAsync();if(p.ExitCode!=0)throw new InvalidOperationException(await p.StandardError.ReadToEndAsync());}
 void OpenDrive(object s,RoutedEventArgs e){if((s as Button)?.Tag is ConnectionProfile p)Process.Start(new ProcessStartInfo("explorer.exe",$"{p.Drive}:\\"){UseShellExecute=true});}
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 void WindowSourceInitialized(object? s,EventArgs e){var hwnd=new WindowInteropHelper(this).Handle;var rounded=2;DwmSetWindowAttribute(hwnd,33,ref rounded,sizeof(int));UpdateWindowTheme();}
}
public class ConnectionProfile
{
 public string Name{get;set;}="新连接";public string Protocol{get;set;}="sftp";public string Host{get;set;}="";public int Port{get;set;}=22;public string User{get;set;}="";public string Password{get;set;}="";public string Keyfile{get;set;}="";public string Path{get;set;}="/";public string Drive{get;set;}="Z";public bool IsMounted{get;set;}
 [JsonIgnore] public string DriveLabel=>Drive.TrimEnd(':').ToUpperInvariant()+":";
 [JsonIgnore] public string Endpoint=>string.IsNullOrWhiteSpace(User)?Host:$"{User}@{Host}:{Port}";
 [JsonIgnore] public string ProtocolLabel=>Protocol.ToUpperInvariant();
 [JsonIgnore] public string ActionLabel=>IsMounted?"卸载":"挂载";
 [JsonIgnore] public string StatusLabel=>IsMounted?"已挂载 · 后台运行":"未挂载 · 配置已保存";
 [JsonIgnore] public System.Windows.Media.Brush StatusBrush=>IsMounted?System.Windows.Media.Brushes.MediumSeaGreen:System.Windows.Media.Brushes.Gray;
 [JsonIgnore] public string MountDetails=>$"盘符 {DriveLabel} · 使用全局 rclone 挂载参数";
}
