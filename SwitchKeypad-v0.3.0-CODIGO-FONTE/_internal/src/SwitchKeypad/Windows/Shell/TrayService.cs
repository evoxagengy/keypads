using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using SwitchKeypad.Core.Configuration;

namespace SwitchKeypad.Windows.Shell;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Window _window;
    private readonly ConfigStore _store;
    public TrayService(Window window, ConfigStore store)
    {
        _window=window; _store=store;
        _icon = new NotifyIcon { Text="Switch Keypad", Visible=true };
        try { _icon.Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { _icon.Icon = SystemIcons.Application; }
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir",null,(_,_)=>Show());
        menu.Items.Add(_store.Config.Enabled ? "Desativar" : "Ativar",null,(_,_)=>Toggle());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair",null,(_,_)=>((App)System.Windows.Application.Current).ExitApplication());
        _icon.ContextMenuStrip=menu;
        _icon.DoubleClick += (_,_)=>Show();
        _window.Closing += (_,e)=> { e.Cancel=true; _window.Hide(); };
    }
    private void Show(){ _window.Show(); _window.WindowState=WindowState.Normal; _window.Activate(); }
    private void Toggle(){if(_window is MainWindow window)window.ToggleCapture();if(_icon.ContextMenuStrip is { } menu)menu.Items[1].Text=_store.Config.Enabled?"Desativar":"Ativar";}
    public void Dispose(){ _icon.Visible=false; _icon.Dispose(); }
}
