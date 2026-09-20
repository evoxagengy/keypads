using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SwitchKeypad.Core.Models;
using SwitchKeypad.UI.Controls;

namespace SwitchKeypad;

public sealed class DeviceLayoutWizardWindow : Window
{
    private readonly DeviceDefinition _device;
    private readonly Grid _keyGrid=new();
    private readonly TextBlock _status=new(){TextWrapping=TextWrapping.Wrap,Foreground=UI.ModalChrome.Brush("#A9BED5")};
    private readonly TextBox _label=new(){MinWidth=170};
    private readonly ComboBox _template=new(){MinWidth=190};
    private readonly TextBox _rows=new(){Width=54,Text="5"};
    private readonly TextBox _columns=new(){Width=54,Text="4"};
    private PhysicalKeyDefinition? _selected;
    private CancellationTokenSource? _captureDelay;
    private readonly List<DeviceKeyEvent> _capture=[];
    private bool _closed;

    public DeviceLayoutDefinition Layout { get; private set; }

    public DeviceLayoutWizardWindow(DeviceDefinition device,DeviceLayoutDefinition current)
    {
        _device=device;
        Layout=Clone(current);
        Title="Identificar teclas";
        Width=1040;Height=780;MinWidth=840;MinHeight=640;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=UI.ModalChrome.Brush("#071421");Foreground=Brushes.White;

        var root=new DockPanel{Margin=new Thickness(24),Background=Background};Content=root;

        var heading=new StackPanel{Margin=new Thickness(0,0,0,16)};
        heading.Children.Add(new TextBlock{Text="Identificar e organizar teclas",FontSize=26,FontWeight=FontWeights.SemiBold});
        heading.Children.Add(new TextBlock
        {
            Text="Clique na posição visual e depois pressione a tecla física correspondente. O mapeamento fica vinculado somente ao dispositivo selecionado.",
            Foreground=UI.ModalChrome.Brush("#A9BED5"),Margin=new Thickness(0,6,0,0),TextWrapping=TextWrapping.Wrap
        });
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);

