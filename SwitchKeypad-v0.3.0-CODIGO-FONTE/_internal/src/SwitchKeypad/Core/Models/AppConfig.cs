namespace SwitchKeypad.Core.Models;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public string? SelectedDeviceFingerprint { get; set; }
    public string ActiveProfileId { get; set; } = "default";
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public List<DeviceDefinition> Devices { get; set; } = [];
    public List<DeviceLayoutDefinition> DeviceLayouts { get; set; } = [];
    public List<ProfileDefinition> Profiles { get; set; } = [ProfileDefinition.CreateDefault()];
}

public sealed class DeviceDefinition
{
    public string Fingerprint { get; set; } = "";
    public string FriendlyName { get; set; } = "Numpad USB Dedicado";
    public string? DevicePath { get; set; }
    public string? Vid { get; set; }
    public string? Pid { get; set; }
    public string? Serial { get; set; }
    public string? Manufacturer { get; set; }
    public string? Product { get; set; }
    public string? InstanceId { get; set; }
    public string? Location { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

public sealed class ProfileDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Padrão";
    public List<KeyMapping> Mappings { get; set; } = [];

    public static ProfileDefinition CreateDefault() => new()
    {
        Id = "default",
        Name = "Padrão",
        Mappings = []
    };
}

public sealed class KeyMapping
{
    public string PhysicalKey { get; set; } = "";
    public int ScanCode { get; set; }
    public int VirtualKey { get; set; }
    public bool IsExtended { get; set; }
    public TriggerType Trigger { get; set; } = TriggerType.Press;
    public RepeatBehavior Repeat { get; set; } = RepeatBehavior.IgnoreRepeat;
    public ActionDefinition Action { get; set; } = new();
}

public sealed class ActionDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ActionType Type { get; set; } = ActionType.Disabled;
    public string Name { get; set; } = "Sem ação";
    public Dictionary<string,string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ActionDefinition> Steps { get; set; } = [];
    // Additive schema-1 fields: older profiles deserialize to safe single execution.
    public RepeatPolicy Repeat { get; set; } = new();
}

public sealed class RepeatPolicy
{
    public SequenceRepeatMode Mode { get; set; } = SequenceRepeatMode.Once;
    public int Count { get; set; } = 1;
    public int IntervalMs { get; set; }
}
public enum SequenceRepeatMode { Once, Count, UntilCancelled }

public enum ActionType
{
    OpenApplication, OpenFile, OpenFolder, OpenUrl, WebSearch, SendHotkey, TypeText,
    MediaControl, SystemAction, MultiAction, SwitchProfile, Disabled, PassThrough, Delay
}
public enum TriggerType { Press, LongPress, DoublePress }
public enum RepeatBehavior { Once, RepeatWhileHeld, IgnoreRepeat }
public enum ActionStatus { Success, Failed, Cancelled, NotFound, PermissionRequired }

public sealed record ActionResult(ActionStatus Status, string Message)
{
    public static ActionResult Ok(string message = "Executado") => new(ActionStatus.Success, message);
    public static ActionResult Fail(string message) => new(ActionStatus.Failed, message);
}

public sealed record DeviceKeyEvent(
    string DeviceId, int ScanCode, int VirtualKey, bool IsExtended, bool IsKeyDown,
    DateTimeOffset Timestamp, string? DevicePath = null);

internal static class DefaultMappings
{
    public static List<KeyMapping> Create()
    {
        static KeyMapping M(string key, int scan, int vk, ActionType type, string name, params (string,string)[] p)
        {
            var a = new ActionDefinition { Type = type, Name = name };
            foreach (var (k,v) in p) a.Parameters[k] = v;
            return new KeyMapping { PhysicalKey = key, ScanCode = scan, VirtualKey = vk, Action = a };
        }
        return
        [
            M("7",0x47,0x67,ActionType.OpenUrl,"Chrome",("url","https://www.google.com")),
            M("8",0x48,0x68,ActionType.OpenApplication,"OBS Studio",("path","obs64.exe")),
            M("9",0x49,0x69,ActionType.OpenApplication,"Discord",("path","discord.exe")),
            M("4",0x4B,0x64,ActionType.OpenFolder,"Explorador",("path",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))),
            M("5",0x4C,0x65,ActionType.SendHotkey,"Salvar",("hotkey","CTRL+S")),
            M("6",0x4D,0x66,ActionType.MediaControl,"Play/Pause",("command","playpause")),
            M("1",0x4F,0x61,ActionType.OpenUrl,"Edge",("url","https://www.microsoft.com/edge")),
            M("2",0x50,0x62,ActionType.OpenApplication,"VS Code",("path","code.exe")),
            M("3",0x51,0x63,ActionType.OpenApplication,"Bloco de Notas",("path","notepad.exe")),
            M("0",0x52,0x60,ActionType.OpenApplication,"Aplicativos",("path","explorer.exe")),
            M(".",0x53,0x6E,ActionType.MediaControl,"Silenciar",("command","mute")),
            M("+",0x4E,0x6B,ActionType.MediaControl,"Vol +",("command","volumeup")),
            M("-",0x4A,0x6D,ActionType.MediaControl,"Vol -",("command","volumedown")),
            M("Enter",0x1C,0x0D,ActionType.MultiAction,"Executar")
        ];
    }
}
