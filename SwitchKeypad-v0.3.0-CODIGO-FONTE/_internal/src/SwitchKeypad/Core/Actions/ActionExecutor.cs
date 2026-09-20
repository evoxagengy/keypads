using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SwitchKeypad.Core.Models;

namespace SwitchKeypad.Core.Actions;

public sealed class ActionExecutor
{
    public Func<string,bool>? SelectProfile { get; set; }

    public async Task<ActionResult> ExecuteAsync(ActionDefinition action, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            if(ActionValidator.Validate(action) is string error)return ActionResult.Fail(error);
            if(action.Parameters.TryGetValue("delayMs",out var delay)&&int.TryParse(delay,out int ms)&&ms>0)
                await Task.Delay(ms,ct);
            return action.Type switch
            {
                ActionType.OpenApplication => Open(action, "path"),
                ActionType.OpenFile => Open(action, "path"),
                ActionType.OpenFolder => Open(action, "path"),
                ActionType.OpenUrl => OpenUrl(action),
                ActionType.WebSearch => WebSearch(action),
                ActionType.SendHotkey => SendHotkey(action),
                ActionType.TypeText => TypeText(action),
                ActionType.MediaControl => Media(action),
                ActionType.SystemAction => SystemAction(action),
                ActionType.MultiAction => await ExecuteSequenceAsync(action, ct),
                ActionType.Delay => await DelayAsync(action,ct),
                ActionType.SwitchProfile => SelectProfile?.Invoke(action.Parameters["profileId"])==true?ActionResult.Ok("Perfil alterado"):ActionResult.Fail("Perfil não encontrado"),
                ActionType.Disabled => ActionResult.Fail("Ação desativada"),
                ActionType.PassThrough => ActionResult.Ok("Pass-through"),
                _ => ActionResult.Fail("Tipo de ação não implementado")
            };
        }
        catch (OperationCanceledException) { return new(ActionStatus.Cancelled,"Cancelado"); }
        catch (UnauthorizedAccessException ex) { return new(ActionStatus.PermissionRequired, ex.Message); }
        catch (Exception ex) { return ActionResult.Fail(ex.Message); }
    }

    private static ActionResult Open(ActionDefinition a, string key)
    {
        if (!a.Parameters.TryGetValue(key, out var path) || string.IsNullOrWhiteSpace(path)) return ActionResult.Fail("Caminho não configurado");
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return ActionResult.Ok(); }
        catch (System.ComponentModel.Win32Exception) { return new(ActionStatus.NotFound, $"Não encontrado: {path}"); }
    }

    private static ActionResult OpenUrl(ActionDefinition a)
    {
        if (!a.Parameters.TryGetValue("url", out var raw) || !Uri.TryCreate(raw, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return ActionResult.Fail("URL inválida. Apenas http/https.");
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        return ActionResult.Ok();
    }

    private static ActionResult WebSearch(ActionDefinition a)
    {
        var q = a.Parameters.GetValueOrDefault("query", "");
        var engine = a.Parameters.GetValueOrDefault("engine", "google").ToLowerInvariant();
        var baseUrl = engine switch { "bing" => "https://www.bing.com/search?q=", "duckduckgo" => "https://duckduckgo.com/?q=", _ => "https://www.google.com/search?q=" };
        Process.Start(new ProcessStartInfo(baseUrl + Uri.EscapeDataString(q)) { UseShellExecute = true });
        return ActionResult.Ok();
    }

    private static ActionResult SendHotkey(ActionDefinition a)
    {
        var text = a.Parameters.GetValueOrDefault("hotkey", "");
        var keys = HotkeyParser.Parse(text);
        if (keys.Count == 0) return ActionResult.Fail("Atalho inválido");
        InputSender.SendChord(keys);
        return ActionResult.Ok();
    }

    private static ActionResult TypeText(ActionDefinition a)
    {
        var text = a.Parameters.GetValueOrDefault("text", "");
        InputSender.TypeUnicode(text);
        if(a.Parameters.GetValueOrDefault("appendEnter")=="true")InputSender.Tap(0x0D);
        return ActionResult.Ok();
    }

    private static ActionResult Media(ActionDefinition a)
    {
        var cmd = a.Parameters.GetValueOrDefault("command", "").ToLowerInvariant();
        var vk = cmd switch { "playpause" => 0xB3, "next" => 0xB0, "previous" => 0xB1, "volumeup" => 0xAF, "volumedown" => 0xAE, "mute" => 0xAD, _ => 0 };
        if (vk == 0) return ActionResult.Fail("Comando de mídia inválido");
        InputSender.Tap((ushort)vk);
        return ActionResult.Ok();
    }

    private static ActionResult SystemAction(ActionDefinition a)
    {
        var cmd = a.Parameters.GetValueOrDefault("command", "").ToLowerInvariant();
        switch (cmd)
        {
            case "explorer": Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); break;
            case "settings": Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); break;
            case "lock": LockWorkStation(); break;
            case "screenshot": InputSender.SendChord([0x5B,0x10,0x53]); break; // Win+Shift+S
            default: return ActionResult.Fail("Comando de sistema inválido");
        }
        return ActionResult.Ok();
    }

    private async Task<ActionResult> ExecuteSequenceAsync(ActionDefinition a, CancellationToken ct)
    {
        if(a.Repeat.Mode==SequenceRepeatMode.UntilCancelled&&!ct.CanBeCanceled)
            return ActionResult.Fail("Repetição contínua exige uma sessão cancelável.");
        int cycles=a.Repeat.Mode==SequenceRepeatMode.Count?a.Repeat.Count:1;
        for(int cycle=0;a.Repeat.Mode==SequenceRepeatMode.UntilCancelled||cycle<cycles;cycle++)
        {
            foreach (var step in a.Steps)
            {
                ct.ThrowIfCancellationRequested();
                var r = await ExecuteAsync(step,ct);
                if (r.Status != ActionStatus.Success) return r;
            }
            if(a.Repeat.Mode==SequenceRepeatMode.UntilCancelled||cycle+1<cycles)
                await Task.Delay(Math.Max(10,a.Repeat.IntervalMs),ct).ConfigureAwait(false);
        }
        ct.ThrowIfCancellationRequested();
        return ActionResult.Ok("Sequência concluída");
    }

    private static async Task<ActionResult> DelayAsync(ActionDefinition action,CancellationToken ct)
    {
        await Task.Delay(int.Parse(action.Parameters["milliseconds"]),ct).ConfigureAwait(false);
        return ActionResult.Ok("Espera concluída");
    }

    [DllImport("user32.dll")] private static extern bool LockWorkStation();
}

