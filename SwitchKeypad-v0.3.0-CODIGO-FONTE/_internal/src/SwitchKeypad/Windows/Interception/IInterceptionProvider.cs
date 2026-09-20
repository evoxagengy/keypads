using SwitchKeypad.Core.Models;
namespace SwitchKeypad.Windows.Interception;

public enum InterceptionState { Unavailable, Ready, Running, Error }
public interface IInterceptionProvider : IDisposable
{
    InterceptionState State { get; }
    string StatusText { get; }
    event EventHandler<DeviceKeyEvent>? KeyEvent;
    bool TryStart(DeviceDefinition device, Func<DeviceKeyEvent,bool> consumePredicate);
    void Stop();
}
