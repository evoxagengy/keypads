namespace SwitchKeypad.Core.Models;

/// <summary>Physical numpad positions, independent of Num Lock and configured actions.</summary>
public static class PhysicalKeyMap
{
    public static IReadOnlyDictionary<string,int> ScanCodes { get; } = new Dictionary<string,int>
    {
        ["Num"]=0x45, ["/"]=0x35, ["*"]=0x37, ["-"]=0x4A,
        ["7"]=0x47, ["8"]=0x48, ["9"]=0x49, ["+"]=0x4E,
        ["4"]=0x4B, ["5"]=0x4C, ["6"]=0x4D,
        ["1"]=0x4F, ["2"]=0x50, ["3"]=0x51, ["Enter"]=0x1C,
        ["0"]=0x52, ["."]=0x53
    };

    public static string? Resolve(DeviceKeyEvent e)
    {
        // E0 distinguishes the navigation cluster, main Enter and slash from numpad keys.
        if(e.ScanCode==0x45)return "Num";
        if(e.ScanCode==0x35)return e.IsExtended?"/":null;
        if(e.ScanCode==0x1C)return e.IsExtended?"Enter":null;
        if(e.IsExtended)return null;
        return ScanCodes.FirstOrDefault(pair=>pair.Value==e.ScanCode).Key;
    }
}
