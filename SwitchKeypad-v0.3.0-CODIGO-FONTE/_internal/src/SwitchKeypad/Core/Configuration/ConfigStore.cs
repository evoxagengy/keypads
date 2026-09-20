using System.Text.Json;
using SwitchKeypad.Core.Models;

namespace SwitchKeypad.Core.Configuration;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public AppConfig Config { get; private set; } = new();
    public string RootPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwitchKeypad");
    public string ConfigPath => Path.Combine(RootPath, "config.json");
    public ConfigStore(string? rootPath=null){if(rootPath is not null)RootPath=rootPath;}

    public void Load()
    {
        Directory.CreateDirectory(RootPath);
        if (!File.Exists(ConfigPath)) { Config = new AppConfig(); Save(); return; }
        try
        {
            Config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions) ?? new AppConfig();
            if (Config.SchemaVersion != 1) throw new InvalidDataException($"Schema {Config.SchemaVersion} não suportado.");
            if (Config.Profiles.Count == 0) Config.Profiles.Add(ProfileDefinition.CreateDefault());
        }
        catch
        {
            var bad = Path.Combine(RootPath, $"config.corrupt.{DateTime.Now:yyyyMMddHHmmss}.json");
            try { File.Copy(ConfigPath, bad, true); } catch { }
            Config = new AppConfig();
            Save();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(RootPath);
        var temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Config, JsonOptions));
        if(File.Exists(ConfigPath)&&!File.Exists(ConfigPath+".before-v0.3.0.bak"))
            File.Copy(ConfigPath,ConfigPath+".before-v0.3.0.bak");
        File.Move(temp, ConfigPath, true);
    }

    public ProfileDefinition ActiveProfile => Config.Profiles.FirstOrDefault(p => p.Id == Config.ActiveProfileId) ?? Config.Profiles[0];

    public ProfileDefinition CreateProfile()
    {
        int number=1;
        while(Config.Profiles.Any(p=>p.Name==$"Perfil {number}"))number++;
        var profile=new ProfileDefinition{Name=$"Perfil {number}"};
        Config.Profiles.Add(profile);Config.ActiveProfileId=profile.Id;Save();return profile;
    }
    public void RenameProfile(string id,string name)
    {
        if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Informe um nome para o perfil.");
        Config.Profiles.Single(p=>p.Id==id).Name=name.Trim();Save();
    }
    public void DeleteProfile(string id)
    {
        Config.Profiles.RemoveAll(p=>p.Id==id);
        if(Config.Profiles.Count==0)Config.Profiles.Add(new(){Name="Perfil 1"});
        if(!Config.Profiles.Any(p=>p.Id==Config.ActiveProfileId))Config.ActiveProfileId=Config.Profiles[0].Id;
        Save();
    }

    public void ExportProfile(ProfileDefinition profile, string destination)
        => File.WriteAllText(destination, JsonSerializer.Serialize(profile, JsonOptions));

    public ProfileDefinition ImportProfile(string source)
    {
        var profile = JsonSerializer.Deserialize<ProfileDefinition>(File.ReadAllText(source), JsonOptions)
                      ?? throw new InvalidDataException("Perfil inválido.");
        if(profile.Mappings is null||profile.Mappings.Count>128||string.IsNullOrWhiteSpace(profile.Name))throw new InvalidDataException("Perfil inválido.");
        if(profile.Mappings.GroupBy(m=>m.PhysicalKey).Any(g=>g.Count()>1))throw new InvalidDataException("O perfil contém teclas duplicadas.");
        foreach(var mapping in profile.Mappings)
            if(Core.Actions.ActionValidator.Validate(mapping.Action) is string error)throw new InvalidDataException(error);
        profile.Id = Guid.NewGuid().ToString("N");
        Config.Profiles.Add(profile);
        Save();
        return profile;
    }
}
