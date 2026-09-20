namespace SwitchKeypad.Core.Models;

public sealed class DeviceLayoutDefinition
{
    public string DeviceFingerprint { get; set; } = "";
    public string Name { get; set; } = "Layout do dispositivo";
    public int Columns { get; set; } = 4;
    public bool Confirmed { get; set; }
    public string Source { get; set; } = "learned";
    public List<PhysicalKeyDefinition> Keys { get; set; } = [];
}

public sealed class PhysicalKeyDefinition
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public int ScanCode { get; set; }
    public int VirtualKey { get; set; }
    public bool IsExtended { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
}

public static class DeviceLayoutCatalog
{
    public static DeviceLayoutDefinition Resolve(AppConfig config, DeviceDefinition? device)
    {
        if (device is null) return LegacyTemplate("");

        var saved = config.DeviceLayouts.FirstOrDefault(x => x.DeviceFingerprint == device.Fingerprint);
        if (saved is not null) return saved;

        DeviceLayoutDefinition layout;
        if (string.Equals(device.Vid, "1710", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(device.Pid, "8812", StringComparison.OrdinalIgnoreCase))
        {
            layout = Known1710_8812(device.Fingerprint);
        }
        else
        {
            layout = new DeviceLayoutDefinition
            {
                DeviceFingerprint = device.Fingerprint,
                Name = device.FriendlyName,
                Columns = 4,
                Confirmed = false,
                Source = "learned"
            };
        }

        config.DeviceLayouts.Add(layout);
        return layout;
    }

    public static PhysicalKeyDefinition? ResolveKey(DeviceLayoutDefinition layout, DeviceKeyEvent e)
        => layout.Keys.FirstOrDefault(k => k.ScanCode == e.ScanCode && k.IsExtended == e.IsExtended);

    public static PhysicalKeyDefinition? GetKey(DeviceLayoutDefinition layout, string id)
        => layout.Keys.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool Observe(DeviceLayoutDefinition layout, DeviceKeyEvent e)
    {
        if (ResolveKey(layout, e) is not null) return false;

        var id = $"sc-{e.ScanCode:X2}-{(e.IsExtended ? "e" : "n")}";
        var label = LabelFor(e);
        var index = layout.Keys.Count;
        var columns = Math.Max(1, layout.Columns);
        layout.Keys.Add(new PhysicalKeyDefinition
        {
            Id = id,
            Label = label,
            ScanCode = e.ScanCode,
            VirtualKey = e.VirtualKey,
            IsExtended = e.IsExtended,
            Row = index / columns,
            Column = index % columns
        });
        layout.Confirmed = false;
        layout.Source = "learned";
        return true;
    }

    public static DeviceLayoutDefinition LegacyTemplate(string fingerprint)
    {
        var layout = new DeviceLayoutDefinition
        {
            DeviceFingerprint = fingerprint,
            Name = "Numpad padrão",
            Columns = 4,
            Confirmed = true,
            Source = "legacy"
        };

        Add(layout, "Num", "Num", 0x45, 0x90, false, 0, 0);
        Add(layout, "/", "/", 0x35, 0x6F, true, 0, 1);
        Add(layout, "*", "*", 0x37, 0x6A, false, 0, 2);
        Add(layout, "-", "-", 0x4A, 0x6D, false, 0, 3);
        Add(layout, "7", "7", 0x47, 0x67, false, 1, 0);
        Add(layout, "8", "8", 0x48, 0x68, false, 1, 1);
        Add(layout, "9", "9", 0x49, 0x69, false, 1, 2);
        Add(layout, "+", "+", 0x4E, 0x6B, false, 1, 3, 2, 1);
        Add(layout, "4", "4", 0x4B, 0x64, false, 2, 0);
        Add(layout, "5", "5", 0x4C, 0x65, false, 2, 1);
        Add(layout, "6", "6", 0x4D, 0x66, false, 2, 2);
        Add(layout, "1", "1", 0x4F, 0x61, false, 3, 0);
        Add(layout, "2", "2", 0x50, 0x62, false, 3, 1);
        Add(layout, "3", "3", 0x51, 0x63, false, 3, 2);
        Add(layout, "Enter", "Enter", 0x1C, 0x0D, true, 3, 3, 2, 1);
        Add(layout, "0", "0", 0x52, 0x60, false, 4, 0, 1, 2);
        Add(layout, ".", ".", 0x53, 0x6E, false, 4, 2);
        return layout;
    }

    private static DeviceLayoutDefinition Known1710_8812(string fingerprint)
    {
        var layout = new DeviceLayoutDefinition
        {
            DeviceFingerprint = fingerprint,
            Name = "Numpad VID 1710 / PID 8812",
            Columns = 4,
            Confirmed = false,
            Source = "known-template"
        };

        Add(layout, "Backspace", "Backspace", 0x0E, 0x08, false, 0, 0);
        Add(layout, "/", "/", 0x35, 0x6F, true, 0, 1);
        Add(layout, "*", "*", 0x37, 0x6A, false, 0, 2);
        Add(layout, "-", "-", 0x4A, 0x6D, false, 0, 3);
        Add(layout, "7", "7", 0x47, 0x67, false, 1, 0);
        Add(layout, "8", "8", 0x48, 0x68, false, 1, 1);
        Add(layout, "9", "9", 0x49, 0x69, false, 1, 2);
        Add(layout, "+", "+", 0x4E, 0x6B, false, 1, 3);
        Add(layout, "4", "4", 0x4B, 0x64, false, 2, 0);
        Add(layout, "5", "5", 0x4C, 0x65, false, 2, 1);
        Add(layout, "6", "6", 0x4D, 0x66, false, 2, 2);
        Add(layout, "1", "1", 0x4F, 0x61, false, 3, 0);
        Add(layout, "2", "2", 0x50, 0x62, false, 3, 1);
        Add(layout, "3", "3", 0x51, 0x63, false, 3, 2);
        Add(layout, "Enter", "Enter", 0x1C, 0x0D, true, 3, 3, 2, 1);
        Add(layout, "0", "0", 0x52, 0x60, false, 4, 0);
        Add(layout, ".", ".", 0x53, 0x6E, false, 4, 1);
        return layout;
    }

    private static void Add(DeviceLayoutDefinition layout, string id, string label, int scan, int vk, bool extended, int row, int col, int rowSpan = 1, int colSpan = 1)
        => layout.Keys.Add(new PhysicalKeyDefinition
        {
            Id = id,
            Label = label,
            ScanCode = scan,
            VirtualKey = vk,
            IsExtended = extended,
            Row = row,
            Column = col,
            RowSpan = rowSpan,
            ColumnSpan = colSpan
        });

    private static string LabelFor(DeviceKeyEvent e)
    {
        if (e.ScanCode == 0x0E) return "Backspace";
        if (e.ScanCode == 0x1C && e.IsExtended) return "Enter";
        if (e.ScanCode == 0x35 && e.IsExtended) return "/";
        if (!e.IsExtended)
        {
            var known = PhysicalKeyMap.ScanCodes.FirstOrDefault(x => x.Value == e.ScanCode).Key;
            if (!string.IsNullOrWhiteSpace(known)) return known;
        }

        if (e.VirtualKey is >= 0x30 and <= 0x39) return ((char)e.VirtualKey).ToString();
        if (e.VirtualKey is >= 0x41 and <= 0x5A) return ((char)e.VirtualKey).ToString();
        return $"0x{e.ScanCode:X2}";
    }
}
