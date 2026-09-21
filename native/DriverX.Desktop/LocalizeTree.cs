using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace DriverX.Desktop;
internal static class LocalizeTree
{
 public static void Apply(DependencyObject root)
 {
  if(root is TextBlock t && !BindingOperations.IsDataBound(t,TextBlock.TextProperty)) t.SetCurrentValue(TextBlock.TextProperty,UiText.T(t.Text));
  if(root is ContentControl c && c.Content is string s) c.SetCurrentValue(ContentControl.ContentProperty,UiText.T(s));
  if(root is FrameworkElement f && f.ToolTip is string tip) f.ToolTip=UiText.T(tip);
  foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) Apply(child);
 }
}
