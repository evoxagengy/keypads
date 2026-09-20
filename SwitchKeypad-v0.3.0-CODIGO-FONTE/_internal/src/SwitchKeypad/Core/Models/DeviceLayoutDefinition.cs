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
    public int ScanCode { get; set; } = -1;
    public int VirtualKey { get; set; }
    public bool IsExtended { get; set; }
    public int PressCount { get; set; } = 1;
    public bool ReservedToggle { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
}

public enum DeviceLayoutTemplate
{
    Numeric4x5,
    FullKeyboard,
    Custom
}

public static class DeviceLayoutCatalog
{
    public static DeviceLayoutDefinition Resolve(AppConfig config, DeviceDefinition? device)
    {
        if (device is null) return LegacyTemplate("");

        var saved = config.DeviceLayouts.FirstOrDefault(x => x.DeviceFingerprint == device.Fingerprint);
        if (saved is not null)
        {
            // v0.4.0 shipped an incorrect hard-coded geometry for this device.
            // Replace only that known template; never overwrite a layout the user calibrated.
            if(string.Equals(device.Vid,"1710",StringComparison.OrdinalIgnoreCase)
               && string.Equals(device.Pid,"8812",StringComparison.OrdinalIgnoreCase)
               && string.Equals(saved.Source,"known-template",StringComparison.OrdinalIgnoreCase))
            {
                var corrected=Numeric4x5Template(device.Fingerprint,
                    string.IsNullOrWhiteSpace(device.CustomName)?device.FriendlyName:device.CustomName);
                config.DeviceLayouts.Remove(saved);
                config.DeviceLayouts.Add(corrected);
                return corrected;
            }
            return saved;
        }

        DeviceLayoutDefinition layout;
        if (string.Equals(device.Vid, "1710", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(device.Pid, "8812", StringComparison.OrdinalIgnoreCase))
        {
            layout = Numeric4x5Template(device.Fingerprint, device.FriendlyName);
        }
        else
        {
            layout = new DeviceLayoutDefinition
            {
                DeviceFingerprint = device.Fingerprint,
                Name = device.FriendlyName,
                Columns = 4,
                Confirmed = false,
                Source = "unconfigured"
            };
        }

        config.DeviceLayouts.Add(layout);
        return layout;
    }

    public static DeviceLayoutDefinition CreateTemplate(DeviceLayoutTemplate template, string fingerprint, string name)
        => template switch
        {
            DeviceLayoutTemplate.Numeric4x5 => Numeric4x5Template(fingerprint,name),
            DeviceLayoutTemplate.FullKeyboard => FullKeyboardTemplate(fingerprint,name),
            _ => GenericGrid(fingerprint,name,5,4)
        };

    public static IReadOnlyList<PhysicalKeyDefinition> ResolveCandidates(DeviceLayoutDefinition layout, DeviceKeyEvent e)
        => layout.Keys.Where(k => k.ScanCode >= 0 && k.ScanCode == e.ScanCode && k.IsExtended == e.IsExtended).ToList();

    public static PhysicalKeyDefinition? ResolveKey(DeviceLayoutDefinition layout, DeviceKeyEvent e)
    {
        var candidates=ResolveCandidates(layout,e);
        if(candidates.Count==0)return null;
        if(candidates.Count==1)return candidates[0];
        return candidates.FirstOrDefault(k=>k.PressCount==1) ?? candidates[0];
    }

    public static PhysicalKeyDefinition? ResolveBurst(DeviceLayoutDefinition layout, DeviceKeyEvent e, int pressCount)
    {
        var candidates=ResolveCandidates(layout,e);
        return candidates.FirstOrDefault(k=>Math.Max(1,k.PressCount)==Math.Max(1,pressCount))
               ?? (pressCount==1?candidates.FirstOrDefault(k=>k.PressCount==1):null)
               ?? candidates.FirstOrDefault();
    }

    public static PhysicalKeyDefinition? GetKey(DeviceLayoutDefinition layout, string id)
        => layout.Keys.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool Observe(DeviceLayoutDefinition layout, DeviceKeyEvent e)
    {
        if (ResolveCandidates(layout,e).Count>0) return false;

        var empty=layout.Keys.FirstOrDefault(k=>k.ScanCode<0);
        if(empty is not null)
        {
            empty.ScanCode=e.ScanCode;
            empty.VirtualKey=e.VirtualKey;
            empty.IsExtended=e.IsExtended;
            empty.PressCount=1;
            if(IsNumLock(e))
            {
                empty.Label="Num Lock";
                empty.ReservedToggle=true;
            }
            layout.Confirmed=false;
            layout.Source="learned";
            return true;
        }

        var id = $"sc-{e.ScanCode:X2}-{(e.IsExtended ? "e" : "n")}-{layout.Keys.Count}";
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
            PressCount=1,
            ReservedToggle=IsNumLock(e),
            Row = index / columns,
            Column = index % columns
        });
        layout.Confirmed = false;
        layout.Source = "learned";
        return true;
    }

    public static bool IsNumLock(DeviceKeyEvent e)
        => e.ScanCode==0x45 || e.VirtualKey==0x90;

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

        Add(layout, "Num", "Num Lock", 0x45, 0x90, false, 0, 0, reserved:true);
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

    public static DeviceLayoutDefinition Numeric4x5Template(string fingerprint,string? name=null)
    {
        var layout = new DeviceLayoutDefinition
        {
            DeviceFingerprint = fingerprint,
            Name = string.IsNullOrWhiteSpace(name)?"Numpad 4x5":name!,
            Columns = 4,
            Confirmed = false,
            Source = "numeric-4x5"
        };

        Add(layout, "Num", "Num Lock", 0x45, 0x90, false, 0, 0, reserved:true);
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
        Add(layout, "Backspace", "Backspace", 0x0E, 0x08, false, 2, 3);

        Add(layout, "1", "1", 0x4F, 0x61, false, 3, 0);
        Add(layout, "2", "2", 0x50, 0x62, false, 3, 1);
        Add(layout, "3", "3", 0x51, 0x63, false, 3, 2);
        Add(layout, "Enter", "Enter", 0x1C, 0x0D, true, 3, 3, 2, 1);

        Add(layout, "0", "0", 0x52, 0x60, false, 4, 0);
        Add(layout, "000", "000", 0x52, 0x60, false, 4, 1, pressCount:3);
        Add(layout, ".", ".", 0x53, 0x6E, false, 4, 2);
        return layout;
    }

    public static DeviceLayoutDefinition GenericGrid(string fingerprint,string name,int rows,int columns)
    {
        rows=Math.Clamp(rows,1,12);
        columns=Math.Clamp(columns,1,24);
        var layout=new DeviceLayoutDefinition
        {
            DeviceFingerprint=fingerprint,
            Name=name,
            Columns=columns,
            Confirmed=false,
            Source="custom"
        };
        for(int r=0;r<rows;r++)
            for(int c=0;c<columns;c++)
                layout.Keys.Add(new PhysicalKeyDefinition
                {
                    Id=$"R{r+1}C{c+1}",
                    Label="",
                    Row=r,
                    Column=c,
                    ScanCode=-1
                });
        return layout;
    }

    public static DeviceLayoutDefinition FullKeyboardTemplate(string fingerprint,string name)
    {
        var layout=new DeviceLayoutDefinition
        {
            DeviceFingerprint=fingerprint,
            Name=name,
            Columns=23,
            Confirmed=false,
            Source="full-keyboard"
        };

        Slot(layout,"Esc","Esc",0,0);
        for(int i=1;i<=12;i++)Slot(layout,$"F{i}",$"F{i}",0,i+1);
        Slot(layout,"Print","PrtSc",0,15);Slot(layout,"Scroll","ScrLk",0,16);Slot(layout,"Pause","Pause",0,17);

        var row1=new[]{"\u0060","1","2","3","4","5","6","7","8","9","0","-","="};
        for(int i=0;i<row1.Length;i++)Slot(layout,$"R1-{i}",row1[i],1,i);
        Slot(layout,"Backspace","Backspace",1,13,1,2);
        Slot(layout,"Insert","Ins",1,16);Slot(layout,"Home","Home",1,17);Slot(layout,"PageUp","PgUp",1,18);
        Slot(layout,"Num","Num Lock",1,19,reserved:true);Slot(layout,"NumSlash","/",1,20);Slot(layout,"NumStar","*",1,21);Slot(layout,"NumMinus","-",1,22);

        Slot(layout,"Tab","Tab",2,0,1,2);
        var row2=new[]{"Q","W","E","R","T","Y","U","I","O","P","[","]","\\"};
        for(int i=0;i<row2.Length;i++)Slot(layout,$"R2-{i}",row2[i],2,i+2);
        Slot(layout,"Delete","Del",2,16);Slot(layout,"End","End",2,17);Slot(layout,"PageDown","PgDn",2,18);
        Slot(layout,"Num7","7",2,19);Slot(layout,"Num8","8",2,20);Slot(layout,"Num9","9",2,21);Slot(layout,"NumPlus","+",2,22,2,1);

        Slot(layout,"Caps","Caps",3,0,1,2);
        var row3=new[]{"A","S","D","F","G","H","J","K","L",";","'"};
        for(int i=0;i<row3.Length;i++)Slot(layout,$"R3-{i}",row3[i],3,i+2);
        Slot(layout,"MainEnter","Enter",3,13,1,2);
        Slot(layout,"Num4","4",3,19);Slot(layout,"Num5","5",3,20);Slot(layout,"Num6","6",3,21);

        Slot(layout,"LShift","Shift",4,0,1,3);
        var row4=new[]{"Z","X","C","V","B","N","M",",",".","/"};
        for(int i=0;i<row4.Length;i++)Slot(layout,$"R4-{i}",row4[i],4,i+3);
        Slot(layout,"RShift","Shift",4,13,1,2);
        Slot(layout,"Up","↑",4,17);
        Slot(layout,"Num1","1",4,19);Slot(layout,"Num2","2",4,20);Slot(layout,"Num3","3",4,21);Slot(layout,"NumEnter","Enter",4,22,2,1);

        Slot(layout,"LCtrl","Ctrl",5,0,1,2);Slot(layout,"LWin","Win",5,2);Slot(layout,"LAlt","Alt",5,3);
        Slot(layout,"Space","Espaço",5,4,1,7);
        Slot(layout,"RAlt","AltGr",5,11,1,2);Slot(layout,"RWin","Win",5,13);Slot(layout,"Menu","Menu",5,14);Slot(layout,"RCtrl","Ctrl",5,15);
        Slot(layout,"Left","←",5,16);Slot(layout,"Down","↓",5,17);Slot(layout,"Right","→",5,18);
        Slot(layout,"Num0","0",5,19,1,2);Slot(layout,"NumDecimal",".",5,21);
        return layout;
    }

    private static void Slot(DeviceLayoutDefinition layout,string id,string label,int row,int col,int rowSpan=1,int colSpan=1,bool reserved=false)
        => layout.Keys.Add(new PhysicalKeyDefinition
        {
            Id=id,Label=label,Row=row,Column=col,RowSpan=rowSpan,ColumnSpan=colSpan,ScanCode=-1,ReservedToggle=reserved
        });

    private static void Add(DeviceLayoutDefinition layout, string id, string label, int scan, int vk, bool extended, int row, int col, int rowSpan = 1, int colSpan = 1, int pressCount=1, bool reserved=false)
        => layout.Keys.Add(new PhysicalKeyDefinition
        {
            Id = id,
            Label = label,
            ScanCode = scan,
            VirtualKey = vk,
            IsExtended = extended,
            PressCount = Math.Max(1,pressCount),
            ReservedToggle=reserved,
            Row = row,
            Column = col,
            RowSpan = rowSpan,
            ColumnSpan = colSpan
        });

    private static string LabelFor(DeviceKeyEvent e)
    {
        if (IsNumLock(e)) return "Num Lock";
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
