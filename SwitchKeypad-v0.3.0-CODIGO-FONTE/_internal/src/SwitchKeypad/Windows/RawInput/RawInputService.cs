using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Interop;
using SwitchKeypad.Core.Models;

namespace SwitchKeypad.Windows.RawInput;

public sealed class RawInputService : IDisposable
{
    public event EventHandler<DeviceKeyEvent>? KeyEvent;
    public event EventHandler? DevicesChanged;
    private HwndSource? _source;
    private nint _hwnd;

    public void Attach(nint hwnd)
    {
        if (_hwnd == hwnd && _source is not null) return;
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
        Register(hwnd);
    }

    public IReadOnlyList<DeviceDefinition> EnumerateKeyboards()
    {
        uint count = 0;
        if (Native.GetRawInputDeviceList(null, ref count, (uint)Marshal.SizeOf<Native.RAWINPUTDEVICELIST>()) == uint.MaxValue) return [];
        var list = new Native.RAWINPUTDEVICELIST[count];
        if (Native.GetRawInputDeviceList(list, ref count, (uint)Marshal.SizeOf<Native.RAWINPUTDEVICELIST>()) == uint.MaxValue) return [];
        var result = new List<DeviceDefinition>();
        foreach (var item in list)
        {
            if (item.dwType != Native.RIM_TYPEKEYBOARD) continue;
            var path = GetDeviceName(item.hDevice);
            if (string.IsNullOrWhiteSpace(path)) continue;
            var (vid,pid) = ParseVidPid(path);
            result.Add(new DeviceDefinition
            {
                DevicePath = path,
                Vid = vid, Pid = pid,
                FriendlyName = Friendly(path, vid, pid),
                Fingerprint = Fingerprint(path),
                LastSeenUtc = DateTime.UtcNow
            });
        }
        foreach(var device in result) DeviceMetadata.Enrich(device);
        return result.OrderBy(d=>d.FriendlyName,StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Native.WM_INPUT)
        {
            var ev = ReadInput(lParam);
            if (ev is not null) KeyEvent?.Invoke(this, ev);
        }
        else if (msg == Native.WM_INPUT_DEVICE_CHANGE) DevicesChanged?.Invoke(this, EventArgs.Empty);
        return 0;
    }

    private static DeviceKeyEvent? ReadInput(nint handle)
    {
        uint size = 0;
        Native.GetRawInputData(handle, Native.RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<Native.RAWINPUTHEADER>());
        if (size == 0) return null;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (Native.GetRawInputData(handle, Native.RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<Native.RAWINPUTHEADER>()) != size) return null;
            var raw = Marshal.PtrToStructure<Native.RAWINPUT>(buffer);
            if (raw.header.dwType != Native.RIM_TYPEKEYBOARD) return null;
            var path = GetDeviceName(raw.header.hDevice) ?? raw.header.hDevice.ToString("X");
            var k = raw.keyboard;
            var isBreak = (k.Flags & Native.RI_KEY_BREAK) != 0;
            var extended = (k.Flags & Native.RI_KEY_E0) != 0 || (k.Flags & Native.RI_KEY_E1) != 0;
            return new DeviceKeyEvent(Fingerprint(path), k.MakeCode, k.VKey, extended, !isBreak, DateTimeOffset.Now, path);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void Register(nint hwnd)
    {
        var rid = new Native.RAWINPUTDEVICE
        {
            usUsagePage = 0x01, usUsage = 0x06,
            dwFlags = Native.RIDEV_INPUTSINK | Native.RIDEV_DEVNOTIFY,
            hwndTarget = hwnd
        };
        if (!Native.RegisterRawInputDevices([rid],1,(uint)Marshal.SizeOf<Native.RAWINPUTDEVICE>()))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private static string? GetDeviceName(nint device)
    {
        uint chars = 0;
        Native.GetRawInputDeviceInfo(device, Native.RIDI_DEVICENAME, IntPtr.Zero, ref chars);
        if (chars == 0) return null;
        var sb = new StringBuilder((int)chars + 1);
        if (Native.GetRawInputDeviceInfo(device, Native.RIDI_DEVICENAME, sb, ref chars) == uint.MaxValue) return null;
        return sb.ToString();
    }

    private static (string? vid,string? pid) ParseVidPid(string path)
    {
        string? Extract(string key)
        {
            var i = path.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0 || i + key.Length + 4 > path.Length) return null;
            return path.Substring(i + key.Length,4).ToUpperInvariant();
        }
        return (Extract("VID_"),Extract("PID_"));
    }
    private static string Friendly(string path,string? vid,string? pid)
        => vid is not null && pid is not null ? $"Teclado USB ({vid}:{pid})" : "Teclado USB";
    private static string Fingerprint(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).Substring(0,24);

    public void Dispose()
    {
        if (_source is not null) _source.RemoveHook(WndProc);
        _source = null;
    }

    private static class Native
    {
        public const int WM_INPUT=0x00FF, WM_INPUT_DEVICE_CHANGE=0x00FE;
        public const uint RID_INPUT=0x10000003, RIDI_DEVICENAME=0x20000007;
        public const uint RIM_TYPEKEYBOARD=1, RIDEV_INPUTSINK=0x00000100, RIDEV_DEVNOTIFY=0x00002000;
        public const ushort RI_KEY_BREAK=1, RI_KEY_E0=2, RI_KEY_E1=4;
        [StructLayout(LayoutKind.Sequential)] public struct RAWINPUTDEVICELIST { public nint hDevice; public uint dwType; }
        [StructLayout(LayoutKind.Sequential)] public struct RAWINPUTDEVICE { public ushort usUsagePage,usUsage; public uint dwFlags; public nint hwndTarget; }
        [StructLayout(LayoutKind.Sequential)] public struct RAWINPUTHEADER { public uint dwType,dwSize; public nint hDevice,wParam; }
        [StructLayout(LayoutKind.Sequential)] public struct RAWKEYBOARD { public ushort MakeCode,Flags,Reserved,VKey; public uint Message,ExtraInformation; }
        [StructLayout(LayoutKind.Explicit)] public struct RAWINPUT { [FieldOffset(0)] public RAWINPUTHEADER header; [FieldOffset(24)] public RAWKEYBOARD keyboard; }
        [DllImport("user32.dll",SetLastError=true)] public static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);
        [DllImport("user32.dll",SetLastError=true,CharSet=CharSet.Unicode)] public static extern uint GetRawInputDeviceInfo(nint hDevice,uint uiCommand,IntPtr pData,ref uint pcbSize);
        [DllImport("user32.dll",SetLastError=true,CharSet=CharSet.Unicode)] public static extern uint GetRawInputDeviceInfo(nint hDevice,uint uiCommand,StringBuilder pData,ref uint pcbSize);
        [DllImport("user32.dll",SetLastError=true)] public static extern uint GetRawInputData(nint hRawInput,uint uiCommand,IntPtr pData,ref uint pcbSize,uint cbSizeHeader);
        [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices,uint uiNumDevices,uint cbSize);
    }
}
