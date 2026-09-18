using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System.Windows.Threading;

namespace DriverX.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriverX", "logs");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"ui-error-{stamp}.log"), e.Exception.ToString());
        }
        catch { }
        System.Windows.MessageBox.Show("DriverX 遇到错误，但不会退出。详细诊断已保存到本地日志。\n\n" + e.Exception.Message, "DriverX", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