        var footer=new Grid{Margin=new Thickness(0,16,0,0)};
        footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        _status.Text="Selecione uma tecla na grade para começar.";
        footer.Children.Add(_status);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var cancel=MakeButton("Cancelar",false);cancel.Click+=(_,_)=>DialogResult=false;
        var save=MakeButton("Salvar layout",true);save.Click+=(_,_)=>Save();
        actions.Children.Add(cancel);actions.Children.Add(save);Grid.SetColumn(actions,1);footer.Children.Add(actions);
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);

        var controls=new WrapPanel{Margin=new Thickness(0,0,0,12)};
        controls.Children.Add(new TextBlock{Text="Modelo",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
        _template.DisplayMemberPath="Value";_template.SelectedValuePath="Key";
        _template.ItemsSource=new[]{
            new KeyValuePair<DeviceLayoutTemplate,string>(DeviceLayoutTemplate.Numeric4x5,"Numpad 4 × 5"),
            new KeyValuePair<DeviceLayoutTemplate,string>(DeviceLayoutTemplate.FullKeyboard,"Teclado completo"),
            new KeyValuePair<DeviceLayoutTemplate,string>(DeviceLayoutTemplate.Custom,"Grade personalizada")};
        _template.SelectedValue=Layout.Source=="full-keyboard"?DeviceLayoutTemplate.FullKeyboard:
                                Layout.Source=="numeric-4x5"?DeviceLayoutTemplate.Numeric4x5:DeviceLayoutTemplate.Custom;
        controls.Children.Add(_template);
        var apply=MakeButton("Aplicar modelo",false);apply.Click+=(_,_)=>ApplySelectedTemplate();controls.Children.Add(apply);

        controls.Children.Add(new TextBlock{Text="Linhas",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(18,0,6,0)});
        controls.Children.Add(_rows);
        controls.Children.Add(new TextBlock{Text="Colunas",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,6,0)});
        controls.Children.Add(_columns);
        var gridButton=MakeButton("Aplicar grade",false);gridButton.Click+=(_,_)=>ApplyCustomGrid();controls.Children.Add(gridButton);

        var square=MakeButton("Normal",false);square.Click+=(_,_)=>SetShape(1,1);controls.Children.Add(square);
        var wide=MakeButton("Larga",false);wide.Click+=(_,_)=>SetShape(1,2);controls.Children.Add(wide);
        var tall=MakeButton("Vertical",false);tall.Click+=(_,_)=>SetShape(2,1);controls.Children.Add(tall);

        var rename=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,10)};
        rename.Children.Add(new TextBlock{Text="Nome da tecla",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
        rename.Children.Add(_label);
        var applyName=MakeButton("Aplicar nome",false);applyName.Click+=(_,_)=>ApplyLabel();rename.Children.Add(applyName);

        var body=new DockPanel();
        var top=new StackPanel();top.Children.Add(controls);top.Children.Add(rename);DockPanel.SetDock(top,Dock.Top);body.Children.Add(top);
        var frame=new Border
        {
            Background=UI.ModalChrome.Brush("#091A28"),BorderBrush=UI.ModalChrome.Brush("#18364C"),BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(10),Padding=new Thickness(18)
        };
        var scroll=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var view=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=_keyGrid};
        scroll.Content=view;frame.Child=scroll;body.Children.Add(frame);root.Children.Add(body);

        App.RawInput.KeyEvent+=RawInput_KeyEvent;
        Closed+=(_,_)=>CloseWizard();
        Render();
        UI.ModalChrome.Apply(this);
    }

    private Button MakeButton(string text,bool primary)
    {
        var b=new Button{Content=text,Style=(Style)FindResource(primary?"PrimaryButton":"FlatButton"),Margin=new Thickness(7,0,0,0),Padding=new Thickness(14,8,14,8)};
        return b;
    }

    private static DeviceLayoutDefinition Clone(DeviceLayoutDefinition source)
        => JsonSerializer.Deserialize<DeviceLayoutDefinition>(JsonSerializer.Serialize(source))!;

    private void ApplySelectedTemplate()
    {
        if(_template.SelectedValue is not DeviceLayoutTemplate template)return;
        Layout=DeviceLayoutCatalog.CreateTemplate(template,_device.Fingerprint,DisplayDeviceName());
        if(template==DeviceLayoutTemplate.Custom)
        {
            _rows.Text="5";_columns.Text="4";
        }
        _selected=null;_label.Text="";Render();
        _status.Text="Modelo aplicado somente nesta janela. Clique em uma posição e pressione a tecla física.";
    }

    private void ApplyCustomGrid()
    {
        if(!int.TryParse(_rows.Text,out var rows)||!int.TryParse(_columns.Text,out var columns)||rows is <1 or >12||columns is <1 or >24)
        {
            _status.Text="Use de 1 a 12 linhas e de 1 a 24 colunas.";return;
        }
        Layout=DeviceLayoutCatalog.GenericGrid(_device.Fingerprint,DisplayDeviceName(),rows,columns);
        _template.SelectedValue=DeviceLayoutTemplate.Custom;_selected=null;Render();
        _status.Text="Grade personalizada criada. Clique em cada posição e pressione a tecla correspondente.";
    }

    private void SetShape(int rowSpan,int columnSpan)
    {
        if(_selected is null){_status.Text="Selecione uma tecla da grade antes de alterar o formato.";return;}
        _selected.RowSpan=rowSpan;_selected.ColumnSpan=columnSpan;Render();
    }

    private void ApplyLabel()
    {
        if(_selected is null)return;
        var value=_label.Text.Trim();
        if(value.Length>24){_status.Text="O nome da tecla pode ter no máximo 24 caracteres.";return;}
        _selected.Label=value;Render();
    }

    private void Render()
    {
        _keyGrid.Children.Clear();_keyGrid.RowDefinitions.Clear();_keyGrid.ColumnDefinitions.Clear();
        var columns=Math.Max(1,Layout.Columns);
        var rows=Math.Max(1,Layout.Keys.Count==0?1:Layout.Keys.Max(k=>k.Row+Math.Max(1,k.RowSpan)));
        for(int i=0;i<rows;i++)_keyGrid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(116)});
        for(int i=0;i<columns;i++)_keyGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(116)});
        _keyGrid.Width=columns*116;_keyGrid.Height=rows*116;

        foreach(var key in Layout.Keys.OrderBy(k=>k.Row).ThenBy(k=>k.Column))
        {
            var tile=new KeyTileControl
            {
                Tag=key,
                KeyLabel=string.IsNullOrWhiteSpace(key.Label)?"?":key.Label,
                ActionLabel=key.ScanCode<0?"Não identificado":SignatureText(key),
                Shape=key.RowSpan>1?"vertical":key.ColumnSpan>1?"horizontal":"square",
                IsSelectedKey=ReferenceEquals(key,_selected),
                Margin=new Thickness(4)
            };
            tile.Click+=(_,_)=>Select(key);
            Grid.SetRow(tile,Math.Max(0,key.Row));Grid.SetColumn(tile,Math.Max(0,key.Column));
            Grid.SetRowSpan(tile,Math.Max(1,key.RowSpan));Grid.SetColumnSpan(tile,Math.Max(1,key.ColumnSpan));
            _keyGrid.Children.Add(tile);
        }
    }

    private void Select(PhysicalKeyDefinition key)
    {
        _selected=key;_label.Text=key.Label;
        _capture.Clear();_captureDelay?.Cancel();
        Render();
        _status.Text=key.ScanCode<0
            ? "Agora pressione a tecla física que ocupa esta posição."
            : "Pressione a tecla física novamente para confirmar ou substituir este vínculo.";
    }

    private void RawInput_KeyEvent(object? sender,DeviceKeyEvent e)
    {
        if(_closed||!e.IsKeyDown||e.DeviceId!=_device.Fingerprint||_selected is null)return;
        if(!Dispatcher.CheckAccess()){_ = Dispatcher.BeginInvoke(()=>RawInput_KeyEvent(sender,e));return;}

        if(_capture.Count==0)
        {
            _capture.Add(e);
        }
        else
        {
            var first=_capture[0];
            if(first.ScanCode==e.ScanCode&&first.IsExtended==e.IsExtended)_capture.Add(e);
        }

        _captureDelay?.Cancel();_captureDelay?.Dispose();
        _captureDelay=new CancellationTokenSource();
        var token=_captureDelay.Token;
        _=Task.Run(async()=>
        {
            try
            {
                await Task.Delay(180,token);
                await Dispatcher.InvokeAsync(CommitCapture);
            }
            catch(OperationCanceledException){}
        });
    }

    private void CommitCapture()
    {
        if(_selected is null||_capture.Count==0)return;
        var first=_capture[0];
        var count=Math.Max(1,_capture.Count);

        var duplicate=Layout.Keys.FirstOrDefault(k=>!ReferenceEquals(k,_selected)&&k.ScanCode==first.ScanCode&&k.IsExtended==first.IsExtended&&Math.Max(1,k.PressCount)==count);
        if(duplicate is not null)
        {
            duplicate.ScanCode=-1;duplicate.VirtualKey=0;duplicate.IsExtended=false;duplicate.PressCount=1;duplicate.ReservedToggle=false;
        }

        _selected.ScanCode=first.ScanCode;
        _selected.VirtualKey=first.VirtualKey;
        _selected.IsExtended=first.IsExtended;
        _selected.PressCount=count;
        _selected.ReservedToggle=DeviceLayoutCatalog.IsNumLock(first);

        if(_selected.ReservedToggle)_selected.Label="Num Lock";
        else if(string.IsNullOrWhiteSpace(_selected.Label)||_selected.Label=="?")_selected.Label=GuessLabel(first,count);

        _status.Text=$"Identificado: {_selected.Label} • Scan 0x{first.ScanCode:X2}{(first.IsExtended?" E0":"")} • {count} pulso(s)." +
                     (duplicate is null?"":$" O vínculo anterior em {duplicate.Id} foi liberado.");
        _capture.Clear();Render();
    }

    private static string GuessLabel(DeviceKeyEvent e,int count)
    {
        if(count==3&&e.ScanCode==0x52)return "000";
        if(DeviceLayoutCatalog.IsNumLock(e))return "Num Lock";
        if(e.ScanCode==0x0E)return "Backspace";
        if(e.ScanCode==0x1C)return "Enter";
        if(e.VirtualKey is >=0x30 and <=0x39)return ((char)e.VirtualKey).ToString();
        if(e.VirtualKey is >=0x41 and <=0x5A)return ((char)e.VirtualKey).ToString();
        return $"0x{e.ScanCode:X2}";
    }

    private static string SignatureText(PhysicalKeyDefinition key)
        => key.PressCount>1?$"Scan {key.ScanCode:X2} ×{key.PressCount}":$"Scan {key.ScanCode:X2}";

    private void Save()
    {
        var occupied=new HashSet<string>();
        foreach(var key in Layout.Keys)
        {
            if(key.Row<0||key.Column<0||key.RowSpan<1||key.ColumnSpan<1||key.Column+key.ColumnSpan>Layout.Columns)
            {
                _status.Text="Existe uma tecla fora dos limites da grade.";return;
            }
            for(int r=key.Row;r<key.Row+key.RowSpan;r++)
                for(int c=key.Column;c<key.Column+key.ColumnSpan;c++)
                    if(!occupied.Add(r+":"+c)){_status.Text="Existem teclas sobrepostas. Ajuste o formato/posição antes de salvar.";return;}
        }

        foreach(var key in Layout.Keys.Where(k=>k.ScanCode==0x45||k.VirtualKey==0x90))
        {
            key.ReservedToggle=true;
            if(string.IsNullOrWhiteSpace(key.Label))key.Label="Num Lock";
        }

        var pending=Layout.Keys.Count(k=>k.ScanCode<0);
        Layout.Confirmed=pending==0;
        Layout.Source=Layout.Source=="unconfigured"?"learned":Layout.Source;
        DialogResult=true;
    }

    private string DisplayDeviceName()=>string.IsNullOrWhiteSpace(_device.CustomName)?_device.FriendlyName:_device.CustomName!;

    private void CloseWizard()
    {
        if(_closed)return;_closed=true;
        _captureDelay?.Cancel();_captureDelay?.Dispose();
        App.RawInput.KeyEvent-=RawInput_KeyEvent;
    }
}
