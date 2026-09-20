namespace SwitchKeypad.Windows.Shell;

public sealed record DiscoveredApp(string Name, string ShortcutPath);

public static class AppDiscoveryService
{
    public static IReadOnlyList<DiscoveredApp> ScanStartMenu()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };
        var apps = new Dictionary<string,DiscoveredApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try { files = Directory.GetFiles(root,"*.lnk",new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true}); }
            catch { continue; }
            foreach (var file in files)
            {
                var name=Path.GetFileNameWithoutExtension(file);
                if(string.IsNullOrWhiteSpace(name)) continue;
                apps.TryAdd(name,new DiscoveredApp(name,file));
            }
        }
        return apps.Values.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
