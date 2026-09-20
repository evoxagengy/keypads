using System.Runtime.InteropServices;
using System.Text;
using SwitchKeypad.Core.Models;

namespace SwitchKeypad.Windows.Interception;

/// <summary>
/// Optional provider for the third-party Interception API. No third-party binary or driver is bundled.
/// If interception.dll + its installed driver are present, Switch Keypad can consume only the selected keyboard.
/// Production distribution must separately resolve driver signing, Windows 11/Secure Boot validation and licensing.
/// </summary>
public sealed class InterceptionProvider : IInterceptionProvider
{
    private nint _ctx;
    private Thread? _thread;
    private volatile bool _stop;
    private int _selectedDevice;
    private string _selectedFingerprint="";
    private Func<DeviceKeyEvent,bool>? _consume;
    private Native.Predicate? _filter;
    public InterceptionState State { get; private set; } = InterceptionState.Unavailable;
    public string StatusText { get; private set; } = "Camada exclusiva não instalada";
    public event EventHandler<DeviceKeyEvent>? KeyEvent;

    public InterceptionProvider()
    {
        try
        {
            _ctx = Native.interception_create_context();
            if (_ctx != 0) { State=InterceptionState.Ready; StatusText="Interception detectado"; }
        }
        catch (DllNotFoundException) { }
        catch (BadImageFormatException) { StatusText="interception.dll incompatível (x64 necessário)"; State=InterceptionState.Error; }
        catch (Exception ex) { StatusText=ex.Message; State=InterceptionState.Error; }
    }

    public bool TryStart(DeviceDefinition device, Func<DeviceKeyEvent,bool> consumePredicate)
    {
        if (_ctx == 0 || State==InterceptionState.Unavailable) return false;
        if(State==InterceptionState.Running)return true;
        // Device matching errors are recoverable: switching to another keyboard must not require restarting the app.
        if(State==InterceptionState.Error){State=InterceptionState.Ready;StatusText="Interception pronto para nova identificação";}
        if(string.IsNullOrWhiteSpace(device.Vid)||string.IsNullOrWhiteSpace(device.Pid)){StatusText="Identificação USB insuficiente";return false;}
        var matches = new List<int>();
        for (var id=1;id<=10;id++)
        {
            var hw = GetHardwareId(id);
            if (string.IsNullOrWhiteSpace(hw)) continue;
            var vidOk = device.Vid is null || hw.Contains("VID_"+device.Vid,StringComparison.OrdinalIgnoreCase);
            var pidOk = device.Pid is null || hw.Contains("PID_"+device.Pid,StringComparison.OrdinalIgnoreCase);
            if (vidOk && pidOk) matches.Add(id);
        }
        if (matches.Count != 1)
        {
            StatusText = matches.Count == 0 ? "Dispositivo não localizado na camada exclusiva" : "Mais de um dispositivo idêntico; reidentificação necessária";
            State = InterceptionState.Error;
            return false;
        }
        _selectedDevice=matches[0]; _selectedFingerprint=device.Fingerprint; _consume=consumePredicate; _stop=false;
        _filter=id=>id==_selectedDevice?1:0;
        Native.interception_set_filter(_ctx, _filter, Native.FilterKeyDown|Native.FilterKeyUp);
        _thread = new Thread(Loop){IsBackground=true,Name="SwitchKeypad.Interception",Priority=ThreadPriority.AboveNormal};
        _thread.Start(); State=InterceptionState.Running; StatusText="Modo exclusivo ativo"; return true;
    }

    private void Loop()
    {
        try
        {
            while(!_stop)
            {
                var dev=Native.interception_wait_with_timeout(_ctx,100);
                if(dev<=0) continue;
                var stroke=new Native.KeyStroke();
                if(Native.interception_receive(_ctx,dev,ref stroke,1)<=0) continue;
                if(dev != _selectedDevice) { Native.interception_send(_ctx,dev,ref stroke,1); continue; }
                var down=(stroke.state & Native.KeyUp)==0;
                var ext=(stroke.state & (Native.KeyE0|Native.KeyE1))!=0;
                var ev=new DeviceKeyEvent(_selectedFingerprint,stroke.code,0,ext,down,DateTimeOffset.Now,GetHardwareId(dev));
                KeyEvent?.Invoke(this,ev);
                var consume=_consume?.Invoke(ev) ?? false;
                if(!consume) Native.interception_send(_ctx,dev,ref stroke,1);
            }
        }
        catch(Exception ex){ State=InterceptionState.Error; StatusText=ex.Message; }
        finally { if(_ctx!=0&&_filter is not null)Native.interception_set_filter(_ctx,_filter,0); }
    }

    private string GetHardwareId(int id)
    {
        var sb=new StringBuilder(500);
        try { var n=Native.interception_get_hardware_id(_ctx,id,sb,(uint)sb.Capacity*2); return n>0?sb.ToString():""; }
        catch { return ""; }
    }
    public void Stop()
    {
        _stop=true;
        if(_ctx!=0&&_filter is not null)Native.interception_set_filter(_ctx,_filter,0);
        if (_thread is { IsAlive:true } && Thread.CurrentThread != _thread) _thread.Join();
        _thread=null;
        if(State==InterceptionState.Running){ State=InterceptionState.Ready; StatusText="Modo exclusivo parado"; }
    }
    public void Dispose(){ Stop(); if(_ctx!=0){ try{Native.interception_destroy_context(_ctx);}catch{} _ctx=0; } }

    private static class Native
    {
        public const ushort KeyUp=0x01, KeyE0=0x02, KeyE1=0x04;
        public const ushort FilterKeyDown=0x01, FilterKeyUp=0x02;
        [StructLayout(LayoutKind.Sequential)] public struct KeyStroke { public ushort code,state; public uint information; }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int Predicate(int device);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern nint interception_create_context();
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern void interception_destroy_context(nint context);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern int interception_wait(nint context);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern int interception_wait_with_timeout(nint context,uint milliseconds);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern int interception_receive(nint context,int device,ref KeyStroke stroke,uint nstroke);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern int interception_send(nint context,int device,ref KeyStroke stroke,uint nstroke);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern void interception_set_filter(nint context,Predicate predicate,ushort filter);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] public static extern uint interception_get_hardware_id(nint context,int device,StringBuilder hardware_id_buffer,uint buffer_size);
        [DllImport("interception.dll",CallingConvention=CallingConvention.Cdecl)] public static extern int interception_is_keyboard(int device);
    }
}
