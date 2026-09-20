using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace SwitchKeypad.UI;

/// <summary>Shared dark dialog frame. Native WindowChrome keeps drag, resize and DPI behavior.</summary>
public static class ModalChrome
{
    public static void Apply(Window window)
    {
        var body=window.Content as UIElement;
        window.Content=null;
        window.WindowStyle=WindowStyle.None;
        window.ShowInTaskbar=false;
        window.FontFamily=new FontFamily("Segoe UI");
        window.FontSize=14;
        window.Foreground=Brushes.White;
        window.Background=Brush("#071421");
        WindowChrome.SetWindowChrome(window,new WindowChrome
        {
            CaptionHeight=48, ResizeBorderThickness=new Thickness(window.ResizeMode==ResizeMode.NoResize?0:6),
            GlassFrameThickness=new Thickness(0), CornerRadius=new CornerRadius(12), UseAeroCaptionButtons=false
        });
        var layout=new DockPanel();
        var header=new Grid{Height=48,Background=Brush("#0D2233")};
        header.Children.Add(new TextBlock{Text="SWITCH KEYPAD  /  "+window.Title,FontSize=12,
            Foreground=Brush("#A9BED5"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(24,0,64,0)});
        var close=new Button{Content="✕",Width=40,Height=32,Margin=new Thickness(0,0,10,0),
            HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,
            Style=(Style)window.FindResource("FlatButton"),Padding=new Thickness(0),
            HorizontalContentAlignment=HorizontalAlignment.Center,ToolTip="Fechar"};
        System.Windows.Automation.AutomationProperties.SetName(close,"Fechar diálogo");
        WindowChrome.SetIsHitTestVisibleInChrome(close,true);
        close.Click+=(_,_)=>window.Close();
        header.Children.Add(close);DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
        if(body is not null)layout.Children.Add(body);
        window.Content=new Border{Background=window.Background,BorderBrush=Brush("#24445E"),BorderThickness=new Thickness(1),Child=layout};
        window.Loaded+=(_,_)=>{
            var screen=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            var dpi=VisualTreeHelper.GetDpi(window);
            window.MinHeight=Math.Min(window.MinHeight,screen.WorkingArea.Height/dpi.DpiScaleY);
            window.MinWidth=Math.Min(window.MinWidth,screen.WorkingArea.Width/dpi.DpiScaleX);
            window.MaxHeight=screen.WorkingArea.Height/dpi.DpiScaleY;
            window.MaxWidth=screen.WorkingArea.Width/dpi.DpiScaleX;
        };
        window.KeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!e.Handled){window.Close();e.Handled=true;}};
    }

    public static SolidColorBrush Brush(string color)=>(SolidColorBrush)new BrushConverter().ConvertFrom(color)!;

    public static Window Create(Window owner,string title,string description,FrameworkElement body,
        string confirm,Func<bool> accept,bool destructive=false)
    {
        var dialog=new Window{Owner=owner,Title=title,Width=480,SizeToContent=SizeToContent.Height,
            ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new StackPanel{Margin=new Thickness(26)};
        panel.Children.Add(new TextBlock{Text=title,FontSize=24,FontWeight=FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text=description,Foreground=Brush("#A9BED5"),
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,22),LineHeight=22});
        panel.Children.Add(body);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
        var cancel=new Button{Content="Cancelar",Style=(Style)owner.FindResource("FlatButton"),Margin=new Thickness(0,0,10,0),MinWidth=100};
        cancel.Click+=(_,_)=>dialog.DialogResult=false;
        var save=new Button{Content=confirm,Style=(Style)owner.FindResource("PrimaryButton"),MinWidth=110,IsDefault=true};
        if(destructive){save.Background=Brush("#A82F49");save.BorderBrush=Brush("#D34A66");}
        save.Click+=(_,_)=>{if(accept())dialog.DialogResult=true;};
        buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
        dialog.Content=panel;Apply(dialog);return dialog;
    }
}