internal static class HotkeyParser
{
    private static readonly Dictionary<string,ushort> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CTRL"]=0x11,["CONTROL"]=0x11,["SHIFT"]=0x10,["ALT"]=0x12,["WIN"]=0x5B,
        ["ENTER"]=0x0D,["TAB"]=0x09,["ESC"]=0x1B,["SPACE"]=0x20,["BACKSPACE"]=0x08,
        ["UP"]=0x26,["DOWN"]=0x28,["LEFT"]=0x25,["RIGHT"]=0x27
    };
    public static List<ushort> Parse(string text)
    {
        var result = new List<ushort>();
        foreach (var token in text.Split('+', StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
        {
            if(token.StartsWith("VK_",StringComparison.OrdinalIgnoreCase)&&ushort.TryParse(token[3..],System.Globalization.NumberStyles.HexNumber,null,out var raw)&&raw is >0 and <255){result.Add(raw);continue;}
            if(token.StartsWith("F",StringComparison.OrdinalIgnoreCase)&&int.TryParse(token[1..],out int f)&&f is >=1 and <=24){result.Add((ushort)(0x70+f-1));continue;}
            if (Map.TryGetValue(token,out var vk)) { result.Add(vk); continue; }
            if (token.Length == 1)
            {
                var ch = char.ToUpperInvariant(token[0]);
                if (ch is >= 'A' and <= 'Z') { result.Add(ch); continue; }
                if (ch is >= '0' and <= '9') { result.Add(ch); continue; }
            }
            return [];
        }
        return result;
    }
}

internal static class InputSender
{
    private const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004;
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public INPUTUNION U; }
    // Win32 INPUT includes a MOUSEINPUT-sized union: 32 bytes on x64, total INPUT size 40.
    [StructLayout(LayoutKind.Explicit,Size=32)] private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public nint dwExtraInfo; }
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void Tap(ushort vk) => Send([Down(vk),Up(vk)]);
    public static void SendChord(IReadOnlyList<ushort> keys)
    {
        var list = new List<INPUT>();
        foreach (var k in keys) list.Add(Down(k));
        for (var i=keys.Count-1;i>=0;i--) list.Add(Up(keys[i]));
        Send(list.ToArray());
    }
    public static void TypeUnicode(string text)
    {
        var list = new List<INPUT>();
        foreach (var ch in text.Replace("\r\n","\n").Replace('\r','\n'))
        {
            if(ch=='\n'){list.Add(Down(0x0D));list.Add(Up(0x0D));}
            else {list.Add(Unicode(ch,false));list.Add(Unicode(ch,true));}
        }
        Send(list.ToArray());
    }
    private static INPUT Down(ushort vk)=>new(){type=INPUT_KEYBOARD,U=new(){ki=new(){wVk=vk}}};
    private static INPUT Up(ushort vk)=>new(){type=INPUT_KEYBOARD,U=new(){ki=new(){wVk=vk,dwFlags=KEYEVENTF_KEYUP}}};
    private static INPUT Unicode(char c,bool up)=>new(){type=INPUT_KEYBOARD,U=new(){ki=new(){wScan=c,dwFlags=KEYEVENTF_UNICODE|(up?KEYEVENTF_KEYUP:0)}}};
    private static void Send(INPUT[] inputs)
    {
        if (inputs.Length == 0) return;
        var sent=SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            var error=Marshal.GetLastWin32Error();
            // A partial insertion can stop between key-down and key-up. Release only
            // keys whose down event was actually accepted, in reverse order.
            var held=new List<INPUT>();
            for(int i=0;i<sent;i++)
            {
                var input=inputs[i];
                if((input.U.ki.dwFlags&KEYEVENTF_KEYUP)==0)held.Add(input);
                else
                {
                    int index=held.FindLastIndex(x=>x.U.ki.wVk==input.U.ki.wVk&&x.U.ki.wScan==input.U.ki.wScan);
                    if(index>=0)held.RemoveAt(index);
                }
            }
            held.Reverse();
            var release=held.Select(x=>{x.U.ki.dwFlags|=KEYEVENTF_KEYUP;return x;}).ToArray();
            if(release.Length>0)SendInput((uint)release.Length,release,Marshal.SizeOf<INPUT>());
            throw new System.ComponentModel.Win32Exception(error,"O Windows não aceitou todas as teclas. Verifique as permissões da janela de destino.");
        }
    }
}
