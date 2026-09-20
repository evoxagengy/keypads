using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SwitchKeypad;
using SwitchKeypad.Core.Models;
using SwitchKeypad.Core.Actions;
using SwitchKeypad.Core.Configuration;
using SwitchKeypad.UI.Controls;
using SwitchKeypad.Windows.RawInput;

class Program
{
 static int passed;
 static void Check(bool value,string title){if(!value)throw new Exception("FAIL: "+title);Console.WriteLine("PASS: "+title);passed++;}
 static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(obj)!;
 static IEnumerable<T> Children<T>(DependencyObject obj)where T:DependencyObject{
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(obj);i++){var child=VisualTreeHelper.GetChild(obj,i);if(child is T typed)yield return typed;foreach(var nested in Children<T>(child))yield return nested;}}
 static void Render(FrameworkElement root,double width,double height,string output){
  root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(output);encoder.Save(stream);}
 [STAThread] static int Main(){
  try{
   System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
   var output=Path.GetFullPath("artifacts");Directory.CreateDirectory(output);
   var store=new ConfigStore(Path.Combine(output,"test-config"));store.Load();
   var app=new App(startServices:false);app.InitializeComponent();
   typeof(App).GetProperty("ConfigStore")!.SetValue(null,store);
   typeof(App).GetProperty("ActionExecutor")!.SetValue(null,new ActionExecutor());
   typeof(App).GetProperty("RawInput")!.SetValue(null,new RawInputService());
   var input=typeof(ActionExecutor).Assembly.GetType("SwitchKeypad.Core.Actions.InputSender+INPUT")!;
   Check(Marshal.SizeOf(input)==40,"Win32 INPUT has correct x64 size");
   Check(ActionValidator.Validate(new(){Type=ActionType.SendHotkey,Parameters=new(){{"hotkey","CTRL+SHIFT+F12"}}})==null,"Hotkey F12");
   Check(ActionValidator.Validate(new(){Type=ActionType.SendHotkey,Parameters=new(){{"hotkey","CTRL+invalid"}}})!=null,"Reject invalid hotkey");
   Check(ActionValidator.Validate(new(){Type=ActionType.OpenUrl,Parameters=new(){{"url","file:///C:/test"}}})!=null,"Reject non-web URL");
   var sequence=new ActionDefinition{Type=ActionType.MultiAction,Steps=[new(){Type=ActionType.PassThrough,Parameters=new(){{"delayMs","200"}}}]};
   using(var cancel=new CancellationTokenSource(30)){var result=new ActionExecutor().ExecuteAsync(sequence,cancel.Token).GetAwaiter().GetResult();Check(result.Status==ActionStatus.Cancelled,"Sequence cancellation interrupts delay");}
   var clone=ActionEditorWindow.Copy(sequence);clone.Steps[0].Name="changed";Check(sequence.Steps[0].Name!="changed","Sequence edits are deep copies");
   var order=new List<string>();var executor=new ActionExecutor{SelectProfile=id=>{order.Add(id);return true;}};
   var ordered=new ActionDefinition{Type=ActionType.MultiAction,Steps=[
       new(){Type=ActionType.SwitchProfile,Parameters=new(){{"profileId","first"}}},
       new(){Type=ActionType.SwitchProfile,Parameters=new(){{"profileId","second"}}}]};
   var orderedResult=executor.ExecuteAsync(ordered).GetAwaiter().GetResult();
   Check(orderedResult.Status==ActionStatus.Success && string.Join(",",order)=="first,second","Sequence preserves execution order");
   var export=Path.Combine(output,"profile.json");
   var profile=new ProfileDefinition{Name="Teste",Mappings=[new(){PhysicalKey="0",ScanCode=0x52,Action=new(){Type=ActionType.TypeText,Parameters=new(){{"text","Teste"}}}}]};
   store.ExportProfile(profile,export);var imported=store.ImportProfile(export);
   Check(imported.Id!=profile.Id && imported.Mappings[0].Action.Parameters["text"]=="Teste","Profile export/import round trip");
   sequence.Steps.Add(new(){Type=ActionType.MultiAction});Check(ActionValidator.Validate(sequence)!=null,"Reject nested sequence");
   Check(ActionValidator.Validate(new(){Type=ActionType.Delay,Parameters=new(){{"milliseconds","-1"}}})!=null,"Reject negative delay");
   Check(ActionValidator.Validate(new(){Type=ActionType.Delay,Parameters=new(){{"milliseconds","60000"}}})==null,"Allow 60-second delay");
   Check(ActionValidator.Validate(new(){Type=ActionType.Delay,Parameters=new(){{"milliseconds","60001"}}})!=null,"Reject oversized delay");
   var repeating=ActionEditorWindow.Copy(ordered);repeating.Repeat=new(){Mode=SequenceRepeatMode.Count,Count=3,IntervalMs=10};
   order.Clear();Check(executor.ExecuteAsync(repeating).GetAwaiter().GetResult().Status==ActionStatus.Success&&order.Count==6,"Sequence repeats exactly three cycles");
   repeating.Repeat.Mode=SequenceRepeatMode.UntilCancelled;
   Check(executor.ExecuteAsync(repeating).GetAwaiter().GetResult().Status==ActionStatus.Failed,"Continuous loop cannot start without cancellation");
   using(var cancel=new CancellationTokenSource(60)){Check(executor.ExecuteAsync(repeating,cancel.Token).GetAwaiter().GetResult().Status==ActionStatus.Cancelled,"Continuous loop cancels");}
   var sessions=new ExecutionSessions();
   var slow=new ActionDefinition{Type=ActionType.MultiAction,Steps=[new(){Type=ActionType.Delay,Parameters=new(){{"milliseconds","1000"}}}]};
   var first=sessions.TriggerAsync("profile","5",slow,executor);
   Check(sessions.TriggerAsync("profile","5",slow,executor).GetAwaiter().GetResult().Message.Contains("já"),"Running finite mapping cannot re-enter");
   sessions.CancelAll();Check(first.GetAwaiter().GetResult().Status==ActionStatus.Cancelled,"Stop cancels active session");
   slow.Repeat.Mode=SequenceRepeatMode.UntilCancelled;
   first=sessions.TriggerAsync("profile","5",slow,executor);
   sessions.TriggerAsync("profile","5",slow,executor).GetAwaiter().GetResult();
   Check(first.GetAwaiter().GetResult().Status==ActionStatus.Cancelled,"Second press cancels continuous session");
   var legacy=System.Text.Json.JsonSerializer.Deserialize<ActionDefinition>("{\"Type\":9,\"Steps\":[{\"Type\":12}]}")!;
   Check(legacy.Repeat.Mode==SequenceRepeatMode.Once&&legacy.Repeat.Count==1&&legacy.Repeat.IntervalMs==0,"v0.2 actions default to one execution");
   Check(new AppConfig().Profiles[0].Mappings.Count==0,"First profile starts empty");
   var profileStore=new ConfigStore(Path.Combine(output,"profile-tests-"+Guid.NewGuid().ToString("N")));profileStore.Load();
   var empty=profileStore.CreateProfile();Check(empty.Mappings.Count==0,"New profile has no sample actions");
   profileStore.RenameProfile(empty.Id,"  Meu perfil  ");profileStore.Load();Check(profileStore.ActiveProfile.Name=="Meu perfil","Rename trims and persists");
   profileStore.DeleteProfile(empty.Id);Check(profileStore.Config.Profiles.Count==1&&profileStore.ActiveProfile.Id!=empty.Id,"Deleting active profile selects remaining profile");
   profileStore.DeleteProfile(profileStore.ActiveProfile.Id);profileStore.Load();Check(profileStore.ActiveProfile.Name=="Perfil 1"&&profileStore.ActiveProfile.Mappings.Count==0,"Deleting last profile creates empty replacement");
   var legacyConfigPath=Path.Combine(output,"legacy-config");Directory.CreateDirectory(legacyConfigPath);
   File.WriteAllText(Path.Combine(legacyConfigPath,"config.json"),"{\"SchemaVersion\":1,\"ActiveProfileId\":\"old\",\"Profiles\":[{\"Id\":\"old\",\"Name\":\"Meu perfil antigo\",\"Mappings\":[{\"PhysicalKey\":\"5\",\"ScanCode\":76,\"Action\":{\"Type\":6,\"Name\":\"Mensagem\",\"Parameters\":{\"text\":\"Olá!\"}}}]}]}");
   var legacyStore=new ConfigStore(legacyConfigPath);legacyStore.Load();legacyStore.Save();legacyStore.Load();
   Check(legacyStore.ActiveProfile.Mappings[0].Action.Parameters["text"]=="Olá!"&&legacyStore.ActiveProfile.Id=="old","v0.2 config preserves existing mappings on reload");
   foreach(var pair in PhysicalKeyMap.ScanCodes)
   {
       var physical=new DeviceKeyEvent("test",pair.Value,0,pair.Key is "/" or "Enter",true,DateTimeOffset.Now);
       Check(PhysicalKeyMap.Resolve(physical)==pair.Key,"Physical position resolves "+pair.Key);
   }
   Check(PhysicalKeyMap.Resolve(new("test",0x48,0x26,true,true,DateTimeOffset.Now))==null,"Navigation arrow is not numpad 8");
   Check(PhysicalKeyMap.Resolve(new("test",0x1C,0x0D,false,true,DateTimeOffset.Now))==null,"Main Enter is not numpad Enter");
   var numericLayout=DeviceLayoutCatalog.Numeric4x5Template("device","Numeric Keypad");
   Check(numericLayout.Keys.Count==19,"Reference numeric layout has 19 physical keys");
   Check(DeviceLayoutCatalog.GetKey(numericLayout,"+") is {Row:1,Column:3,RowSpan:1},"Plus key is a normal aligned key");
   Check(DeviceLayoutCatalog.GetKey(numericLayout,"Backspace") is {Row:2,Column:3,RowSpan:1},"Backspace occupies row 3 column 4");
   Check(DeviceLayoutCatalog.GetKey(numericLayout,"Enter") is {Row:3,Column:3,RowSpan:2},"Enter is the only vertical key in reference numpad");
   Check(DeviceLayoutCatalog.GetKey(numericLayout,"0") is {Row:4,Column:0,ColumnSpan:1},"Zero remains a normal key");
   Check(DeviceLayoutCatalog.GetKey(numericLayout,"000") is {Row:4,Column:1,PressCount:3},"000 burst key is modeled separately");
   Check(DeviceLayoutCatalog.GetKey(numericLayout,"Num")?.ReservedToggle==true,"Num Lock is reserved as enable-disable control");
   var fullLayout=DeviceLayoutCatalog.FullKeyboardTemplate("full","Main Keyboard");
   Check(fullLayout.Columns==23&&DeviceLayoutCatalog.GetKey(fullLayout,"NumPlus")?.RowSpan==2&&DeviceLayoutCatalog.GetKey(fullLayout,"NumEnter")?.RowSpan==2,"Full keyboard template includes aligned numpad");
   var migrateLayoutConfig=new AppConfig{DeviceLayouts=[new(){DeviceFingerprint="migrate",Source="known-template",Columns=4,Keys=[]}]};
   var migratedLayout=DeviceLayoutCatalog.Resolve(migrateLayoutConfig,new DeviceDefinition{Fingerprint="migrate",Vid="1710",Pid="8812",FriendlyName="HID"});
   Check(migratedLayout.Source=="numeric-4x5"&&DeviceLayoutCatalog.GetKey(migratedLayout,"000") is not null,"v0.4.0 wrong known template migrates to corrected layout");

   var window=new MainWindow();
   window.Show();
   window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Loaded);
   Check(ReferenceEquals(App.ConfigStore,store),"Tests never load the production config");
   var selectedDeviceField=typeof(MainWindow).GetField("_selectedDevice",BindingFlags.Instance|BindingFlags.NonPublic)!;
   selectedDeviceField.SetValue(window,new DeviceDefinition{Fingerprint="chosen"});
   var rawHandler=typeof(MainWindow).GetMethod("RawInput_KeyEvent",BindingFlags.Instance|BindingFlags.NonPublic)!;
   void Raw(int scan,bool down=true,string device="chosen",int vk=0,bool extended=false)=>rawHandler.Invoke(window,new object?[]{null,new DeviceKeyEvent(device,scan,vk,extended,down,DateTimeOffset.Now)});
   Raw(0x4C,vk:0x65);Check(Field<string>(window,"_selectedKey")=="5","Selected device key 5 selects UI");
   Raw(0x4F,device:"other");Check(Field<string>(window,"_selectedKey")=="5","Other keyboard cannot change selection");
   Raw(0x4F,down:false);Check(Field<string>(window,"_selectedKey")=="5","Key-up keeps selected state");
   Raw(0x4F,vk:0x23);Check(Field<string>(window,"_selectedKey")=="1","Num Lock off still selects physical key 1");
   store.Config.Enabled=true;
   Raw(0x45,vk:0x90);Check(!store.Config.Enabled,"Num Lock disables Switch Keypad on selected keyboard");
   System.Threading.Thread.Sleep(280);
   Raw(0x45,vk:0x90);Check(store.Config.Enabled,"Num Lock enables Switch Keypad again");

   var ensure=typeof(MainWindow).GetMethod("EnsureMapping",BindingFlags.Instance|BindingFlags.NonPublic)!;
   var oldProfile=store.Config.ActiveProfileId;store.CreateProfile();Raw(0x4C);
   var createdMapping=(KeyMapping)ensure.Invoke(window,null)!;
   Check(createdMapping.ScanCode==0x4C&&createdMapping.PhysicalKey=="5","Empty profile assigns correct scan code to key 5");
   Check(Field<TextBlock>(window,"SelectedKeyText").Text=="5","Physical selection updates action panel");
   var clicked=Children<KeyTileControl>((DependencyObject)window.Content).Single(t=>t.KeyLabel=="4");
   typeof(MainWindow).GetMethod("Key_Click",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{clicked,new RoutedEventArgs()});
   Check(Field<string>(window,"_selectedKey")=="4"&&clicked.IsSelectedKey,"Mouse uses same selected state");
   var mainSessions=Field<ExecutionSessions>(window,"_sessions");
   foreach(var method in new[]{"StopActions_Click","SyncProfile","StopCapture"})
   {
       var pending=mainSessions.TriggerAsync("ui","5",slow,executor);
       var stop=typeof(MainWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;
       stop.Invoke(window,stop.GetParameters().Length==0?null:new object[]{window,new RoutedEventArgs()});
       Check(pending.GetAwaiter().GetResult().Status==ActionStatus.Cancelled,"UI cancellation "+method);
   }
   store.Config.Enabled=true;
   store.Config.ActiveProfileId=oldProfile;selectedDeviceField.SetValue(window,null);
   var menu=new ContextMenu{Style=(Style)app.FindResource(typeof(ContextMenu))};menu.ApplyTemplate();
   Check(menu.Background is SolidColorBrush brush && brush.Color.R<20 && brush.Color.G<20,"Device context menu has black background");
   var root=(FrameworkElement)window.Content;
   root.Measure(new Size(1450,920));root.Arrange(new Rect(0,0,1450,920));root.UpdateLayout();
   foreach(var tile in Children<KeyTileControl>(root))tile.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
   foreach(var tile in Children<KeyTileControl>(root)){
    var grid=(Grid)tile.Content;
    Check(grid.Background!=null,"Full key hit surface "+tile.KeyLabel);
    Check(!string.IsNullOrWhiteSpace(tile.ActionLabel),"Every visible key keeps an action/status label "+tile.KeyLabel);
    var hit=grid.InputHitTest(new Point(grid.ActualWidth*.25,grid.ActualHeight*.30));
    Check(hit!=null,"Empty surface hit "+tile.KeyLabel);
    var before=grid.RenderSize;tile.IsSelectedKey=true;tile.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));root.UpdateLayout();
    Check(before==grid.RenderSize,"Selected size fixed "+tile.KeyLabel);tile.IsSelectedKey=tile.KeyLabel=="0";tile.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
   }
   Render(root,1450,920,Path.Combine(output,"main.png"));
   Render(root,1080,700,Path.Combine(output,"main-small.png"));
   foreach(var type in Enum.GetValues<ActionType>()){
    var editor=new ActionEditorWindow(new(){Type=type,Name=ActionEditorWindow.Label(type)});
    if(type==ActionType.MultiAction)
    {
        editor.Action.Steps=[
            new(){Type=ActionType.SendHotkey,Name="Pressionar T",Parameters=new(){{"hotkey","T"}}},
            new(){Type=ActionType.Delay,Name="Esperar",Parameters=new(){{"milliseconds","100"}}},
            new(){Type=ActionType.TypeText,Name="Digitar mensagem",Parameters=new(){{"text","Olá! Teste de sequência."}}},
            new(){Type=ActionType.Delay,Name="Esperar",Parameters=new(){{"milliseconds","50"}}},
            new(){Type=ActionType.SendHotkey,Name="Pressionar Enter",Parameters=new(){{"hotkey","ENTER"}}}];
        typeof(ActionEditorWindow).GetMethod("RefreshSteps",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,null);
        Field<ComboBox>(editor,"_repeatMode").SelectedValue=SequenceRepeatMode.Count;
        Field<TextBox>(editor,"_repeatCount").Text="5";
        Field<TextBox>(editor,"_repeatInterval").Text="1000";
        typeof(ActionEditorWindow).GetMethod("Collect",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,null);
        Check(editor.Action.Repeat.Count==5&&editor.Action.Repeat.IntervalMs==1000&&ActionValidator.Validate(editor.Action)==null,"Editor saves repeat settings and five ordered steps");
    }
    var content=(FrameworkElement)editor.Content;
    Render(content,758,858,Path.Combine(output,"editor-"+type+".png"));
    Check(editor.WindowStyle==WindowStyle.None,"Dark modal frame "+type);
    Check(Field<ComboBox>(editor,"_type").SelectedValue is ActionType selected&&selected==type,"Editor opens "+type);
    if(type==ActionType.TypeText){
     Field<TextBox>(editor,"_parameter").Text="  Olá!\nSegunda linha  ";
     typeof(ActionEditorWindow).GetMethod("Collect",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,null);
     Check(editor.Action.Parameters["text"]=="  Olá!\nSegunda linha  ","Text preserves spaces and newlines");
    }
    editor.Close();
   }
   var settings=new SettingsWindow(new SwitchKeypad.Windows.Interception.InterceptionProvider());
   typeof(SettingsWindow).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settings,null);
   Render((FrameworkElement)settings.Content,720,680,Path.Combine(output,"settings.png"));settings.Close();
   var rename=SwitchKeypad.UI.ModalChrome.Create(window,"Renomear perfil","Altere o nome sem modificar as ações das teclas.",new TextBox{Text="Meu perfil"},"Salvar nome",()=>true);
   Render((FrameworkElement)rename.Content,480,310,Path.Combine(output,"rename.png"));rename.Close();
   var iconPath=Path.Combine(Environment.SystemDirectory,"notepad.exe");
   var icon=SwitchKeypad.Windows.Shell.ShellIcon.Read(iconPath);
   Check(icon!=null&&ReferenceEquals(icon,SwitchKeypad.Windows.Shell.ShellIcon.Read(iconPath)),"Shell application icon is cached");
   Check(Directory.EnumerateFiles(Path.Combine(store.RootPath,"Cache","Icons"),"*.png").Any(),"Shell icon has persistent cache");
   Check(SwitchKeypad.Windows.Shell.ShellIcon.Read("Z:\\not-a-real-app.exe")==null,"Missing application icon falls back safely");
   foreach(var device in App.RawInput.EnumerateKeyboards())Console.WriteLine("DEVICE: "+device.FriendlyName+" | "+device.Product);
   window.Close();App.RawInput.Dispose();
   Console.WriteLine("TOTAL PASSED: "+passed);return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
