using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Control=System.Windows.Controls.Control;
using Brush=System.Windows.Media.Brush;
using System.ComponentModel;
using Binding=System.Windows.Data.Binding;
using Orientation=System.Windows.Controls.Orientation;
using Button=System.Windows.Controls.Button;
using CheckBox=System.Windows.Controls.CheckBox;
using ComboBox=System.Windows.Controls.ComboBox;

namespace DriverX.Desktop;
public partial class MainWindow
{
    sealed class DriveViewSettings { public bool MountedFirst {get;set;}=true; public int Layout {get;set;} public int Density {get;set;}=2; }
    DriveViewSettings driveView=new();
    DataTemplate? cardTemplate;
    Grid? tableHeader;
    CheckBox? mountedFirstChoice;
    static string DriveViewPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX","drive-view.json");

    void InitializeDriveView()
    {
        try { driveView=JsonSerializer.Deserialize<DriveViewSettings>(File.ReadAllText(DriveViewPath))??new(); }catch{}
        driveView.Layout=Math.Clamp(driveView.Layout,0,1);driveView.Density=Math.Clamp(driveView.Density,0,4);
        cardTemplate=ConnectionList.ItemTemplate;
        ConnectionList.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,new RoutedEventHandler(CompactClick));
        ((UIElement)DrivesPage.Children[0]).Visibility=Visibility.Collapsed;
        InitializeDriveInteraction();
        var scroll=(ScrollViewer)ConnectionList.Parent;
        var grid=(Grid)scroll.Parent;
        grid.Children.Remove(scroll);
        var dock=new DockPanel();Grid.SetRow(dock,1);grid.Children.Add(dock);
        var toolbar=new DockPanel{Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(toolbar,Dock.Top);dock.Children.Add(toolbar);
        tableHeader=new Grid{Margin=new Thickness(12,0,12,8)};foreach(var width in new[]{new GridLength(58),new GridLength(1.1,GridUnitType.Star),new GridLength(130),new GridLength(1.6,GridUnitType.Star),new GridLength(90),new GridLength(120)})tableHeader.ColumnDefinitions.Add(new ColumnDefinition{Width=width});
        var labels=new[]{"磁盘","名称","状态","服务器","协议","操作"};for(var c=0;c<labels.Length;c++){var title=new TextBlock{Text=UiText.T(labels[c])};title.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");Grid.SetColumn(title,c);tableHeader.Children.Add(title);}DockPanel.SetDock(tableHeader,Dock.Top);dock.Children.Add(tableHeader);dock.Children.Add(scroll);
        var density=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=System.Windows.HorizontalAlignment.Right};DockPanel.SetDock(density,Dock.Right);toolbar.Children.Add(density);
        var layout=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,10,0)};toolbar.Children.Add(layout);
        var layoutButtons=new[]{"卡片","列表"};
        for(var i=0;i<layoutButtons.Length;i++){var index=i;var button=SegmentButton(layoutButtons[i],driveView.Layout==i);button.Click+=(_,_)=>{driveView.Layout=index;ApplyDriveView();SaveDriveView();RefreshSegments(layout,density);};layout.Children.Add(button);}
        var densityButtons=new[]{"巨大","大","中","小","迷你"};
        for(var i=0;i<densityButtons.Length;i++){var index=i;var button=SegmentButton(densityButtons[i],driveView.Density==index);button.Click+=(_,_)=>{driveView.Density=index;ApplyDriveView();SaveDriveView();RefreshSegments(layout,density);};density.Children.Add(button);}
        mountedFirstChoice=new CheckBox{Content="已挂载磁盘优先显示",IsChecked=driveView.MountedFirst,Margin=new Thickness(0,0,0,18)};
        ((StackPanel)SettingsPage.Content).Children.Insert(0,mountedFirstChoice);
        mountedFirstChoice.Click+=(_,_)=>{driveView.MountedFirst=mountedFirstChoice.IsChecked==true;ApplyDriveView();SaveDriveView();};
        ApplyDriveView(); LocalizeTree.Apply(this);
    }
    Button SegmentButton(string text,bool selected){var b=new Button{Content=UiText.T(text),Padding=new Thickness(12,6,12,6),Margin=new Thickness(2,0,0,0),BorderThickness=new Thickness(0)};SetSegment(b,selected);return b;}
    void SetSegment(Button button,bool selected){button.SetResourceReference(Control.BackgroundProperty,selected?"AccentSoft":"AppBackground");button.SetResourceReference(Control.ForegroundProperty,selected?"Accent":"TextSecondary");}
    void RefreshSegments(StackPanel layout,StackPanel density){for(var i=0;i<layout.Children.Count;i++)SetSegment((Button)layout.Children[i],i==driveView.Layout);for(var i=0;i<density.Children.Count;i++)SetSegment((Button)density.Children[i],i==driveView.Density);}    void SaveDriveView(){try{Directory.CreateDirectory(Path.GetDirectoryName(DriveViewPath)!);File.WriteAllText(DriveViewPath,JsonSerializer.Serialize(driveView));}catch(Exception ex){System.Windows.MessageBox.Show($"保存视图设置失败：{ex.Message}");}}
    void ApplyDriveView()
    {
        Resources["DriveCardWidth"]=new double[]{540,470,420,350,320}[driveView.Density];
        Resources["DriveCardPadding"]=new Thickness(new double[]{28,24,20,14,8}[driveView.Density]);
        Resources["DriveDetailVisibility"]=driveView.Density==4?Visibility.Collapsed:Visibility.Visible;
        var panel=new FrameworkElementFactory(driveView.Layout==0&&driveView.Density!=4?typeof(WrapPanel):typeof(StackPanel));
        ConnectionList.ItemsPanel=new ItemsPanelTemplate(panel);
        ConnectionList.ItemTemplate=driveView.Layout==0&&driveView.Density!=4?cardTemplate:CreateCompactRow();
        if(tableHeader is not null)tableHeader.Visibility=driveView.Layout==1||driveView.Density==4?Visibility.Visible:Visibility.Collapsed;
        SortDriveView();
    }
    void SortDriveView()
    {
        var view=CollectionViewSource.GetDefaultView(ConnectionList.ItemsSource);if(view is null)return;
        using(view.DeferRefresh()){view.SortDescriptions.Clear();if(driveView.MountedFirst)view.SortDescriptions.Add(new SortDescription(nameof(ConnectionProfile.IsMounted),ListSortDirection.Descending));}
    }
    DataTemplate CreateDriveRow()
    {
        var border=new FrameworkElementFactory(typeof(Border));border.SetResourceReference(Border.BackgroundProperty,"CardBackground");border.SetResourceReference(Border.BorderBrushProperty,"BorderBrush");border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetValue(Border.MarginProperty,new Thickness(0,0,12,8));border.SetValue(Border.PaddingProperty,new Thickness(new double[]{28,24,20,14,8}[driveView.Density]));
        var dock=new FrameworkElementFactory(typeof(DockPanel));border.AppendChild(dock);
        var actions=new FrameworkElementFactory(typeof(StackPanel));actions.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);actions.SetValue(DockPanel.DockProperty,Dock.Right);dock.AppendChild(actions);
        void Action(string caption,RoutedEventHandler handler,bool primary=false){var b=new FrameworkElementFactory(typeof(Button));b.SetBinding(FrameworkElement.TagProperty,new Binding());if(primary)b.SetBinding(Button.ContentProperty,new Binding("ActionLabel"));else b.SetValue(Button.ContentProperty,caption);b.SetValue(Button.PaddingProperty,new Thickness(10,6,10,6));b.SetValue(Button.MarginProperty,new Thickness(4,0,0,0));if(primary)b.SetResourceReference(Button.StyleProperty,"PrimaryButton");b.AddHandler(Button.ClickEvent,handler);actions.AppendChild(b);}
        Action("",ToggleMount,true);Action("打开",OpenDrive);Action("编辑",EditConnection);Action("删除",DeleteConnection);
        var letter=new FrameworkElementFactory(typeof(TextBlock));letter.SetBinding(TextBlock.TextProperty,new Binding("DriveLabel"));letter.SetValue(TextBlock.WidthProperty,52d);letter.SetValue(TextBlock.FontWeightProperty,FontWeights.Bold);letter.SetResourceReference(TextBlock.ForegroundProperty,"Accent");letter.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);dock.AppendChild(letter);
        var info=new FrameworkElementFactory(typeof(StackPanel));dock.AppendChild(info);
        void Text(string binding,bool title=false){var t=new FrameworkElementFactory(typeof(TextBlock));t.SetBinding(TextBlock.TextProperty,new Binding(binding));t.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);t.SetValue(TextBlock.FontSizeProperty,title?new double[]{24,21,18,15,14}[driveView.Density]:12d);t.SetResourceReference(TextBlock.ForegroundProperty,title?"TextPrimary":"TextSecondary");info.AppendChild(t);}
        Text("Name",true);var status=new FrameworkElementFactory(typeof(TextBlock));status.SetBinding(TextBlock.TextProperty,new Binding("StatusLabel"));status.SetBinding(TextBlock.ForegroundProperty,new Binding("StatusBrush"));info.AppendChild(status);if(driveView.Density<4)Text("Endpoint");if(driveView.Density<2)Text("Path");
        return new DataTemplate{VisualTree=border};
    }
}



