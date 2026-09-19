using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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
    CheckBox? mountedFirstChoice;
    static string DriveViewPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DriverX","drive-view.json");

    void InitializeDriveView()
    {
        try { driveView=JsonSerializer.Deserialize<DriveViewSettings>(File.ReadAllText(DriveViewPath))??new(); }catch{}
        driveView.Layout=Math.Clamp(driveView.Layout,0,1);driveView.Density=driveView.Density switch{1=>1,3=>3,_=>2};
        cardTemplate=ConnectionList.ItemTemplate;
        var scroll=(ScrollViewer)ConnectionList.Parent;
        var grid=(Grid)scroll.Parent;
        grid.Children.Remove(scroll);
        var dock=new DockPanel();Grid.SetRow(dock,1);grid.Children.Add(dock);
        var toolbar=new DockPanel{Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(toolbar,Dock.Top);dock.Children.Add(toolbar);dock.Children.Add(scroll);
        var density=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=System.Windows.HorizontalAlignment.Right};DockPanel.SetDock(density,Dock.Right);toolbar.Children.Add(density);
        var layout=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,10,0)};toolbar.Children.Add(layout);
        var layoutButtons=new[]{"▦  卡片","☰  列表"};
        for(var i=0;i<layoutButtons.Length;i++){var index=i;var button=SegmentButton(layoutButtons[i],driveView.Layout==i);button.Click+=(_,_)=>{driveView.Layout=index;ApplyDriveView();SaveDriveView();RefreshSegments(layout,density);};layout.Children.Add(button);}
        var densityButtons=new[]{"紧凑","标准","舒适"};
        for(var i=0;i<densityButtons.Length;i++){var index=i;var button=SegmentButton(densityButtons[i],driveView.Density==index+1);button.Click+=(_,_)=>{driveView.Density=index+1;ApplyDriveView();SaveDriveView();RefreshSegments(layout,density);};density.Children.Add(button);}
        mountedFirstChoice=new CheckBox{Content="已挂载磁盘优先显示",IsChecked=driveView.MountedFirst,Margin=new Thickness(0,0,0,18)};
        var settingsScroll=(ScrollViewer)SettingsPage.Content;((StackPanel)settingsScroll.Content).Children.Insert(0,mountedFirstChoice);
        mountedFirstChoice.Click+=(_,_)=>{driveView.MountedFirst=mountedFirstChoice.IsChecked==true;ApplyDriveView();SaveDriveView();};
        ApplyDriveView();
    }
    Button SegmentButton(string text,bool selected)=>new(){Content=text,Padding=new Thickness(12,6,12,6),Margin=new Thickness(2,0,0,0),BorderThickness=new Thickness(0),Background=(Brush)new BrushConverter().ConvertFromString(selected?"#DCEEFF":"Transparent")!,Foreground=(Brush)new BrushConverter().ConvertFromString(selected?"#005FB8":"#5C5C5C")!};
    void RefreshSegments(StackPanel layout,StackPanel density){foreach(var button in layout.Children.OfType<Button>()){var selected=(button.Content?.ToString()?.Contains("卡片")==true)==(driveView.Layout==0);button.Background=(Brush)new BrushConverter().ConvertFromString(selected?"#DCEEFF":"Transparent")!;button.Foreground=(Brush)new BrushConverter().ConvertFromString(selected?"#005FB8":"#5C5C5C")!;}foreach(var button in density.Children.OfType<Button>()){var selected=button.Content?.ToString()==(driveView.Density==1?"紧凑":driveView.Density==2?"标准":"舒适");button.Background=(Brush)new BrushConverter().ConvertFromString(selected?"#DCEEFF":"Transparent")!;button.Foreground=(Brush)new BrushConverter().ConvertFromString(selected?"#005FB8":"#5C5C5C")!;}}
    void SaveDriveView(){try{Directory.CreateDirectory(Path.GetDirectoryName(DriveViewPath)!);File.WriteAllText(DriveViewPath,JsonSerializer.Serialize(driveView));}catch(Exception ex){System.Windows.MessageBox.Show($"保存视图设置失败：{ex.Message}");}}
    void ApplyDriveView()
    {
        Resources["DriveCardWidth"]=new double[]{540,420,350}[driveView.Density-1];
        Resources["DriveCardPadding"]=new Thickness(new double[]{24,20,14}[driveView.Density-1]);
        Resources["DriveDetailVisibility"]=Visibility.Visible;
        var panel=new FrameworkElementFactory(driveView.Layout==0?typeof(WrapPanel):typeof(StackPanel));
        ConnectionList.ItemsPanel=new ItemsPanelTemplate(panel);
        ConnectionList.ItemTemplate=driveView.Layout==0?cardTemplate:CreateDriveRow();
        SortDriveView();
    }
    void SortDriveView()
    {
        var view=CollectionViewSource.GetDefaultView(ConnectionList.ItemsSource);if(view is null)return;
        using(view.DeferRefresh()){view.SortDescriptions.Clear();if(driveView.MountedFirst)view.SortDescriptions.Add(new SortDescription(nameof(ConnectionProfile.IsMounted),ListSortDirection.Descending));}
    }
    DataTemplate CreateDriveRow()
    {
        var border=new FrameworkElementFactory(typeof(Border));border.SetResourceReference(Border.BackgroundProperty,"CardBackground");border.SetResourceReference(Border.BorderBrushProperty,"BorderBrush");border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetValue(Border.MarginProperty,new Thickness(0,0,12,8));border.SetValue(Border.PaddingProperty,new Thickness(new double[]{24,20,14}[driveView.Density-1]));
        var dock=new FrameworkElementFactory(typeof(DockPanel));border.AppendChild(dock);
        var actions=new FrameworkElementFactory(typeof(StackPanel));actions.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);actions.SetValue(DockPanel.DockProperty,Dock.Right);dock.AppendChild(actions);
        void Action(string caption,RoutedEventHandler handler,bool primary=false){var b=new FrameworkElementFactory(typeof(Button));b.SetBinding(FrameworkElement.TagProperty,new Binding());if(primary)b.SetBinding(Button.ContentProperty,new Binding("ActionLabel"));else b.SetValue(Button.ContentProperty,caption);b.SetValue(Button.PaddingProperty,new Thickness(10,6,10,6));b.SetValue(Button.MarginProperty,new Thickness(4,0,0,0));if(primary)b.SetResourceReference(Button.StyleProperty,"PrimaryButton");b.AddHandler(Button.ClickEvent,handler);actions.AppendChild(b);}
        Action("",ToggleMount,true);Action("打开",OpenDrive);Action("编辑",EditConnection);Action("删除",DeleteConnection);
        var letter=new FrameworkElementFactory(typeof(TextBlock));letter.SetBinding(TextBlock.TextProperty,new Binding("DriveLabel"));letter.SetValue(TextBlock.WidthProperty,52d);letter.SetValue(TextBlock.FontWeightProperty,FontWeights.Bold);letter.SetResourceReference(TextBlock.ForegroundProperty,"Accent");letter.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);dock.AppendChild(letter);
        var info=new FrameworkElementFactory(typeof(StackPanel));dock.AppendChild(info);
        void Text(string binding,bool title=false){var t=new FrameworkElementFactory(typeof(TextBlock));t.SetBinding(TextBlock.TextProperty,new Binding(binding));t.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);t.SetValue(TextBlock.FontSizeProperty,title?new double[]{21,18,15}[driveView.Density-1]:12d);t.SetResourceReference(TextBlock.ForegroundProperty,title?"TextPrimary":"TextSecondary");info.AppendChild(t);}
        Text("Name",true);Text("StatusLabel");Text("Endpoint");if(driveView.Density==3)Text("Path");
        return new DataTemplate{VisualTree=border};
    }
}
