using System.Windows;
using SwitchKeypad.Core.Configuration;
using SwitchKeypad.Core.Actions;
using SwitchKeypad.Windows.RawInput;
using SwitchKeypad.Windows.Shell;

namespace SwitchKeypad;

public partial class App : System.Windows.Application
{
    private readonly bool _startServices;
    public App():this(true) { }
    // Component tests load WPF resources without touching a user's config, tray or input.
    public App(bool startServices){_startServices=startServices;}
    private TrayService? _tray;
    public static ConfigStore ConfigStore { get; private set; } = null!;
    public static ActionExecutor ActionExecutor { get; private set; } = null!;
    public static RawInputService RawInput { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(!_startServices)return;
        ConfigStore = new ConfigStore();
        ConfigStore.Load();
        ActionExecutor = new ActionExecutor();
        RawInput = new RawInputService();

        var window = new MainWindow();
        MainWindow = window;
        _tray = new TrayService(window, ConfigStore);
        window.Show();
    }

    public void ExitApplication()
    {
        try { ConfigStore.Save(); } catch { }
        if(MainWindow is MainWindow window)window.DisposeInput();
        RawInput.Dispose();
        _tray?.Dispose();
        Shutdown();
    }
}
