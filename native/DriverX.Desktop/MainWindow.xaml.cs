using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Reflection;

namespace DriverX.Desktop;
public partial class MainWindow : Window
{
 readonly ObservableCollection<ConnectionProfile> profiles=[]; readonly Dictionary<ConnectionProfile,Process> mounts=[];
 static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
 public MainWindow(){InitializeComponent();LoadTheme();LoadProfiles();ConnectionList.ItemsSource=profiles;RenderProtocols();UpdateStatus();}
 void LoadProfiles(){var user=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX","profiles.json");var imported=Path.Combine(AppContext.BaseDirectory,"import","raidrive_connections.json");var file=File.Exists(user)?user:imported;if(!File.Exists(file))return;try{foreach(var p in JsonSerializer.Deserialize<List<ConnectionProfile>>(File.ReadAllText(file),JsonOptions)??[])profiles.Add(p);}catch(Exception e){MessageBox.Show($"读取连接失败：{e.Message}");}}
 void SaveProfiles(){var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"profiles.json"),JsonSerializer.Serialize(profiles,JsonOptions));}
 static string? RclonePath(){var embedded=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DriverX","bin","rclone.exe");try{if(!File.Exists(embedded)){Directory.CreateDirectory(Path.GetDirectoryName(embedded)!);using var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("DriverX.rclone.exe");if(input is not null){using var output=File.Create(embedded);input.CopyTo(output);}}}catch{}if(File.Exists(embedded))return embedded;var bundled=Path.Combine(AppContext.BaseDirectory,"rclone.exe");if(File.Exists(bundled))return bundled;var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WinGet","Links","rclone.exe");if(File.Exists(p))return p;return Environment.GetEnvironmentVariable("PATH")?.Split(';').Select(x=>Path.Combine(x,"rclone.exe")).FirstOrDefault(File.Exists);}
 void UpdateStatus(){CountText.Text=$"{profiles.Count} 个连接  ·  {mounts.Count} 个挂载";StatusText.Text=RclonePath() is null?"未找到 rclone":"rclone + WinFsp 就绪 · 后台静默运行";StatusDot.Fill=new SolidColorBrush(RclonePath() is null?Color.FromRgb(230,81,74):Color.FromRgb(32,178,107));ConnectionList.Items.Refresh();}
 void SetPage(string title,string sub,UIElement page){PageTitle.Text=title;PageSubtitle.Text=sub;DrivesPage.Visibility=ProtocolsPage.Visibility=SettingsPage.Visibility=InfoPage.Visibility=Visibility.Collapsed;page.Visibility=Visibility.Visible;AddButton.Visibility=page==DrivesPage?Visibility.Visible:Visibility.Collapsed;}
 void ShowDrives(object s,RoutedEventArgs e)=>SetPage("我的磁盘","连接远程存储，像本地磁盘一样使用。",DrivesPage);
 void ShowProtocols(object s,RoutedEventArgs e)=>SetPage("协议中心","从一个界面管理常用远程存储协议。",ProtocolsPage);
 void ShowSettings(object s,RoutedEventArgs e)=>SetPage("设置","调整 DriverX 的外观和运行方式。",SettingsPage);
 void ShowActivity(object s,RoutedEventArgs e){InfoText.Text=$"当前 {mounts.Count} 个后台挂载进程正在运行。\n\n所有 rclone 进程均通过 CreateNoWindow 启动。";SetPage("活动与状态","查看挂载进程和运行状态。",InfoPage);}
 void RenderProtocols(){foreach(var p in new[]{("SFTP","SSH 安全文件传输"),("WebDAV","标准网络文件访问"),("FTP","传统文件传输"),("SMB","Windows 网络共享"),("S3","对象存储"),("OneDrive","Microsoft 云盘"),("Google Drive","Google 云端硬盘"),("Dropbox","Dropbox 云存储")}){var b=new Border{Width=250,Height=100,Margin=new(0,0,16,16),Padding=new(18),CornerRadius=new(12),Background=(Brush)FindResource("CardBackground"),BorderBrush=(Brush)FindResource("BorderBrush"),BorderThickness=new(1)};b.Child=new StackPanel{Children={new TextBlock{Text=p.Item1,FontSize=16,FontWeight=FontWeights.SemiBold},new TextBlock{Text=p.Item2,Margin=new(0,7,0,0),Foreground=(Brush)FindResource("TextSecondary")}}};ProtocolCards.Children.Add(b);}}
 void AddConnection(object s,RoutedEventArgs e){var d=new ConnectionDialog{Owner=this};if(d.ShowDialog()==true){profiles.Add(d.Profile);SaveProfiles();UpdateStatus();}}
 void EditConnection(object s,RoutedEventArgs e){if((s as Button)?.Tag is not ConnectionProfile current||current.IsMounted)return;var d=new ConnectionDialog(current){Owner=this};if(d.ShowDialog()==true){profiles[profiles.IndexOf(current)]=d.Profile;SaveProfiles();UpdateStatus();}}
 void DeleteConnection(object s,RoutedEventArgs e){if((s as Button)?.Tag is not ConnectionProfile p)return;if(p.IsMounted){MessageBox.Show("请先卸载此连接。","DriverX");return;}if(MessageBox.Show($"确定删除“{p.Name}”？","删除连接",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){profiles.Remove(p);SaveProfiles();UpdateStatus();}}
 async void ToggleMount(object s,RoutedEventArgs e){if((s as Button)?.Tag is not ConnectionProfile p)return;if(mounts.TryGetValue(p,out var running)){try{await RunHidden("unmount",$"{p.Drive}:");if(!running.HasExited)running.Kill(true);}catch{}mounts.Remove(p);p.IsMounted=false;UpdateStatus();return;}var r=RclonePath();if(r is null){MessageBox.Show("未找到 rclone，请先安装。");return;}try{var remote="driverx-"+Math.Abs(p.Name.GetHashCode());await RunHidden(BuildConfigArgs(remote,p));var psi=HiddenStart(r,["mount",$"{remote}:{p.Path}",$"{p.Drive}:","--network-mode","--dir-cache-time","2s","--attr-timeout","1s","--poll-interval","0","--vfs-cache-mode","minimal","--buffer-size","4M","--transfers","2","--checkers","2","--vfs-read-chunk-size","8M","--vfs-read-chunk-size-limit","64M"]);mounts[p]=Process.Start(psi)!;p.IsMounted=true;UpdateStatus();}catch(Exception ex){MessageBox.Show($"挂载失败：{ex.Message}");}}
 static string[] BuildConfigArgs(string remote,ConnectionProfile p){var a=new List<string>{"config","create",remote,p.Protocol};if(p.Protocol is "sftp" or "ftp" or "smb"){a.AddRange(["host",p.Host,"user",p.User,"port",p.Port.ToString()]);if(!string.IsNullOrWhiteSpace(p.Password))a.AddRange(["pass",p.Password]);}if(p.Protocol=="sftp"&&!string.IsNullOrWhiteSpace(p.Keyfile))a.AddRange(["key_file",Environment.ExpandEnvironmentVariables(p.Keyfile)]);if(p.Protocol=="webdav")a.AddRange(["url",p.Host,"vendor","other","user",p.User,"pass",p.Password]);return[..a];}
 static ProcessStartInfo HiddenStart(string file,IEnumerable<string> args){var p=new ProcessStartInfo(file){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true,RedirectStandardInput=true};foreach(var a in args)p.ArgumentList.Add(a);return p;}
 static async Task RunHidden(params string[] args){var p=Process.Start(HiddenStart(RclonePath()!,args))!;await p.WaitForExitAsync();if(p.ExitCode!=0)throw new InvalidOperationException(await p.StandardError.ReadToEndAsync());}
 void OpenDrive(object s,RoutedEventArgs e){if((s as Button)?.Tag is ConnectionProfile p)Process.Start(new ProcessStartInfo("explorer.exe",$"{p.Drive}:\\"){UseShellExecute=true});}
 void ApplyTheme(bool dark){Resources["AppBackground"]=Brush(dark?"#CC111318":"#EAF6F7FB");Resources["SidebarBackground"]=Brush(dark?"#D9171A20":"#EAFBFCFE");Resources["CardBackground"]=Brush(dark?"#E61C2028":"#F4FFFFFF");Resources["TextPrimary"]=Brush(dark?"#F4F7FC":"#172033");Resources["TextSecondary"]=Brush(dark?"#9BA6B7":"#687386");Resources["BorderBrush"]=Brush(dark?"#303641":"#E3E7EF");Resources["AccentSoft"]=Brush(dark?"#153A62":"#E7F2FF");}static Brush Brush(string c)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(c));
 void SaveTheme(string theme){var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"native-settings.json"),JsonSerializer.Serialize(new{theme}));}
 void LoadTheme(){var theme="system";try{var f=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX","native-settings.json");if(File.Exists(f))theme=JsonDocument.Parse(File.ReadAllText(f)).RootElement.GetProperty("theme").GetString()??"system";}catch{}ApplyTheme(theme=="dark"||(theme=="system"&&SystemIsDark()));}
 static bool SystemIsDark()=>Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0;
 void UseLightTheme(object s,RoutedEventArgs e){ApplyTheme(false);SaveTheme("light");}void UseDarkTheme(object s,RoutedEventArgs e){ApplyTheme(true);SaveTheme("dark");}void UseSystemTheme(object s,RoutedEventArgs e){ApplyTheme(SystemIsDark());SaveTheme("system");}
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 void WindowSourceInitialized(object? s,EventArgs e){var hwnd=new WindowInteropHelper(this).Handle;var rounded=2;DwmSetWindowAttribute(hwnd,33,ref rounded,sizeof(int));var mica=2;DwmSetWindowAttribute(hwnd,38,ref mica,sizeof(int));}
}
public class ConnectionProfile
{
 public string Name{get;set;}="新连接";public string Protocol{get;set;}="sftp";public string Host{get;set;}="";public int Port{get;set;}=22;public string User{get;set;}="";public string Password{get;set;}="";public string Keyfile{get;set;}="";public string Path{get;set;}="/";public string Drive{get;set;}="Z";public bool IsMounted{get;set;}
 public string DriveLabel=>Drive.TrimEnd(':').ToUpperInvariant()+":";public string Endpoint=>string.IsNullOrWhiteSpace(User)?Host:$"{User}@{Host}:{Port}";public string ProtocolLabel=>Protocol.ToUpperInvariant();public string ActionLabel=>IsMounted?"卸载":"挂载";
}
