using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Button=System.Windows.Controls.Button;
using MenuItem=System.Windows.Controls.MenuItem;
using ContextMenu=System.Windows.Controls.ContextMenu;

namespace DriverX.Desktop;
public partial class MainWindow
{
    void PowerExit(object s,RoutedEventArgs e){shuttingDown=true;Close();}
    void PrimaryDriveAction(object s,RoutedEventArgs e)
    {
        if(s is Button {Tag:ConnectionProfile p}){if(p.IsMounted)OpenDrive(s,e);else ToggleMount(s,e);}
    }
    void DriveMenu(object s,RoutedEventArgs e)
    {
        if(s is not Button {Tag:ConnectionProfile p} button)return;
        var menu=new ContextMenu();
        void Item(string label,RoutedEventHandler handler,bool enabled=true){var item=new MenuItem{Header=label,IsEnabled=enabled};item.Click+=(_,args)=>handler(new Button{Tag=p},args);menu.Items.Add(item);}
        Item(p.ActionLabel,ToggleMount);Item(UiText.T("打开"),OpenDrive,p.IsMounted);Item(UiText.T("重置磁盘"),ResetMount);
        menu.Items.Add(new Separator());Item(UiText.T("编辑"),EditConnection,!p.IsMounted);Item(UiText.T("删除"),DeleteConnection,!p.IsMounted);
        menu.PlacementTarget=button;menu.IsOpen=true;
    }
    DataTemplate CreateCompactRow()
    {
        var template=(DataTemplate)XamlReader.Parse("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
         <Border Background="{DynamicResource CardBackground}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="0,0,0,1" Padding="12,0" MinHeight="48" ToolTip="{Binding Path}">
          <Grid VerticalAlignment="Center"><Grid.ColumnDefinitions><ColumnDefinition Width="58"/><ColumnDefinition Width="1.1*"/><ColumnDefinition Width="130"/><ColumnDefinition Width="1.6*"/><ColumnDefinition Width="90"/><ColumnDefinition Width="120"/></Grid.ColumnDefinitions>
           <TextBlock Text="{Binding DriveLabel}" Foreground="{DynamicResource Accent}" FontWeight="SemiBold" VerticalAlignment="Center"/>
           <TextBlock Grid.Column="1" Text="{Binding Name}" TextTrimming="CharacterEllipsis" Margin="0,0,12,0" VerticalAlignment="Center"/>
           <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center"><Ellipse Width="7" Height="7" Fill="{Binding StatusBrush}" Margin="0,0,7,0"/><TextBlock Text="{Binding StatusLabel}" ToolTip="{Binding ConnectionError}"/></StackPanel>
           <TextBlock Grid.Column="3" Text="{Binding Endpoint}" Foreground="{DynamicResource TextSecondary}" TextTrimming="CharacterEllipsis" Margin="0,0,14,0" VerticalAlignment="Center"/>
           <TextBlock Grid.Column="4" Text="{Binding ProtocolLabel}" Foreground="{DynamicResource TextSecondary}" VerticalAlignment="Center"/>
           <StackPanel Grid.Column="5" Orientation="Horizontal" HorizontalAlignment="Right"><Button Name="PrimaryCompact" Tag="{Binding}" Content="{Binding PrimaryActionLabel}" BorderThickness="0" Padding="10,5"/><Button Name="ResetCompact" Tag="{Binding}" Content="重置" ToolTip="重置磁盘" BorderThickness="0" Padding="8,5"/><Button Name="MoreCompact" Tag="{Binding}" Content="⋯" BorderThickness="0" Padding="10,5"/></StackPanel>
          </Grid>
         </Border>
        </DataTemplate>
        """);return template;
    }
    void CompactClick(object sender,RoutedEventArgs e)
    {
        if(e.OriginalSource is not Button b)return;
        if(b.Name=="PrimaryCompact"){PrimaryDriveAction(b,e);e.Handled=true;}
        if(b.Name=="ResetCompact"){ResetMount(b,e);e.Handled=true;}
        if(b.Name=="MoreCompact"){DriveMenu(b,e);e.Handled=true;}
    }
}

