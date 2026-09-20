using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SwitchKeypad.Core.Models;
using SwitchKeypad.Core.Actions;
using SwitchKeypad.Windows.Shell;
namespace SwitchKeypad;

public sealed class ActionEditorWindow : Window
{
    public ActionDefinition Action { get; private set; }
    private readonly TextBox _name=new(), _parameter=new(), _search=new(), _delay=new();
    private readonly ComboBox _type=new(), _choice=new();
    private readonly ListBox _apps=new(), _steps=new();
    private readonly CheckBox _enter=new(){Content="Pressionar Enter depois do texto",Foreground=Brushes.White};
    private readonly StackPanel _fields=new(), _sequence=new(), _appPanel=new();
    private readonly TextBlock _label=new(), _hint=new(){TextWrapping=TextWrapping.Wrap}, _result=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightSalmon};
    private Button _record=null!, _browse=null!, _test=null!;
    private bool _loading, _recording;
    private string _parameterKey="path", _choiceKey="command";
    private IReadOnlyList<DiscoveredApp> _discovered=[];
    private readonly CancellationTokenSource _lifetime=new();
    private CancellationTokenSource? _testRun;
    private readonly ComboBox _repeatMode=new(){DisplayMemberPath="Value",SelectedValuePath="Key"};
    private readonly TextBox _repeatCount=new(),_repeatInterval=new();
    public static ActionDefinition Copy(ActionDefinition a)=>JsonSerializer.Deserialize<ActionDefinition>(JsonSerializer.Serialize(a))!;
    public static string Label(ActionType t)=>t switch {
        ActionType.OpenApplication=>"Abrir aplicativo",ActionType.OpenFile=>"Abrir arquivo",ActionType.OpenFolder=>"Abrir pasta",
        ActionType.OpenUrl=>"Abrir site",ActionType.WebSearch=>"Pesquisar na internet",ActionType.SendHotkey=>"Atalho de teclado",
        ActionType.TypeText=>"Texto personalizado",ActionType.MediaControl=>"Controle de mídia",ActionType.SystemAction=>"Comando de sistema",
        ActionType.MultiAction=>"Sequência de ações",ActionType.SwitchProfile=>"Trocar perfil",ActionType.PassThrough=>"Tecla normal",ActionType.Delay=>"Esperar",_=>"Sem ação"};
    public ActionEditorWindow(ActionDefinition action,bool step=false)
    {
        Action=Copy(action);
        Title=step?"Editar etapa":"Configurar ação";Width=760;Height=860;MinWidth=640;MinHeight=600;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=(Brush)new BrushConverter().ConvertFrom("#071421")!;Foreground=Brushes.White;
        var root=new DockPanel{Margin=new Thickness(24),Background=Background};Content=root;
        var heading=new StackPanel{Margin=new Thickness(0,0,0,20)};
        heading.Children.Add(new TextBlock{Text=Title,FontSize=26,FontWeight=FontWeights.SemiBold});
        heading.Children.Add(new TextBlock{Text=step?"Defina o que acontece nesta etapa da sequência.":"Personalize o que a tecla faz no seu dia a dia.",Foreground=UI.ModalChrome.Brush("#A9BED5"),Margin=new Thickness(0,6,0,0)});
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,15,0,0)};
        _test=Button("Testar em 3 segundos",Test);footer.Children.Add(_test);
        footer.Children.Add(Button("Cancelar",(_,_)=>DialogResult=false));
        footer.Children.Add(Button("Salvar ação",Save,true));
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var body=new StackPanel{Margin=new Thickness(0,0,10,0)};root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        Add(body,"Nome na tecla",_name);Add(body,"Tipo de ação",_type);
        _type.DisplayMemberPath="Value";_type.SelectedValuePath="Key";
        _type.ItemsSource=Enum.GetValues<ActionType>().Where(t=>!step||t!=ActionType.MultiAction).Select(t=>new KeyValuePair<ActionType,string>(t,Label(t))).ToList();
        body.Children.Add(_fields);_fields.Children.Add(_label);_fields.Children.Add(_parameter);
        _parameter.Margin=new Thickness(0,6,0,10);_parameter.TextWrapping=TextWrapping.Wrap;_parameter.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        var tools=new WrapPanel();_fields.Children.Add(tools);
        _browse=Button("Procurar…",Browse);tools.Children.Add(_browse);
        _record=Button("Gravar atalho",(_,_)=>{_recording=true;_record.Content="Pressione o atalho…";_record.Focus();});
        _record.PreviewKeyDown+=Record;tools.Children.Add(_record);
        _fields.Children.Add(_appPanel);Add(_appPanel,"Aplicativos instalados — pesquise como no Windows",_search);
        _apps.Height=180;StyleList(_apps);
        var appTemplate=new DataTemplate(typeof(DiscoveredApp));
        var appRow=new FrameworkElementFactory(typeof(StackPanel));appRow.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
        var appIcon=new FrameworkElementFactory(typeof(Image));appIcon.SetValue(Image.WidthProperty,28.0);appIcon.SetValue(Image.HeightProperty,28.0);appIcon.SetValue(Image.MarginProperty,new Thickness(4,4,12,4));appIcon.SetBinding(Image.SourceProperty,new System.Windows.Data.Binding(nameof(DiscoveredApp.Icon)));
        var appName=new FrameworkElementFactory(typeof(TextBlock));appName.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);appName.SetValue(TextBlock.FontSizeProperty,14.0);appName.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(DiscoveredApp.Name)));
        appRow.AppendChild(appIcon);appRow.AppendChild(appName);appTemplate.VisualTree=appRow;_apps.ItemTemplate=appTemplate;_appPanel.Children.Add(_apps);
        _search.TextChanged+=(_,_)=>{if(!_loading)_apps.ItemsSource=_discovered.Where(a=>a.Name.Contains(_search.Text,StringComparison.CurrentCultureIgnoreCase)).ToList();};
        _apps.SelectionChanged+=(_,_)=>{if(!_loading&&_apps.SelectedItem is DiscoveredApp app){_parameter.Text=app.LaunchTarget;_name.Text=app.Name;}};
        _choice.DisplayMemberPath="Value";_choice.SelectedValuePath="Key";_choice.Margin=new Thickness(0,10,0,10);_fields.Children.Add(_choice);
        _fields.Children.Add(_enter);
        body.Children.Add(_sequence);Add(_sequence,"Passos da sequência — até 32",_steps);_steps.Height=160;StyleList(_steps);
        var stepTools=new WrapPanel();_sequence.Children.Add(stepTools);
        stepTools.Children.Add(Button("Adicionar",(_,_)=>EditStep(true)));
        stepTools.Children.Add(Button("Editar",(_,_)=>EditStep(false)));
        stepTools.Children.Add(Button("Remover",(_,_)=>{if(_steps.SelectedIndex>=0){Action.Steps.RemoveAt(_steps.SelectedIndex);RefreshSteps();}}));
        stepTools.Children.Add(Button("Duplicar",(_,_)=>{if(_steps.SelectedIndex>=0&&Action.Steps.Count<32){var index=_steps.SelectedIndex;Action.Steps.Insert(index+1,Copy(Action.Steps[index]));RefreshSteps();_steps.SelectedIndex=index+1;}}));
        stepTools.Children.Add(Button("↑",(_,_)=>Move(-1)));stepTools.Children.Add(Button("↓",(_,_)=>Move(1)));
        _repeatMode.ItemsSource=new[]{
            new KeyValuePair<SequenceRepeatMode,string>(SequenceRepeatMode.Once,"Executar uma vez"),
            new KeyValuePair<SequenceRepeatMode,string>(SequenceRepeatMode.Count,"Repetir por quantidade"),
            new KeyValuePair<SequenceRepeatMode,string>(SequenceRepeatMode.UntilCancelled,"Repetir até interromper")};
        Add(_sequence,"Repetição",_repeatMode);
        var repeatFields=new Grid();repeatFields.ColumnDefinitions.Add(new());repeatFields.ColumnDefinitions.Add(new());
        var countPanel=new StackPanel{Margin=new Thickness(0,0,10,0)};
        var intervalPanel=new StackPanel{Margin=new Thickness(10,0,0,0)};
        Add(countPanel,"Quantidade (1–10.000)",_repeatCount);Add(intervalPanel,"Intervalo entre ciclos (ms)",_repeatInterval);
        Grid.SetColumn(intervalPanel,1);repeatFields.Children.Add(countPanel);repeatFields.Children.Add(intervalPanel);_sequence.Children.Add(repeatFields);
        _repeatMode.SelectedValue=Action.Repeat.Mode;_repeatCount.Text=Action.Repeat.Count.ToString();_repeatInterval.Text=Action.Repeat.IntervalMs.ToString();
        void UpdateRepeat(){_repeatCount.IsEnabled=_repeatMode.SelectedValue is SequenceRepeatMode.Count;_repeatInterval.IsEnabled=_repeatMode.SelectedValue is not SequenceRepeatMode.Once;}
        _repeatMode.SelectionChanged+=(_,_)=>UpdateRepeat();UpdateRepeat();
        Add(body,"Espera antes desta ação (0–5000 ms)",_delay);
        _hint.Margin=new Thickness(0,12,0,12);body.Children.Add(_hint);body.Children.Add(_result);
        _name.Text=Action.Name;_delay.Text=Action.Parameters.GetValueOrDefault("delayMs","0");_type.SelectedValue=Action.Type;
        _type.SelectionChanged+=(_,_)=>{if(!_loading&&_type.SelectedValue is ActionType t){Collect();Action.Type=t;_name.Text=Label(t);LoadFields();}};
        LoadFields();Closed+=(_,_)=>_lifetime.Cancel();
        UI.ModalChrome.Apply(this);
    }
    private Button Button(string title,RoutedEventHandler handler,bool primary=false)
    {
        var button=new Button{Content=title,Style=(Style)FindResource(primary?"PrimaryButton":"FlatButton"),Margin=new Thickness(0,0,7,7)};
        button.Click+=handler;return button;
    }
    private static void Add(Panel panel,string label,FrameworkElement field){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,8,0,6)});field.Margin=new Thickness(0,0,0,10);panel.Children.Add(field);}
    private static void StyleList(ListBox list){list.Background=UI.ModalChrome.Brush("#091A28");list.Foreground=Brushes.White;list.BorderBrush=UI.ModalChrome.Brush("#24445E");list.Padding=new Thickness(5);ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);}
    private static void Show(UIElement element,bool show)=>element.Visibility=show?Visibility.Visible:Visibility.Collapsed;
    private static List<KeyValuePair<string,string>> Pairs(params string[] strings){var list=new List<KeyValuePair<string,string>>();for(int i=0;i<strings.Length;i+=2)list.Add(new(strings[i],strings[i+1]));return list;}
    private void LoadFields()
    {
        _loading=true;var t=Action.Type;
        _parameterKey=t switch {ActionType.Delay=>"milliseconds",ActionType.OpenUrl=>"url",ActionType.WebSearch=>"query",ActionType.SendHotkey=>"hotkey",ActionType.TypeText=>"text",ActionType.MediaControl or ActionType.SystemAction=>"command",ActionType.SwitchProfile=>"profileId",_=>"path"};
        _label.Text=t==ActionType.Delay?"Tempo de espera (10–60.000 ms)":t==ActionType.TypeText?"Texto a digitar":t==ActionType.SendHotkey?"Tecla ou combinação":t==ActionType.WebSearch?"O que pesquisar":"Destino da ação";
        _parameter.Text=Action.Parameters.GetValueOrDefault(_parameterKey,"");
        _parameter.AcceptsReturn=t==ActionType.TypeText;_parameter.Height=t==ActionType.TypeText?120:42;
        Show(_fields,t is not (ActionType.MultiAction or ActionType.Disabled or ActionType.PassThrough));
        Show(_sequence,t==ActionType.MultiAction);
        Show(_browse,t is ActionType.OpenApplication or ActionType.OpenFile or ActionType.OpenFolder);
        Show(_record,t==ActionType.SendHotkey);Show(_enter,t==ActionType.TypeText);
        _enter.IsChecked=Action.Parameters.GetValueOrDefault("appendEnter")=="true";
        Show(_appPanel,t==ActionType.OpenApplication);
        if(t==ActionType.OpenApplication){_discovered=AppDiscoveryService.ScanInstalledApps();_apps.ItemsSource=_discovered;}
        var options=t switch {
            ActionType.MediaControl=>Pairs("playpause","Reproduzir / pausar","next","Próxima faixa","previous","Faixa anterior","volumeup","Aumentar volume","volumedown","Diminuir volume","mute","Silenciar áudio"),
            ActionType.SystemAction=>Pairs("explorer","Explorador de Arquivos","settings","Configurações do Windows","lock","Bloquear sessão","screenshot","Capturar tela"),
            ActionType.WebSearch=>Pairs("google","Google","bing","Bing","duckduckgo","DuckDuckGo"),
            ActionType.SwitchProfile=>App.ConfigStore.Config.Profiles.Select(p=>new KeyValuePair<string,string>(p.Id,p.Name)).ToList(),_=>[]};
        _choiceKey=t==ActionType.WebSearch?"engine":_parameterKey;
        _choice.ItemsSource=options;_choice.SelectedValue=Action.Parameters.GetValueOrDefault(_choiceKey,options.FirstOrDefault().Key??"");
        Show(_choice,options.Count>0);Show(_parameter,t is not (ActionType.MediaControl or ActionType.SystemAction or ActionType.SwitchProfile or ActionType.OpenApplication));
        Show(_label,t!=ActionType.OpenApplication);
        _hint.Text=t switch {
            ActionType.SendHotkey=>"Clique em Gravar atalho e pressione a combinação. Escape cancela a gravação. Também aceita CTRL+SHIFT+S, F1, TAB e teclas individuais.",
            ActionType.TypeText=>"Espaços e quebras de linha são preservados. O texto será digitado na janela ativa; marque a opção acima para enviar Enter ao final.",
            ActionType.MultiAction=>"Etapas em ordem. Para parar um loop, pressione a mesma tecla novamente ou use Parar sequências. Trocar o perfil ou desativar a captura também interrompe a execução.",
            ActionType.Delay=>"Aguarda o tempo indicado sem bloquear o aplicativo. Exemplo: 1000 ms = 1 segundo.",
            _=>"O teste começa após 3 segundos: mude para a janela desejada. Fechar este editor cancela o teste pendente."};
        RefreshSteps();_loading=false;
    }
    private void Collect()
    {
        Action.Name=string.IsNullOrWhiteSpace(_name.Text)?Label(Action.Type):_name.Text.Trim();
        Action.Parameters[_parameterKey]=Action.Type==ActionType.TypeText?_parameter.Text:_parameter.Text.Trim();
        if(_choice.Visibility==Visibility.Visible&&_choice.SelectedValue is string selected)Action.Parameters[_choiceKey]=selected;
        Action.Parameters["appendEnter"]=_enter.IsChecked==true?"true":"false";Action.Parameters["delayMs"]=_delay.Text.Trim();
        Action.Repeat.Mode=_repeatMode.SelectedValue is SequenceRepeatMode mode?mode:SequenceRepeatMode.Once;
        Action.Repeat.Count=Action.Repeat.Mode==SequenceRepeatMode.Count?(int.TryParse(_repeatCount.Text,out int count)?count:-1):1;
        Action.Repeat.IntervalMs=Action.Repeat.Mode==SequenceRepeatMode.Once?0:(int.TryParse(_repeatInterval.Text,out int interval)?interval:-1);
    }
    private void Browse(object sender,RoutedEventArgs e)
    {
        if(Action.Type==ActionType.OpenFolder){var dialog=new Microsoft.Win32.OpenFolderDialog();if(dialog.ShowDialog(this)==true)_parameter.Text=dialog.FolderName;}
        else{var dialog=new Microsoft.Win32.OpenFileDialog{Filter=Action.Type==ActionType.OpenApplication?"Aplicativos (*.exe;*.lnk)|*.exe;*.lnk":"Todos os arquivos|*.*"};if(dialog.ShowDialog(this)==true){_parameter.Text=dialog.FileName;_name.Text=Path.GetFileNameWithoutExtension(dialog.FileName);}}
    }
    private void Record(object sender,KeyEventArgs e)
    {
        if(!_recording)return;e.Handled=true;var key=e.Key==Key.System?e.SystemKey:e.Key;
        if(key==Key.Escape){_recording=false;_record.Content="Gravar atalho";return;}
        if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)return;
        var parts=new List<string>();var mods=Keyboard.Modifiers;
        if(mods.HasFlag(ModifierKeys.Control))parts.Add("CTRL");if(mods.HasFlag(ModifierKeys.Alt))parts.Add("ALT");
        if(mods.HasFlag(ModifierKeys.Shift))parts.Add("SHIFT");if(mods.HasFlag(ModifierKeys.Windows))parts.Add("WIN");
        var vk=KeyInterop.VirtualKeyFromKey(key);
        parts.Add(vk is >=65 and <=90 or >=48 and <=57?((char)vk).ToString():vk is >=112 and <=135?"F"+(vk-111):key switch {Key.Return=>"ENTER",Key.Tab=>"TAB",Key.Space=>"SPACE",Key.Back=>"BACKSPACE",_=>"VK_"+vk.ToString("X2")});
        _parameter.Text=string.Join("+",parts);_recording=false;_record.Content="Gravar atalho";
    }
    private void RefreshSteps(){int i=_steps.SelectedIndex;_steps.ItemsSource=Action.Steps.Select((s,n)=>$"{n+1:00}   {s.Name}  ·  {(s.Type==ActionType.Delay?s.Parameters.GetValueOrDefault("milliseconds","0")+" ms":Label(s.Type))}").ToList();_steps.SelectedIndex=Math.Min(i,Action.Steps.Count-1);}
    private void EditStep(bool add)
    {
        int i=_steps.SelectedIndex;if(!add&&i<0)return;if(add&&Action.Steps.Count>=32){_result.Text="Limite de 32 passos.";return;}
        var editor=new ActionEditorWindow(add?new ActionDefinition{Type=ActionType.OpenApplication,Name="Abrir aplicativo"}:Action.Steps[i],true){Owner=this};
        if(editor.ShowDialog()==true){if(add)Action.Steps.Add(editor.Action);else Action.Steps[i]=editor.Action;RefreshSteps();}
    }
    private void Move(int delta){int i=_steps.SelectedIndex,j=i+delta;if(i<0||j<0||j>=Action.Steps.Count)return;(Action.Steps[i],Action.Steps[j])=(Action.Steps[j],Action.Steps[i]);RefreshSteps();_steps.SelectedIndex=j;}
    private async void Test(object sender,RoutedEventArgs e)
    {
        if(_testRun is not null){_testRun.Cancel();return;}
        Collect();if(ActionValidator.Validate(Action) is string error){_result.Text=error;return;}
        _testRun=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _test.Content="Parar teste";_result.Text="Mude para a janela de destino. Teste em 3 segundos…";
        var snapshot=Copy(Action);var token=_testRun.Token;
        try{await Task.Delay(3000,token);var r=await Task.Run(()=>App.ActionExecutor.ExecuteAsync(snapshot,token));_result.Text=r.Message;}
        catch(OperationCanceledException){_result.Text="Teste interrompido";}
        finally{_testRun.Dispose();_testRun=null;_test.Content="Testar em 3 segundos";}
    }
    private void Save(object sender,RoutedEventArgs e){Collect();if(ActionValidator.Validate(Action) is string error){_result.Text=error;return;}DialogResult=true;}
}
