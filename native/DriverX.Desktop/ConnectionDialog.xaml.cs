using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace DriverX.Desktop;

public partial class ConnectionDialog : Window
{
    public ConnectionProfile Profile { get; private set; } = new();
    readonly HashSet<char> reservedDrives;
    bool passwordVisible;

    public ConnectionDialog(ConnectionProfile? source = null, IEnumerable<string>? configuredDrives = null)
    {
        InitializeComponent();
        DriveIcon.ItemsSource = DriveAppearance.Options;
        DriveIcon.SelectedValue = source?.DriveIconId ?? -1;
        reservedDrives = GetReservedDrives(configuredDrives);
        if (source is not null)
        {
            ProfileName.Text = source.Name;
            Host.Text = source.Host;
            Port.Text = source.Port.ToString();
            User.Text = source.User;
            Password.Password = source.Password;
            Keyfile.Text = source.Keyfile;
            RemotePath.Text = source.Path;
            Drive.Text = source.Drive;
            foreach (ComboBoxItem item in Protocol.Items)
                if ((item.Tag as string) == source.Protocol) { Protocol.SelectedItem = item; break; }
        }
        else Drive.Text = FindAvailableDrive();
        UpdateProtocolFields();
    }

    string SelectedProtocol => (Protocol.SelectedItem as ComboBoxItem)?.Tag as string ?? "sftp";

    void ProtocolChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized) UpdateProtocolFields();
    }

    void UpdateProtocolFields()
    {
        var protocol = SelectedProtocol;
        var isSftp = protocol == "sftp";
        var isFtp = protocol == "ftp";
        var isWebDav = protocol == "webdav";
        var isSmb = protocol == "smb";
        var isS3 = protocol == "s3";
        var isCloud = protocol is "onedrive" or "drive" or "dropbox";

        SetVisible(HostLabel, Host, !isCloud);
        SetVisible(PortLabel, Port, isSftp || isFtp);
        SetVisible(UserLabel, User, !isCloud);
        SetVisible(PasswordLabel, Password, !isCloud);
        SetVisible(KeyfileLabel, Keyfile, isSftp);

        HostLabel.Text = isWebDav ? "服务器 URL *" : isS3 ? "S3 端点 *" : isSmb ? "服务器 / 共享 *" : "主机 *";
        UserLabel.Text = isS3 ? "Access Key" : "用户名";
        PasswordLabel.Text = isS3 ? "Secret Key" : "密码（可选）";
        PathLabel.Text = isS3 ? "存储桶 / 路径" : isCloud ? "远程目录（可选）" : "远程目录";
        Port.Text = isFtp ? "21" : isSftp ? "22" : Port.Text;
        ProtocolHint.Text = isSftp ? "密码和私钥任选其一即可。" :
            isWebDav ? "URL、用户名和密码由 WebDAV 服务商提供。" :
            isFtp ? "匿名 FTP 可以不填写用户名和密码。" :
            isSmb ? "填写服务器地址；共享名可放在远程目录中。" :
            isS3 ? "填写端点与密钥，存储桶写在远程路径中。" :
            "云盘授权将在首次连接时完成，只需设置名称、目录和盘符。";
        DialogSubtitle.Text = isCloud ? "此协议无需手动填写服务器和账号。" : "只显示当前协议需要的信息。";
    }

    static void SetVisible(UIElement label, UIElement input, bool visible)
    {
        label.Visibility = input.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    static HashSet<char> GetReservedDrives(IEnumerable<string>? configuredDrives)
    {
        var drives = new HashSet<char>();
        foreach (var root in Environment.GetLogicalDrives())
            if (root.Length > 0) drives.Add(char.ToUpperInvariant(root[0]));
        foreach (var drive in configuredDrives ?? [])
            if (!string.IsNullOrWhiteSpace(drive)) drives.Add(char.ToUpperInvariant(drive.Trim()[0]));
        return drives;
    }

    string FindAvailableDrive()
    {
        for (var letter = 'Z'; letter >= 'D'; letter--)
            if (!reservedDrives.Contains(letter)) return letter.ToString();
        return string.Empty;
    }

    void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!passwordVisible) VisiblePassword.Text = Password.Password;
    }

    void TogglePasswordVisibility(object sender, RoutedEventArgs e)
    {
        passwordVisible = !passwordVisible;
        if (passwordVisible)
        {
            VisiblePassword.Text = Password.Password;
            Password.Visibility = Visibility.Collapsed;
            VisiblePassword.Visibility = Visibility.Visible;
            VisiblePassword.Focus();
            VisiblePassword.CaretIndex = VisiblePassword.Text.Length;
            PasswordRevealButton.ToolTip = "隐藏密码";
        }
        else
        {
            Password.Password = VisiblePassword.Text;
            VisiblePassword.Visibility = Visibility.Collapsed;
            Password.Visibility = Visibility.Visible;
            Password.Focus();
            PasswordRevealButton.ToolTip = "显示密码";
        }
    }

    void Cancel(object sender, RoutedEventArgs e) => DialogResult = false;

    void Save(object sender, RoutedEventArgs e)
    {
        var protocol = SelectedProtocol;
        if (string.IsNullOrWhiteSpace(ProfileName.Text) || string.IsNullOrWhiteSpace(Drive.Text))
        {
            MessageBox.Show("请填写名称和盘符。");
            return;
        }
        if (protocol is not ("onedrive" or "drive" or "dropbox") && string.IsNullOrWhiteSpace(Host.Text))
        {
            MessageBox.Show("请填写服务器地址。");
            return;
        }
        if (Drive.Text.Trim().Length != 1 || !char.IsLetter(Drive.Text.Trim()[0]))
        {
            MessageBox.Show("本地盘符必须是一个英文字母。");
            return;
        }
        var driveLetter = char.ToUpperInvariant(Drive.Text.Trim()[0]);
        if (reservedDrives.Contains(driveLetter))
        {
            MessageBox.Show($"盘符 {driveLetter}: 已被本机磁盘、网络磁盘或其他 DriverX 连接占用，请选择其他盘符。", "盘符不可用", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Profile = new ConnectionProfile
        {
            Protocol = protocol,
            DriveIconId = DriveIcon.SelectedValue is int iconId ? iconId : -1,
            Name = ProfileName.Text.Trim(),
            Host = Host.Text.Trim(),
            Port = int.TryParse(Port.Text, out var port) ? port : protocol == "ftp" ? 21 : 22,
            User = User.Text.Trim(),
            Password = passwordVisible ? VisiblePassword.Text : Password.Password,
            Keyfile = Keyfile.Text.Trim(),
            Path = string.IsNullOrWhiteSpace(RemotePath.Text) ? "/" : RemotePath.Text.Trim(),
            Drive = Drive.Text.Trim().ToUpperInvariant()
        };
        DialogResult = true;
    }
}
