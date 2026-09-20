using System.Windows;
using SwitchKeypad.Windows.Interception;
namespace SwitchKeypad;
public partial class SettingsWindow:Window
{
    private readonly IInterceptionProvider _provider;
    public SettingsWindow(IInterceptionProvider provider){InitializeComponent();Height=680;UI.ModalChrome.Apply(this);_provider=provider;Loaded+=(_,_)=>Refresh();}
    private void Refresh(){InputStatus.Text=$"Status: {_provider.State}";InputDetail.Text=_provider.StatusText+"\nRaw Input permanece disponível para descoberta, identificação e modo de teste por dispositivo.";}
    private void Stop_Click(object sender,RoutedEventArgs e){if(Owner is MainWindow main)main.StopCapture();else _provider.Stop();App.ConfigStore.Config.Enabled=false;App.ConfigStore.Save();Refresh();}
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
}
