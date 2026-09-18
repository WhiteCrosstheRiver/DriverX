using RadioButton = System.Windows.Controls.RadioButton;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace DriverX.Desktop;

public partial class MainWindow
{
    string selectedTheme = "system";
    string selectedFont = "fluent";
    double selectedSize = 14;
    string selectedLanguage = "zh-CN";
    string dirCacheTime = "2s";
    string attrTimeout = "1s";
    string vfsCacheMode = "minimal";
    string bufferSize = "4M";
    string transfers = "2";
    string readChunkSize = "8M";
    bool appearanceReady;
    static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DriverX", "native-settings.json");
    static readonly string[] PaletteKeys = ["AppBackground", "SidebarBackground", "CardBackground", "ControlBackground", "TextPrimary", "TextSecondary", "BorderBrush", "Accent", "AccentSoft", "OnAccent", "Danger", "FocusBrush", "HoverOverlay"];

    void ApplyAppearance()
    {
        bool contrast = selectedTheme == "github-contrast";
        bool dark = contrast || selectedTheme == "dark" || (selectedTheme == "system" && SystemIsDark());
        // Opaque neutral layers avoid the washed-out gray caused by alpha over the WPF window.
        string[] colors = contrast
            ? ["#010409", "#010409", "#0D1117", "#161B22", "#FFFFFF", "#D9E2EC", "#8B949E", "#71B7FF", "#10243A", "#010409", "#FF9492", "#FFFFFF", "#18FFFFFF"]
            : dark
            ? ["#202020", "#202020", "#2B2B2B", "#333333", "#FFFFFF", "#C5C5C5", "#505050", "#99D6FF", "#30404D", "#001B2E", "#FFB4AB", "#FFFFFF", "#0FFFFFFF"]
            : ["#F3F3F3", "#F3F3F3", "#FFFFFF", "#FAFAFA", "#1A1A1A", "#5C5C5C", "#C4C4C4", "#005FB8", "#E5F0FA", "#FFFFFF", "#B42332", "#171717", "#08000000"];
        for (int i = 0; i < PaletteKeys.Length; i++)
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colors[i]));
            brush.Freeze();
            System.Windows.Application.Current.Resources[PaletteKeys[i]] = brush;
        }
        var resources = System.Windows.Application.Current.Resources;
        resources["BodyFont"] = new System.Windows.Media.FontFamily(selectedFont == "classic"
            ? "Segoe UI, Microsoft YaHei UI, Microsoft YaHei"
            : "Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, Microsoft YaHei");
        resources["HeadingFont"] = new System.Windows.Media.FontFamily(selectedFont == "classic"
            ? "Segoe UI, Microsoft YaHei UI, Microsoft YaHei"
            : "Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI, Microsoft YaHei");
        resources["BodySize"] = selectedSize;
        resources["CaptionSize"] = selectedSize - 2;
        resources["CardTitleSize"] = selectedSize + 4;
        resources["SubtitleSize"] = selectedSize + 6;
        resources["TitleSize"] = selectedSize + 14;
        LightChoice.IsChecked = selectedTheme == "light";
        DarkChoice.IsChecked = selectedTheme == "dark";
        ContrastChoice.IsChecked = contrast;
        SystemChoice.IsChecked = selectedTheme == "system";
        FluentFontChoice.IsChecked = selectedFont == "fluent";
        ClassicFontChoice.IsChecked = selectedFont == "classic";
        StandardSizeChoice.IsChecked = selectedSize == 14;
        LargeSizeChoice.IsChecked = selectedSize == 16;
        LanguageChoice.SelectedValue = selectedLanguage;
        DirCacheChoice.SelectedValue = dirCacheTime;
        AttrTimeoutChoice.SelectedValue = attrTimeout;
        VfsCacheChoice.SelectedValue = vfsCacheMode;
        BufferSizeChoice.SelectedValue = bufferSize;
        TransfersChoice.SelectedValue = transfers;
        ReadChunkChoice.SelectedValue = readChunkSize;
        ApplyLanguage();
        UpdateMountSettingsSummary();
        UpdateWindowTheme();
    }

    void UpdateWindowTheme()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int dark = selectedTheme is "dark" or "github-contrast" || (selectedTheme == "system" && SystemIsDark()) ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        // Solid layers give deterministic contrast and avoid an extra translucent composition pass.
        int backdrop = 1;
        DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
    }

    void SaveAppearance()
    {
        try
        {
            var settings = File.Exists(SettingsPath) ? JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject() : new JsonObject();
            settings["theme"] = selectedTheme;
            settings["font"] = selectedFont;
            settings["textSize"] = selectedSize;
            settings["language"] = selectedLanguage;
            settings["dirCacheTime"] = dirCacheTime;
            settings["attrTimeout"] = attrTimeout;
            settings["vfsCacheMode"] = vfsCacheMode;
            settings["bufferSize"] = bufferSize;
            settings["transfers"] = transfers;
            settings["readChunkSize"] = readChunkSize;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { StatusText.Text = "外观已应用，设置保存失败：" + ex.Message; }
    }

    void LoadTheme()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("theme", out var theme) && theme.GetString() is "light" or "dark" or "system" or "github-contrast") selectedTheme = theme.GetString()!;
                if (root.TryGetProperty("font", out var font) && font.GetString() is "fluent" or "classic") selectedFont = font.GetString()!;
                if (root.TryGetProperty("textSize", out var size) && size.TryGetDouble(out var value) && value is 14 or 16) selectedSize = value;
                if (root.TryGetProperty("language", out var language) && language.GetString() is "zh-CN" or "ja-JP" or "en-US" or "fr-FR") selectedLanguage = language.GetString()!;
                if (root.TryGetProperty("dirCacheTime", out var dirCache) && dirCache.GetString() is "1s" or "2s" or "5s" or "10s") dirCacheTime = dirCache.GetString()!;
                if (root.TryGetProperty("attrTimeout", out var attr) && attr.GetString() is "0s" or "1s" or "2s" or "5s") attrTimeout = attr.GetString()!;
                if (root.TryGetProperty("vfsCacheMode", out var vfs) && vfs.GetString() is "minimal" or "writes" or "full") vfsCacheMode = vfs.GetString()!;
                if (root.TryGetProperty("bufferSize", out var buffer) && buffer.GetString() is "0" or "4M" or "8M" or "16M") bufferSize = buffer.GetString()!;
                if (root.TryGetProperty("transfers", out var transfer) && transfer.GetString() is "1" or "2" or "4") transfers = transfer.GetString()!;
                if (root.TryGetProperty("readChunkSize", out var chunk) && chunk.GetString() is "4M" or "8M" or "16M" or "32M") readChunkSize = chunk.GetString()!;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException) { }
        ApplyAppearance();
        appearanceReady = true;
    }
    static bool SystemIsDark() => Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1)) == 0;
    void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (selectedTheme == "system" && !Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke(new Action(() => { if (!shuttingDown) ApplyAppearance(); }));
    }
    void ChooseTheme(object sender, RoutedEventArgs e) { if (!appearanceReady) return; selectedTheme = (string)((RadioButton)sender).Tag; ApplyAppearance(); SaveAppearance(); }
    void ChooseFont(object sender, RoutedEventArgs e) { if (!appearanceReady) return; selectedFont = (string)((RadioButton)sender).Tag; ApplyAppearance(); SaveAppearance(); }
    void ChooseTextSize(object sender, RoutedEventArgs e) { if (!appearanceReady) return; selectedSize = (string)((RadioButton)sender).Tag == "16" ? 16 : 14; ApplyAppearance(); SaveAppearance(); }
    void ChooseLanguage(object sender, SelectionChangedEventArgs e)
    {
        if (!appearanceReady || LanguageChoice.SelectedItem is not ComboBoxItem item || item.Tag is not string language) return;
        selectedLanguage = language;
        ApplyLanguage();
        SaveAppearance();
    }

    void MountSettingsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!appearanceReady) return;
        dirCacheTime = DirCacheChoice.SelectedValue as string ?? "2s";
        attrTimeout = AttrTimeoutChoice.SelectedValue as string ?? "1s";
        vfsCacheMode = VfsCacheChoice.SelectedValue as string ?? "minimal";
        bufferSize = BufferSizeChoice.SelectedValue as string ?? "4M";
        transfers = TransfersChoice.SelectedValue as string ?? "2";
        readChunkSize = ReadChunkChoice.SelectedValue as string ?? "8M";
        UpdateMountSettingsSummary();
        SaveAppearance();
    }

    void ResetMountDefaults(object sender, RoutedEventArgs e)
    {
        dirCacheTime = "2s"; attrTimeout = "1s"; vfsCacheMode = "minimal";
        bufferSize = "4M"; transfers = "2"; readChunkSize = "8M";
        DirCacheChoice.SelectedValue = dirCacheTime; AttrTimeoutChoice.SelectedValue = attrTimeout;
        VfsCacheChoice.SelectedValue = vfsCacheMode; BufferSizeChoice.SelectedValue = bufferSize;
        TransfersChoice.SelectedValue = transfers; ReadChunkChoice.SelectedValue = readChunkSize;
        UpdateMountSettingsSummary();
        SaveAppearance();
    }

    void UpdateMountSettingsSummary()
    {
        if (MountDefaultsStatus is null) return;
        var defaults = dirCacheTime == "2s" && attrTimeout == "1s" && vfsCacheMode == "minimal" && bufferSize == "4M" && transfers == "2" && readChunkSize == "8M";
        MountDefaultsStatus.Text = (defaults ? "✓ 正在使用推荐默认值：" : "当前自定义：") + $"目录 {dirCacheTime} · 属性 {attrTimeout} · {vfsCacheMode} · 缓冲 {bufferSize} · 并发 {transfers} · 分块 {readChunkSize}";
        if (DriveLatencyText is not null) DriveLatencyText.Text = $"目录 {dirCacheTime} · 属性 {attrTimeout}";
        ConnectionList?.Items.Refresh();
    }

    void ApplyLanguage()
    {
        var language = selectedLanguage switch
        {
            "ja-JP" => new[] { "マイドライブ", "プロトコルセンター", "管理", "アクティビティと状態", "設定", "リモートワークスペース", "リモートストレージをローカルディスクのように接続します。", "＋ 接続を追加", "外観とレイアウト", "テーマと文字設定は個別に変更できます。", "インターフェース言語", "DriverX の表示言語を選択します。" },
            "en-US" => new[] { "My drives", "Protocol center", "Manage", "Activity & status", "Settings", "Remote workspace", "Connect remote storage like a local disk.", "＋ Add connection", "Appearance & typography", "Theme and text settings apply immediately.", "Interface language", "Choose the display language for DriverX." },
            "fr-FR" => new[] { "Mes disques", "Centre des protocoles", "Gestion", "Activité et état", "Paramètres", "Espace de travail distant", "Connectez un stockage distant comme un disque local.", "＋ Ajouter une connexion", "Apparence et typographie", "Le thème et le texte s’appliquent immédiatement.", "Langue de l’interface", "Choisissez la langue d’affichage de DriverX." },
            _ => new[] { "我的磁盘", "协议中心", "管理", "活动与状态", "设置", "远程工作空间", "连接远程存储，像本地磁盘一样使用。", "＋ 添加连接", "外观与排版", "主题和文字独立设置，切换后立即生效。", "界面语言", "选择 DriverX 的显示语言。" }
        };
        DrivesNav.Content = "▣   " + language[0]; ProtocolsNav.Content = "◈   " + language[1]; ManageLabel.Text = language[2]; ActivityNav.Content = "⌁   " + language[3]; SettingsNav.Content = "⚙   " + language[4];
        if (PageTitle != null && DrivesPage.Visibility == Visibility.Visible) { PageTitle.Text = language[0]; PageSubtitle.Text = language[6]; }
        if (AddButton != null) AddButton.Content = language[7];
        if (LanguageLabel != null) { LanguageLabel.Text = language[10]; LanguageHint.Text = language[11]; }
        var appearanceHeading = FindName("AppearanceHeading") as TextBlock; if (appearanceHeading != null) appearanceHeading.Text = language[8];
        var appearanceHint = FindName("AppearanceHint") as TextBlock; if (appearanceHint != null) appearanceHint.Text = language[9];
    }
}
