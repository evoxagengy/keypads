using SwitchKeypad.Core.Models;
namespace SwitchKeypad.Core.Actions;
public static class ActionValidator
{
    public static string? Validate(ActionDefinition a,int depth=0)
    {
        if(a is null||a.Parameters is null||a.Steps is null)return "Ação inválida.";
        if(!Enum.IsDefined(a.Type))return "Tipo de ação inválido.";
        if(a.Parameters.TryGetValue("delayMs",out var delay)&&(!int.TryParse(delay,out int ms)||ms<0||ms>5000))return "A espera deve ser de 0 a 5000 ms.";
        string Get(string key)=>a.Parameters.GetValueOrDefault(key,"");
        if(a.Type==ActionType.MultiAction)
        {
            if(a.Repeat is null||!Enum.IsDefined(a.Repeat.Mode))return "Modo de repetição inválido.";
            if(a.Repeat.Count is <1 or >10000)return "Escolha entre 1 e 10.000 repetições.";
            if(a.Repeat.IntervalMs is <0 or >60000)return "O intervalo deve ser de 0 a 60.000 ms.";
            if(depth>0)return "Sequências aninhadas não são permitidas.";
            if(a.Steps.Count is <1 or >32)return "A sequência precisa de 1 a 32 passos.";
            foreach(var step in a.Steps)if(Validate(step,depth+1) is string error)return error;
            return null;
        }
        if(a.Type==ActionType.Delay&&(!int.TryParse(Get("milliseconds"),out int wait)||wait is <10 or >60000))return "A espera deve ser de 10 a 60.000 ms.";
        if(a.Type==ActionType.SendHotkey&&HotkeyParser.Parse(Get("hotkey")).Count==0)return "Atalho inválido. Use uma tecla ou combinação como CTRL+SHIFT+S.";
        if(a.Type==ActionType.OpenUrl&&(!Uri.TryCreate(Get("url"),UriKind.Absolute,out var url)||url.Scheme is not ("http" or "https")))return "Informe uma URL http:// ou https:// válida.";
        if(a.Type is ActionType.OpenApplication or ActionType.OpenFile or ActionType.OpenFolder && string.IsNullOrWhiteSpace(Get("path")))return "Escolha um destino.";
        if(a.Type==ActionType.TypeText&&Get("text").Length==0)return "Informe o texto.";
        if(a.Type==ActionType.TypeText&&Get("text").Length>16000)return "Limite de 16.000 caracteres.";
        if(a.Type==ActionType.WebSearch&&string.IsNullOrWhiteSpace(Get("query")))return "Informe a pesquisa.";
        if(a.Type==ActionType.MediaControl&&!new[]{"playpause","next","previous","volumeup","volumedown","mute"}.Contains(Get("command")))return "Escolha um controle de mídia.";
        if(a.Type==ActionType.SystemAction&&!new[]{"explorer","settings","lock","screenshot"}.Contains(Get("command")))return "Escolha um comando de sistema.";
        if(a.Type==ActionType.SwitchProfile&&string.IsNullOrWhiteSpace(Get("profileId")))return "Escolha um perfil.";
        return null;
    }
}
