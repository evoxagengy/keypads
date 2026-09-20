using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SwitchKeypad.Core.Models;
using SwitchKeypad.UI.Controls;
using SwitchKeypad.Windows.Interception;
using SwitchKeypad.Windows.Shell;

namespace SwitchKeypad;

public partial class MainWindow : Window
{
    private DeviceDefinition? _selectedDevice;
    private string _selectedKey = "0";
    private bool _identifyMode;
    private DeviceLayoutDefinition _activeLayout = DeviceLayoutCatalog.LegacyTemplate("");
    private int _iconGeneration;
    private DateTimeOffset _lastNumLockToggle=DateTimeOffset.MinValue;
    private readonly Dictionary<string,BurstState> _bursts=new();
    private readonly Core.Actions.ExecutionSessions _sessions=new();
    private readonly IInterceptionProvider _interception = new InterceptionProvider();
    private readonly HashSet<int> _pressedScans = [];
    private static readonly SolidColorBrush SuccessBrush = new(Color.FromRgb(32,217,148));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(248,179,60));
    private static readonly SolidColorBrush DisabledBrush = new(Color.FromRgb(118,139,158));

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += Window_SourceInitialized;
        App.RawInput.KeyEvent += RawInput_KeyEvent;
        App.RawInput.DevicesChanged += (_,_) => Dispatcher.Invoke(RefreshDeviceDisplay);
        _interception.KeyEvent += Interception_KeyEvent;
        App.ActionExecutor.SelectProfile=id=>Dispatcher.Invoke(()=>{if(!App.ConfigStore.Config.Profiles.Any(p=>p.Id==id))return false;App.ConfigStore.Config.ActiveProfileId=id;SyncProfile();return true;});
    }

    private void Window_SourceInitialized(object? sender,EventArgs e)
    {
        var handle=new WindowInteropHelper(this).Handle;
        App.RawInput.Attach(handle);
        HwndSource.FromHwnd(handle)?.AddHook(WindowMessageHook);
    }

    private nint WindowMessageHook(nint hwnd,int msg,nint wParam,nint lParam,ref bool handled)
    {
        const int WmGetMinMaxInfo=0x0024;
        if (msg==WmGetMinMaxInfo)
        {
            ApplyWorkingArea(hwnd,lParam);
            handled=true;
        }
        return 0;
    }

    private static void ApplyWorkingArea(nint hwnd,nint lParam)
    {
        const uint MonitorDefaultToNearest=2;
        var monitor=MonitorFromWindow(hwnd,MonitorDefaultToNearest);
        if (monitor==0) return;
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if (!GetMonitorInfo(monitor,ref info)) return;
        var data=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        data.MaxPosition.X=Math.Abs(info.WorkArea.Left-info.MonitorArea.Left);
        data.MaxPosition.Y=Math.Abs(info.WorkArea.Top-info.MonitorArea.Top);
        data.MaxSize.X=Math.Abs(info.WorkArea.Right-info.WorkArea.Left);
        data.MaxSize.Y=Math.Abs(info.WorkArea.Bottom-info.WorkArea.Top);
        data.MaxTrackSize=data.MaxSize;
        Marshal.StructureToPtr(data,lParam,true);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        StartupCheck.IsChecked = StartupService.IsEnabled();
        HeaderProfileName.Text = LeftProfileName.Text = App.ConfigStore.ActiveProfile.Name;
        RefreshDeviceDisplay();
        RenderKeypad();
        RefreshKeyTiles();
        UpdateSelectedAction();
        if (Environment.GetCommandLineArgs().Any(a=>a.Equals("--minimized",StringComparison.OrdinalIgnoreCase))) Hide();
        UpdateFooter();
    }

    private void RefreshDeviceDisplay()
    {
        var devices = App.RawInput.EnumerateKeyboards();
        if (App.ConfigStore.Config.SelectedDeviceFingerprint is { Length:>0 } fp)
            _selectedDevice = devices.FirstOrDefault(d=>d.Fingerprint==fp);
        if(_selectedDevice is not null)
        {
            var saved=App.ConfigStore.Config.Devices.FirstOrDefault(x=>x.Fingerprint==_selectedDevice.Fingerprint);
            if(!string.IsNullOrWhiteSpace(saved?.CustomName))_selectedDevice.CustomName=saved.CustomName;
        }
        _activeLayout = DeviceLayoutCatalog.Resolve(App.ConfigStore.Config,_selectedDevice);
        if (_selectedDevice is null)
        {
            _interception.Stop();
            _sessions.CancelAll();
            lock(_pressedScans)_pressedScans.Clear();
            HeaderDeviceName.Text="Selecione um dispositivo"; HeaderDeviceIds.Text=$"{devices.Count} teclado(s) detectado(s)";
            DeviceNameText.Text="Nenhum dispositivo"; DeviceStatusText.Text="Aguardando seleção"; DeviceStatusDot.Fill=WarningBrush;
            DeviceDetailText.Text="VID: —\nPID: —\nSerial: —\nProduto: —\nFabricante: —"; HeaderStatusText.Text="Selecione um teclado";
        }
        else
        {
            HeaderDeviceName.Text=DisplayDeviceName(_selectedDevice); HeaderDeviceIds.Text=$"VID: {_selectedDevice.Vid ?? "----"}    PID: {_selectedDevice.Pid ?? "----"}";
            DeviceNameText.Text=DisplayDeviceName(_selectedDevice); DeviceStatusText.Text="Dispositivo conectado"; DeviceStatusDot.Fill=SuccessBrush;
            DeviceDetailText.Text=$"VID: {_selectedDevice.Vid ?? "—"}\nPID: {_selectedDevice.Pid ?? "—"}\nProduto: {_selectedDevice.Product ?? "Não informado"}\nFabricante: {_selectedDevice.Manufacturer ?? "Não informado"}";
            DeviceDetailText.ToolTip=_selectedDevice.InstanceId+"\n"+_selectedDevice.Location;
            HeaderStatusText.Text="Dispositivo Conectado e Ativo";
            if (App.ConfigStore.Config.Enabled && _interception.State==InterceptionState.Ready) _interception.TryStart(_selectedDevice,ShouldConsume);
        }
        RenderKeypad();
        UpdateRunState();
    }

    private string? ResolvePhysicalKey(DeviceKeyEvent ev)
        => DeviceLayoutCatalog.ResolveKey(_activeLayout,ev)?.Id ?? PhysicalKeyMap.Resolve(ev);

    private bool ShouldConsume(DeviceKeyEvent ev)
    {
        if (!App.ConfigStore.Config.Enabled || _identifyMode || _selectedDevice is null) return false;
        if(DeviceLayoutCatalog.IsNumLock(ev))return false;
        var candidates=DeviceLayoutCatalog.ResolveCandidates(_activeLayout,ev);
        if(candidates.Count>0)
            return candidates.Any(k=>
            {
                var mapped=FindMapping(k.Id);
                return mapped is not null && mapped.Action.Type is not (ActionType.PassThrough or ActionType.Disabled);
            });
        var mapping = ResolvePhysicalKey(ev) is string key?FindMapping(key):null;
        return mapping is not null && mapping.Action.Type is not (ActionType.PassThrough or ActionType.Disabled);
    }

    private async void Interception_KeyEvent(object? sender, DeviceKeyEvent e)
    {
        if(!Dispatcher.CheckAccess()){_ = Dispatcher.BeginInvoke(()=>Interception_KeyEvent(sender,e));return;}
        if (_selectedDevice is null || e.DeviceId!=_selectedDevice.Fingerprint) return;

        if(DeviceLayoutCatalog.IsNumLock(e))
        {
            if(e.IsKeyDown&&!_identifyMode)ToggleEnabledFromNumLock();
            return;
        }

        if (!e.IsKeyDown) { lock(_pressedScans) _pressedScans.Remove(e.ScanCode); return; }
        lock(_pressedScans) if(!_pressedScans.Add(e.ScanCode)) return;

        var candidates=DeviceLayoutCatalog.ResolveCandidates(_activeLayout,e);
        if(candidates.Count>1&&candidates.Any(k=>k.PressCount>1))
        {
            QueueBurst(e);
            return;
        }

        if(ResolvePhysicalKey(e) is string physicalKey)
        {
            SelectKey(physicalKey);
            await ExecuteMappedKeyAsync(physicalKey);
        }
    }

    private void QueueBurst(DeviceKeyEvent e)
    {
        var id=$"{e.ScanCode}:{e.IsExtended}";
        if(!_bursts.TryGetValue(id,out var state))
        {
            state=new BurstState{Event=e};
            _bursts[id]=state;
        }
        state.Count++;
        state.Cancellation?.Cancel();
        state.Cancellation?.Dispose();
        state.Cancellation=new CancellationTokenSource();
        var token=state.Cancellation.Token;
        _=Task.Run(async()=>
        {
            try
            {
                await Task.Delay(180,token);
                await Dispatcher.InvokeAsync(async()=>
                {
                    if(!_bursts.Remove(id,out var current))return;
                    current.Cancellation?.Dispose();
                    var key=DeviceLayoutCatalog.ResolveBurst(_activeLayout,current.Event,current.Count);
                    if(key is null)return;
                    SelectKey(key.Id);
                    await ExecuteMappedKeyAsync(key.Id);
                });
            }
            catch(OperationCanceledException){}
        });
    }

    private async Task ExecuteMappedKeyAsync(string physicalKey)
    {
        var mapping=FindMapping(physicalKey);
        if(mapping is null || !App.ConfigStore.Config.Enabled || _identifyMode)return;
        var profileId=App.ConfigStore.ActiveProfile.Id;
        FooterStatus.Text=$"Tecla {KeyDisplayName(physicalKey)} — executando";
        var r=await _sessions.TriggerAsync(profileId,mapping.PhysicalKey,ActionEditorWindow.Copy(mapping.Action),App.ActionExecutor);
        if(App.ConfigStore.ActiveProfile.Id==profileId)FooterStatus.Text=$"Tecla {KeyDisplayName(physicalKey)} — {r.Message}";
    }

    private void RawInput_KeyEvent(object? sender, DeviceKeyEvent e)
    {
        if(!Dispatcher.CheckAccess()){_ = Dispatcher.BeginInvoke(()=>RawInput_KeyEvent(sender,e));return;}
        if (_selectedDevice is null || e.DeviceId != _selectedDevice.Fingerprint) return;

        if(DeviceLayoutCatalog.IsNumLock(e))
        {
            if(e.IsKeyDown&&!_identifyMode&&_interception.State!=InterceptionState.Running)ToggleEnabledFromNumLock();
            return;
        }

        if(e.IsKeyDown && ResolvePhysicalKey(e) is string key) SelectKey(key);
        // Raw Input identifies the source device but does not selectively suppress the original keystroke.
        // Physical mappings execute only through the exclusive provider.
    }

    private KeyMapping? FindMappingByScan(int scan) => App.ConfigStore.ActiveProfile.Mappings.FirstOrDefault(m=>m.ScanCode==scan);
    private KeyMapping? FindMapping(string key) => App.ConfigStore.ActiveProfile.Mappings.FirstOrDefault(m=>m.PhysicalKey==key);

    private void DeviceButton_Click(object sender,RoutedEventArgs e)
    {
        var devices=App.RawInput.EnumerateKeyboards();
        var menu=new ContextMenu{Style=(Style)FindResource(typeof(ContextMenu))};
        if(devices.Count==0) menu.Items.Add(new MenuItem{Header="Nenhum teclado detectado",IsEnabled=false});
        foreach(var d in devices)
        {
            var label=new StackPanel();
            label.Children.Add(new TextBlock{Text=DisplayDeviceName(d),FontWeight=FontWeights.SemiBold});
            label.Children.Add(new TextBlock{Text=$"VID {d.Vid ?? "—"} · PID {d.Pid ?? "—"} · interface {d.Fingerprint[..6]}",FontSize=11,Foreground=Brushes.LightSteelBlue,Margin=new Thickness(0,4,0,0)});
            var mi=new MenuItem{Header=label,Tag=d,ToolTip=d.InstanceId+"\n"+d.Location};
            mi.Click += (_,_)=>SelectDevice((DeviceDefinition)mi.Tag); menu.Items.Add(mi);
        }
        menu.Items.Add(new Separator());
        var identify=new MenuItem{Header="Identificar pressionando uma tecla…"}; identify.Click += (_,_)=>BeginIdentify(); menu.Items.Add(identify);
        menu.PlacementTarget=(UIElement)sender; menu.IsOpen=true;
    }

    private void BeginIdentify()
    {
        FooterStatus.Text="Pressione uma tecla no teclado que deseja usar…";
        EventHandler<DeviceKeyEvent>? handler=null;
        handler=(s,e)=>
        {
            if(!e.IsKeyDown) return;
            App.RawInput.KeyEvent-=handler;
            Dispatcher.Invoke(()=>
            {
                var d=App.RawInput.EnumerateKeyboards().FirstOrDefault(x=>x.Fingerprint==e.DeviceId);
                if(d is not null) SelectDevice(d); else FooterStatus.Text="Não foi possível identificar o dispositivo.";
            });
        };
        App.RawInput.KeyEvent+=handler;
    }
    private void SelectDevice(DeviceDefinition d)
    {
        _sessions.CancelAll();_pressedScans.Clear();_interception.Stop();
        var saved=App.ConfigStore.Config.Devices.FirstOrDefault(x=>x.Fingerprint==d.Fingerprint);
        if(!string.IsNullOrWhiteSpace(saved?.CustomName))d.CustomName=saved.CustomName;
        _selectedDevice=d;
        App.ConfigStore.Config.SelectedDeviceFingerprint=d.Fingerprint;
        App.ConfigStore.Config.Devices.RemoveAll(x=>x.Fingerprint==d.Fingerprint);
        App.ConfigStore.Config.Devices.Add(d);
        App.ConfigStore.Save();
        RefreshDeviceDisplay();
    }

    private static string DisplayDeviceName(DeviceDefinition d)
        => string.IsNullOrWhiteSpace(d.CustomName)?d.FriendlyName:d.CustomName!;

    private void RenameDevice_Click(object sender,RoutedEventArgs e)
    {
        if(_selectedDevice is null){FooterStatus.Text="Selecione um dispositivo antes de renomear.";return;}
        var input=new TextBox{Text=DisplayDeviceName(_selectedDevice),MaxLength=80};
        var dialog=UI.ModalChrome.Create(this,"Renomear dispositivo","Use um nome fácil de reconhecer. O nome técnico e o fingerprint continuam preservados.",input,"Salvar nome",()=>
        {
            var value=input.Text.Trim();
            if(string.IsNullOrWhiteSpace(value))return false;
            _selectedDevice.CustomName=value;
            var saved=App.ConfigStore.Config.Devices.FirstOrDefault(x=>x.Fingerprint==_selectedDevice.Fingerprint);
            if(saved is null){saved=_selectedDevice;App.ConfigStore.Config.Devices.Add(saved);}
            saved.CustomName=value;
            _activeLayout.Name=value;
            App.ConfigStore.Save();
            RefreshDeviceDisplay();
            return true;
        });
        dialog.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
        dialog.ShowDialog();
    }

    private void Key_Click(object sender,RoutedEventArgs e)
    {
        if(sender is FrameworkElement { Tag: string key }) SelectKey(key);
    }
    private void SelectKey(string key)
    {
        if(DeviceLayoutCatalog.GetKey(_activeLayout,key) is null && !PhysicalKeyMap.ScanCodes.ContainsKey(key))return;
        _selectedKey=key;
        foreach(var tile in FindVisualChildren<KeyTileControl>(KeypadGrid)) tile.IsSelectedKey=Equals(tile.Tag,_selectedKey);
        SelectedKeyText.Text=KeyDisplayName(_selectedKey); UpdateSelectedAction();
    }
    private string KeyDisplayName(string id)
        => DeviceLayoutCatalog.GetKey(_activeLayout,id)?.Label is { Length:>0 } label?label:id;

    private bool IsReservedKey(string id)
        => DeviceLayoutCatalog.GetKey(_activeLayout,id)?.ReservedToggle==true;

    private void UpdateSelectedAction()
    {
        if(IsReservedKey(_selectedKey))
        {
            CurrentActionTitle.Text="Ativar / Desativar Switch Keypad";
            CurrentActionSubtitle.Text="Num Lock é reservado e mantém sua função normal no Windows.";
            return;
        }
        var m=FindMapping(_selectedKey);
        CurrentActionTitle.Text=m?.Action.Name ?? "Sem ação";
        CurrentActionSubtitle.Text=m is null?"Sem ação":ActionEditorWindow.Label(m.Action.Type);
    }

    private void ClearKey_Click(object sender,RoutedEventArgs e)
    {
        if(IsReservedKey(_selectedKey)){FooterStatus.Text="Num Lock é reservado para ligar/desligar o Switch Keypad.";return;}
        var m=FindMapping(_selectedKey); if(m is null) return; m.Action=new ActionDefinition{Type=ActionType.Disabled,Name="Sem ação"}; UpdateSelectedAction(); RefreshKeyTiles();
    }
    private KeyMapping EnsureMapping()
    {
        var mapping=FindMapping(_selectedKey);if(mapping is not null)return mapping;
        var definition=DeviceLayoutCatalog.GetKey(_activeLayout,_selectedKey);
        mapping=new KeyMapping
        {
            PhysicalKey=_selectedKey,
            ScanCode=definition?.ScanCode ?? PhysicalKeyMap.ScanCodes.GetValueOrDefault(_selectedKey),
            VirtualKey=definition?.VirtualKey ?? 0,
            IsExtended=definition?.IsExtended ?? (_selectedKey is "/" or "Enter"),
            Action=new()
        };
        App.ConfigStore.ActiveProfile.Mappings.Add(mapping);return mapping;
    }
    private void ChangeAction_Click(object sender,RoutedEventArgs e)
    {
        if(IsReservedKey(_selectedKey)){FooterStatus.Text="Num Lock é reservado para ligar/desligar o Switch Keypad.";return;}
        ShowActionEditor(EnsureMapping());
    }
    private void ActionType_Click(object sender,RoutedEventArgs e)
    {
        if(sender is Button b && Enum.TryParse<ActionType>((string)b.Tag,out var t))
        {
            if(IsReservedKey(_selectedKey)){FooterStatus.Text="Num Lock é reservado para ligar/desligar o Switch Keypad.";return;}
            var mapping=EnsureMapping();
            var candidate=mapping.Action.Type==t?mapping.Action:new ActionDefinition{Type=t,Name=ActionEditorWindow.Label(t)};
            var dialog=new ActionEditorWindow(candidate){Owner=this};
            if(dialog.ShowDialog()==true){mapping.Action=dialog.Action;App.ConfigStore.Save();UpdateSelectedAction();RefreshKeyTiles();}
        }
    }

    private void ShowActionEditor(KeyMapping? m)
    {
        if(m is null) return;
        var dlg=new ActionEditorWindow(m.Action){Owner=this};
        if(dlg.ShowDialog()==true){m.Action=dlg.Action;UpdateSelectedAction();RefreshKeyTiles();App.ConfigStore.Save();}
    }

    private void Save_Click(object sender,RoutedEventArgs e){App.ConfigStore.Save();FooterStatus.Text="Alterações salvas";}
    private void StartupCheck_Changed(object sender,RoutedEventArgs e){if(!IsLoaded)return;StartupService.SetEnabled(StartupCheck.IsChecked==true);App.ConfigStore.Config.StartWithWindows=StartupCheck.IsChecked==true;App.ConfigStore.Save();}
    private void TestMode_Click(object sender,RoutedEventArgs e)
    {
        if(_selectedDevice is null){FooterStatus.Text="Selecione um teclado antes de identificar as teclas.";return;}
        _sessions.CancelAll();lock(_pressedScans)_pressedScans.Clear();CancelBursts();
        _identifyMode=true;_interception.Stop();UpdateRunState();

        var previous=_activeLayout;
        var dialog=new DeviceLayoutWizardWindow(_selectedDevice,previous){Owner=this};
        var accepted=dialog.ShowDialog()==true;
        _identifyMode=false;

        if(accepted)
        {
            var updated=dialog.Layout;
            MigrateMappings(previous,updated);
            App.ConfigStore.Config.DeviceLayouts.RemoveAll(x=>x.DeviceFingerprint==_selectedDevice.Fingerprint);
            App.ConfigStore.Config.DeviceLayouts.Add(updated);
            _activeLayout=updated;
            App.ConfigStore.Save();
            RenderKeypad();RefreshKeyTiles();
            FooterStatus.Text=updated.Confirmed?"Layout identificado e salvo.":"Layout salvo parcialmente — você pode continuar a identificação depois.";
        }
        RefreshDeviceDisplay();
    }

    private void MigrateMappings(DeviceLayoutDefinition previous,DeviceLayoutDefinition updated)
    {
        foreach(var profile in App.ConfigStore.Config.Profiles)
        foreach(var mapping in profile.Mappings)
        {
            var old=DeviceLayoutCatalog.GetKey(previous,mapping.PhysicalKey)
                    ?? previous.Keys.FirstOrDefault(k=>k.ScanCode==mapping.ScanCode&&k.IsExtended==mapping.IsExtended);
            if(old is null)continue;
            var replacement=updated.Keys.FirstOrDefault(k=>k.ScanCode==old.ScanCode&&k.IsExtended==old.IsExtended&&Math.Max(1,k.PressCount)==Math.Max(1,old.PressCount))
                            ?? updated.Keys.FirstOrDefault(k=>string.Equals(k.Label,old.Label,StringComparison.OrdinalIgnoreCase));
            if(replacement is null)continue;
            mapping.PhysicalKey=replacement.Id;mapping.ScanCode=replacement.ScanCode;mapping.VirtualKey=replacement.VirtualKey;mapping.IsExtended=replacement.IsExtended;
        }
    }

    private void ToggleEnabled_Click(object sender,RoutedEventArgs e)
        => SetEnabled(!App.ConfigStore.Config.Enabled,"Controle da interface");

    private void ToggleEnabledFromNumLock()
    {
        var now=DateTimeOffset.UtcNow;
        if((now-_lastNumLockToggle).TotalMilliseconds<250)return;
        _lastNumLockToggle=now;
        SetEnabled(!App.ConfigStore.Config.Enabled,"Num Lock");
    }

    private void SetEnabled(bool enabled,string source)
    {
        App.ConfigStore.Config.Enabled=enabled;
        _sessions.CancelAll();lock(_pressedScans)_pressedScans.Clear();CancelBursts();
        if(!enabled)_interception.Stop();
        else if(_selectedDevice is not null&&_interception.State==InterceptionState.Ready)_interception.TryStart(_selectedDevice,ShouldConsume);
        App.ConfigStore.Save();
        UpdateRunState();
        FooterStatus.Text=enabled?$"Switch Keypad ativado por {source}.":$"Switch Keypad desativado por {source}. Num Lock continua funcionando normalmente.";
    }

    private void CancelBursts()
    {
        foreach(var state in _bursts.Values){state.Cancellation?.Cancel();state.Cancellation?.Dispose();}
        _bursts.Clear();
    }

    private void NewProfile_Click(object sender,RoutedEventArgs e){App.ConfigStore.CreateProfile();SyncProfile();}
    private void DuplicateProfile_Click(object sender,RoutedEventArgs e){var src=App.ConfigStore.ActiveProfile;var p=new ProfileDefinition{Name=src.Name+" - Cópia",Mappings=src.Mappings.Select(CloneMapping).ToList()};App.ConfigStore.Config.Profiles.Add(p);App.ConfigStore.Config.ActiveProfileId=p.Id;SyncProfile();}
    private static KeyMapping CloneMapping(KeyMapping m)=>new(){PhysicalKey=m.PhysicalKey,ScanCode=m.ScanCode,VirtualKey=m.VirtualKey,IsExtended=m.IsExtended,Trigger=m.Trigger,Repeat=m.Repeat,Action=ActionEditorWindow.Copy(m.Action)};
    private void ProfileMenu_Click(object sender,RoutedEventArgs e){var menu=new ContextMenu();foreach(var p in App.ConfigStore.Config.Profiles){var mi=new MenuItem{Header=p.Name,Tag=p};mi.Click+=(_,_)=>{App.ConfigStore.Config.ActiveProfileId=p.Id;SyncProfile();};menu.Items.Add(mi);}menu.PlacementTarget=(UIElement)sender;menu.IsOpen=true;}
    private void SyncProfile(){_sessions.CancelAll();HeaderProfileName.Text=LeftProfileName.Text=App.ConfigStore.ActiveProfile.Name;App.ConfigStore.Save();UpdateSelectedAction();RefreshKeyTiles();}
    private void ProfileMore_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu{PlacementTarget=(UIElement)sender};
        var rename=new MenuItem{Header="Renomear perfil"};rename.Click+=(_,_)=>RenameProfile();
        var delete=new MenuItem{Header="Excluir perfil",Foreground=UI.ModalChrome.Brush("#FF8FA3")};delete.Click+=(_,_)=>DeleteProfile();
        menu.Items.Add(rename);menu.Items.Add(delete);menu.IsOpen=true;
    }
    private void RenameProfile()
    {
        var profile=App.ConfigStore.ActiveProfile;
        var panel=new StackPanel();
        var name=new TextBox{Text=profile.Name,MaxLength=100};
        var error=new TextBlock{Foreground=Brushes.LightSalmon,Margin=new Thickness(0,8,0,0)};
        panel.Children.Add(name);panel.Children.Add(error);
        var dialog=UI.ModalChrome.Create(this,"Renomear perfil","Altere o nome sem modificar as ações das teclas.",panel,"Salvar nome",()=>
        {
            if(string.IsNullOrWhiteSpace(name.Text)){error.Text="Informe um nome para o perfil.";name.Focus();return false;}
            App.ConfigStore.RenameProfile(profile.Id,name.Text);SyncProfile();return true;
        });
        dialog.Loaded+=(_,_)=>{name.Focus();name.SelectAll();};dialog.ShowDialog();
    }
    private void DeleteProfile()
    {
        var profile=App.ConfigStore.ActiveProfile;
        var note=new TextBlock{Text=App.ConfigStore.Config.Profiles.Count==1?"Um novo Perfil 1 vazio será criado.":"Os outros perfis serão mantidos.",Foreground=Brushes.LightSteelBlue};
        UI.ModalChrome.Create(this,"Excluir perfil?",$"Excluir “{profile.Name}” e suas configurações de teclas? Esta ação não pode ser desfeita.",note,"Excluir",()=>
        {
            _sessions.CancelAll();App.ConfigStore.DeleteProfile(profile.Id);SyncProfile();return true;
        },true).ShowDialog();
    }
    private void ImportProfile_Click(object sender,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Switch Keypad Profile (*.json)|*.json"};if(d.ShowDialog()==true){try{var p=App.ConfigStore.ImportProfile(d.FileName);App.ConfigStore.Config.ActiveProfileId=p.Id;SyncProfile();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Importação",MessageBoxButton.OK,MessageBoxImage.Warning);}}}
    private void ExportProfile_Click(object sender,RoutedEventArgs e){var d=new SaveFileDialog{Filter="Switch Keypad Profile (*.json)|*.json",FileName=$"{App.ConfigStore.ActiveProfile.Name}.switchkeypad.json"};if(d.ShowDialog()==true){App.ConfigStore.ExportProfile(App.ConfigStore.ActiveProfile,d.FileName);FooterStatus.Text="Perfil exportado";}}

    private void Settings_Click(object sender,RoutedEventArgs e){new SettingsWindow(_interception){Owner=this}.ShowDialog();UpdateRunState();}
    private void TabKeys_Click(object sender,RoutedEventArgs e)=>FooterStatus.Text="Teclas";
    private void TabActions_Click(object sender,RoutedEventArgs e)
    {
        ActionScroll.ScrollToTop();
        if(IsReservedKey(_selectedKey)){FooterStatus.Text="Num Lock é reservado para ligar/desligar o Switch Keypad.";return;}
        ShowActionEditor(EnsureMapping());
    }
    private void TabProfiles_Click(object sender,RoutedEventArgs e)=>ProfileMenu_Click(sender,e);
    private void TabDevice_Click(object sender,RoutedEventArgs e)=>DeviceButton_Click(sender,e);

    private void UpdateFooter() => UpdateRunState();

    private void UpdateRunState()
    {
        var greenBg=new SolidColorBrush(Color.FromRgb(6,46,40));
        var greenBorder=new SolidColorBrush(Color.FromRgb(12,96,79));
        var amberBg=new SolidColorBrush(Color.FromRgb(58,45,20));
        var amberBorder=new SolidColorBrush(Color.FromRgb(116,89,32));
        var grayBg=new SolidColorBrush(Color.FromRgb(27,40,52));
        var grayBorder=new SolidColorBrush(Color.FromRgb(55,75,92));

        if (_selectedDevice is null)
        {
            HeaderStatusText.Text="Selecione um teclado"; HeaderStatusText.Foreground=WarningBrush; HeaderStatusDot.Fill=WarningBrush;
            RunStateText.Text="Aguardando"; RunStateText.Foreground=WarningBrush; RunStateIcon.Foreground=WarningBrush; RunStateButton.Background=amberBg; RunStateButton.BorderBrush=amberBorder;
            FooterStatus.Text="Selecione qualquer teclado conectado"; FooterStatusDot.Fill=WarningBrush;
        }
        else if (!App.ConfigStore.Config.Enabled)
        {
            HeaderStatusText.Text="Captura desativada"; HeaderStatusText.Foreground=DisabledBrush; HeaderStatusDot.Fill=DisabledBrush;
            RunStateText.Text="Desativado"; RunStateText.Foreground=DisabledBrush; RunStateIcon.Foreground=DisabledBrush; RunStateButton.Background=grayBg; RunStateButton.BorderBrush=grayBorder;
            FooterStatus.Text="Captura desativada — o teclado funciona normalmente"; FooterStatusDot.Fill=DisabledBrush;
        }
        else if (_interception.State==InterceptionState.Running)
        {
            HeaderStatusText.Text="Dispositivo conectado e ativo"; HeaderStatusText.Foreground=SuccessBrush; HeaderStatusDot.Fill=SuccessBrush;
            RunStateText.Text="Em Execução"; RunStateText.Foreground=SuccessBrush; RunStateIcon.Foreground=SuccessBrush; RunStateButton.Background=greenBg; RunStateButton.BorderBrush=greenBorder;
            FooterStatus.Text="Modo exclusivo ativo • somente o dispositivo selecionado é capturado"; FooterStatusDot.Fill=SuccessBrush;
        }
        else
        {
            HeaderStatusText.Text="Conectado — exclusivo indisponível"; HeaderStatusText.Foreground=WarningBrush; HeaderStatusDot.Fill=WarningBrush;
            RunStateText.Text=_identifyMode?"Identificando":"Monitorando"; RunStateText.Foreground=WarningBrush; RunStateIcon.Foreground=WarningBrush; RunStateButton.Background=amberBg; RunStateButton.BorderBrush=amberBorder;
            FooterStatus.Text=_identifyMode?"Identificação de teclas ativa":$"Ações físicas aguardando modo exclusivo • {_interception.StatusText}"; FooterStatusDot.Fill=WarningBrush;
        }
    }

    private void RenderKeypad()
    {
        if(KeypadGrid is null) return;
        KeypadGrid.Children.Clear();
        KeypadGrid.RowDefinitions.Clear();
        KeypadGrid.ColumnDefinitions.Clear();

        var columns=Math.Max(1,_activeLayout.Columns);
        var rows=Math.Max(1,_activeLayout.Keys.Count==0?5:_activeLayout.Keys.Max(k=>k.Row+Math.Max(1,k.RowSpan)));
        for(int i=0;i<rows;i++)KeypadGrid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(120)});
        for(int i=0;i<columns;i++)KeypadGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(120)});
        KeypadGrid.Width=Math.Max(500,columns*120);
        KeypadGrid.Height=Math.Max(620,rows*120);

        foreach(var key in _activeLayout.Keys.OrderBy(k=>k.Row).ThenBy(k=>k.Column))
        {
            var tile=new KeyTileControl
            {
                KeyLabel=string.IsNullOrWhiteSpace(key.Label)?key.Id:key.Label,
                Tag=key.Id,
                Margin=new Thickness(4),
                IsSelectedKey=string.Equals(key.Id,_selectedKey,StringComparison.OrdinalIgnoreCase),
                Shape=key.RowSpan>1?"vertical":key.ColumnSpan>1?"horizontal":"square",
                ActionLabel=key.ReservedToggle?"Ativar / Desativar":(FindMapping(key.Id)?.Action.Name ?? (key.ScanCode<0?"Não identificado":"Sem ação"))
            };
            tile.Click+=Key_Click;
            Grid.SetRow(tile,Math.Max(0,key.Row));
            Grid.SetColumn(tile,Math.Max(0,key.Column));
            Grid.SetRowSpan(tile,Math.Max(1,key.RowSpan));
            Grid.SetColumnSpan(tile,Math.Max(1,key.ColumnSpan));
            KeypadGrid.Children.Add(tile);
        }

        if(_activeLayout.Keys.Count>0 && DeviceLayoutCatalog.GetKey(_activeLayout,_selectedKey) is null)
            _selectedKey=_activeLayout.Keys[0].Id;
        SelectedKeyText.Text=KeyDisplayName(_selectedKey);
        UpdateSelectedAction();
        RefreshKeyTiles();
    }

    private async void RefreshKeyTiles()
    {
        int generation=++_iconGeneration;
        var icons=new List<(KeyTileControl Tile,string? Path)>();
        foreach (var tile in FindVisualChildren<KeyTileControl>(KeypadGrid))
        {
            if (tile.Tag is not string key) continue;
            var definition=DeviceLayoutCatalog.GetKey(_activeLayout,key);
            var mapping=FindMapping(key);
            tile.ActionLabel=definition?.ReservedToggle==true?"Ativar / Desativar":mapping?.Action.Name ?? (definition?.ScanCode<0?"Não identificado":"Sem ação");
            tile.IconText=definition?.ReservedToggle==true?"⏻":mapping?.Action.Type switch
            {
                ActionType.OpenApplication => "▦", ActionType.OpenFolder => "▭", ActionType.OpenUrl => "🌐",
                ActionType.WebSearch => "⌕", ActionType.SendHotkey => "⌨", ActionType.TypeText => "Tᵀ",
                ActionType.MediaControl => "▶", ActionType.SystemAction => "⚙", ActionType.MultiAction => "◇",
                ActionType.Disabled => "", ActionType.PassThrough => "↪", _ => ""
            };
            tile.SetActionIcon(null);
            if(mapping?.Action.Type is ActionType.OpenApplication or ActionType.OpenFile or ActionType.OpenFolder)
                icons.Add((tile,mapping.Action.Parameters.GetValueOrDefault("path")));
            tile.ToolTip=$"Tecla {key} — {tile.ActionLabel}. Clique para configurar.";
        }
        foreach(var item in icons)
        {
            var icon=await Task.Run(()=>ShellIcon.Read(item.Path));
            if(Dispatcher.HasShutdownStarted)return;
            bool current=await Dispatcher.InvokeAsync(()=>
            {
                if(generation!=_iconGeneration)return false;
                item.Tile.SetActionIcon(icon);return true;
            });
            if(!current)return;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject d) where T:DependencyObject
    { for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){var c=VisualTreeHelper.GetChild(d,i);if(c is T t)yield return t;foreach(var x in FindVisualChildren<T>(c))yield return x;} }

    private void Window_PreviewMouseLeftButtonDown(object sender,MouseButtonEventArgs e)
    {
        var headerPoint=e.GetPosition(HeaderRoot);
        if (e.ChangedButton!=MouseButton.Left || headerPoint.Y>HeaderRoot.ActualHeight || headerPoint.Y < -18 || IsInteractiveElement(e.OriginalSource as DependencyObject)) return;
        if (e.ClickCount==2) { Maximize_Click(sender,new RoutedEventArgs()); return; }
        if (e.ButtonState!=MouseButtonState.Pressed) return;
        if (WindowState==WindowState.Maximized)
        {
            var pointer=PointToScreen(e.GetPosition(this));
            var fraction=ActualWidth>0?e.GetPosition(this).X/ActualWidth:0.5;
            WindowState=WindowState.Normal;
            Left=pointer.X-(RestoreBounds.Width*fraction);
            Top=Math.Max(SystemParameters.WorkArea.Top,pointer.Y-22);
        }
        ReleaseCapture();
        SendMessage(new WindowInteropHelper(this).Handle,0x00A1,2,0);
        e.Handled=true;
    }
    private bool IsInteractiveElement(DependencyObject? element)
    {
        while (element is not null && element!=this)
        {
            if (element is ButtonBase or TextBox or ComboBox or CheckBox) return true;
            element=VisualTreeHelper.GetParent(element);
        }
        return false;
    }
    private void Minimize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void Maximize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void Close_Click(object sender,RoutedEventArgs e){_sessions.CancelAll();Hide();}
    private void StopActions_Click(object sender,RoutedEventArgs e){_sessions.CancelAll();CancelBursts();FooterStatus.Text="Sequências interrompidas";}
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e){_sessions.CancelAll();base.OnClosing(e);}
    private void Window_StateChanged(object sender,EventArgs e){ }
    protected override void OnClosed(EventArgs e){_iconGeneration++;_sessions.CancelAll();CancelBursts();App.RawInput.KeyEvent-=RawInput_KeyEvent;_interception.Dispose();base.OnClosed(e);}
    public void StopCapture()=>SetEnabled(false,"Parada");
    public void ToggleCapture()=>ToggleEnabled_Click(this,new RoutedEventArgs());
    public void DisposeInput(){_sessions.CancelAll();_interception.Dispose();}

    private sealed class BurstState
    {
        public DeviceKeyEvent Event { get; set; } = new("",0,0,false,true,DateTimeOffset.MinValue);
        public int Count { get; set; }
        public CancellationTokenSource? Cancellation { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)] private struct MonitorInfo { public int Size; public NativeRect MonitorArea,WorkArea; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd,int message,nint wParam,nint lParam);
}
