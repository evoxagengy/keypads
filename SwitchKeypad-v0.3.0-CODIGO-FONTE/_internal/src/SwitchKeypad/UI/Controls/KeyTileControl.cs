using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SwitchKeypad.UI.Controls;

public sealed class KeyTileControl : System.Windows.Controls.Button
{
    public static readonly DependencyProperty KeyLabelProperty = DependencyProperty.Register(nameof(KeyLabel),typeof(string),typeof(KeyTileControl),new(""));
    public static readonly DependencyProperty ActionLabelProperty = DependencyProperty.Register(nameof(ActionLabel),typeof(string),typeof(KeyTileControl),new(""));
    public static readonly DependencyProperty IconTextProperty = DependencyProperty.Register(nameof(IconText),typeof(string),typeof(KeyTileControl),new(""));
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(nameof(Shape),typeof(string),typeof(KeyTileControl),new("square"));
    public static readonly DependencyProperty IsSelectedKeyProperty = DependencyProperty.Register(nameof(IsSelectedKey),typeof(bool),typeof(KeyTileControl),new(false,(_,__)=>{}));
    public string KeyLabel { get=> (string)GetValue(KeyLabelProperty); set=>SetValue(KeyLabelProperty,value); }
    public string ActionLabel { get=> (string)GetValue(ActionLabelProperty); set=>SetValue(ActionLabelProperty,value); }
    public string IconText { get=> (string)GetValue(IconTextProperty); set=>SetValue(IconTextProperty,value); }
    public string Shape { get=> (string)GetValue(ShapeProperty); set=>SetValue(ShapeProperty,value); }
    public bool IsSelectedKey { get=> (bool)GetValue(IsSelectedKeyProperty); set=>SetValue(IsSelectedKeyProperty,value); }

    private static readonly Dictionary<string,ImageSource> ImageCache=new(StringComparer.OrdinalIgnoreCase);
    private readonly Image _appIcon=new(){Width=30,Height=30,Stretch=Stretch.Uniform,IsHitTestVisible=false,Margin=new Thickness(0,9,0,20)};
    public void SetActionIcon(ImageSource? image){_appIcon.Source=image;_appIcon.Visibility=image is null?Visibility.Collapsed:Visibility.Visible;_icon.Visibility=image is null?Visibility.Visible:Visibility.Collapsed;}
    private readonly System.Windows.Controls.Image _bg = new(){Stretch=System.Windows.Media.Stretch.Fill,IsHitTestVisible=false,Margin=new Thickness(4)};
    private readonly TextBlock _key = new(){FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(18,16,8,0),HorizontalAlignment=System.Windows.HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock _icon = new(){FontSize=22,HorizontalAlignment=System.Windows.HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,9,0,20)};
    private readonly TextBlock _action = new(){FontSize=10.5,FontWeight=FontWeights.Medium,HorizontalAlignment=System.Windows.HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Bottom,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,MaxHeight=30,Margin=new Thickness(12,0,12,11),Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220,232,246))};

    public KeyTileControl()
    {
        Background=System.Windows.Media.Brushes.Transparent; BorderThickness=new Thickness(0); Padding=new Thickness(0); Cursor=System.Windows.Input.Cursors.Hand; Focusable=true;
        var grid=new Grid{Background=Brushes.Transparent}; grid.Children.Add(_bg); grid.Children.Add(_key); grid.Children.Add(_icon); grid.Children.Add(_appIcon); grid.Children.Add(_action); Content=grid;
        Loaded += (_,_)=>Refresh(); MouseEnter += (_,_)=>Refresh("hover"); MouseLeave += (_,_)=>Refresh(); PreviewMouseDown += (_,_)=>Refresh("select"); PreviewMouseUp += (_,_)=>Refresh(IsMouseOver?"hover":null);
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){ base.OnPropertyChanged(e); if(IsLoaded) Refresh(); }
    private void Refresh(string? forced=null)
    {
        _key.Text=KeyLabel; _action.Text=string.IsNullOrWhiteSpace(ActionLabel)?"Sem ação":ActionLabel; _icon.Text=IconText;
        _key.FontSize=KeyLabel.Length switch { >12=>11, >8=>13, >5=>15, _=>18 };
        var state = IsSelectedKey || IsPressed ? "select" : IsMouseOver || IsKeyboardFocused ? "hover" : "normal";
        var file = Shape.ToLowerInvariant() switch
        {
            "vertical"=>$"switch_keypad_tecla_vertical_{state}.png",
            "horizontal"=>$"switch_keypad_tecla_horizontal_{state}.png",
            _=>$"switch_keypad_tecla_{state}.png"
        };
        _bg.Source = GetNormalizedImage(file);
    }

    private static ImageSource GetNormalizedImage(string file)
    {
        if (ImageCache.TryGetValue(file,out var cached)) return cached;
        var source=new BitmapImage();
        source.BeginInit();
        source.CacheOption=BitmapCacheOption.OnLoad;
        source.UriSource=new Uri($"pack://application:,,,/SwitchKeypad;component/Assets/Keys/{file}",UriKind.Absolute);
        source.EndInit();
        source.Freeze();
        var crop=file switch
        {
            "switch_keypad_tecla_normal.png"=>new Int32Rect(78,69,565,566),
            "switch_keypad_tecla_hover.png"=>new Int32Rect(58,69,564,566),
            "switch_keypad_tecla_select.png"=>new Int32Rect(6,9,572,572),
            "switch_keypad_tecla_vertical_normal.png"=>new Int32Rect(129,128,404,865),
            "switch_keypad_tecla_vertical_hover.png"=>new Int32Rect(107,50,399,864),
            "switch_keypad_tecla_vertical_select.png"=>new Int32Rect(52,50,406,864),
            "switch_keypad_tecla_horizontal_normal.png"=>new Int32Rect(64,273,1322,540),
            "switch_keypad_tecla_horizontal_hover.png"=>new Int32Rect(49,251,1351,592),
            "switch_keypad_tecla_horizontal_select.png"=>new Int32Rect(48,279,1352,530),
            _=>new Int32Rect(0,0,source.PixelWidth,source.PixelHeight)
        };
        var normalized=new CroppedBitmap(source,crop);
        normalized.Freeze();
        ImageCache[file]=normalized;
        return normalized;
    }
}
