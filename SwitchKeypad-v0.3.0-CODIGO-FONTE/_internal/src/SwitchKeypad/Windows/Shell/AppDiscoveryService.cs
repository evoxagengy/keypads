using System.Runtime.InteropServices;

namespace SwitchKeypad.Windows.Shell;

public sealed record DiscoveredApp(string Name, string LaunchTarget, string? IconSource = null, string Source = "start-menu");

public static class AppDiscoveryService
{
    public static IReadOnlyList<DiscoveredApp> ScanInstalledApps()
    {
        var apps = new Dictionary<string,DiscoveredApp>(StringComparer.CurrentCultureIgnoreCase);
        AddStartMenuApps(apps);
        AddAppsFolder(apps);
        return apps.Values
            .OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // Compatibility for v0.3 callers/tests.
    public static IReadOnlyList<DiscoveredApp> ScanStartMenu() => ScanInstalledApps();

    private static void AddStartMenuApps(Dictionary<string,DiscoveredApp> apps)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };
        foreach (var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try { files = Directory.GetFiles(root,"*.lnk",new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true}); }
            catch { continue; }
            foreach (var file in files)
            {
                var name=Path.GetFileNameWithoutExtension(file);
                if(string.IsNullOrWhiteSpace(name)) continue;
                apps.TryAdd(name,new DiscoveredApp(name,file,file));
            }
        }
    }

    private static void AddAppsFolder(Dictionary<string,DiscoveredApp> apps)
    {
        object? shell = null, folder = null, items = null;
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if(type is null) return;
            shell=Activator.CreateInstance(type);
            if(shell is null) return;
            dynamic dShell=shell;
            folder=dShell.NameSpace("shell:AppsFolder");
            if(folder is null) return;
            dynamic dFolder=folder;
            items=dFolder.Items();
            foreach(dynamic item in items)
            {
                string name;
                string path;
                try { name=(string)item.Name; path=(string)item.Path; }
                catch { continue; }
                if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(path)) continue;
                var target = path.StartsWith("shell:",StringComparison.OrdinalIgnoreCase) ? path : "shell:AppsFolder\\"+path;
                apps[name]=new DiscoveredApp(name,target,null,"apps-folder");
            }
        }
        catch { /* AppsFolder is optional; Start Menu results remain usable. */ }
        finally
        {
            Release(items);Release(folder);Release(shell);
        }
    }

    private static void Release(object? value)
    {
        if(value is null || !Marshal.IsComObject(value)) return;
        try { Marshal.FinalReleaseComObject(value); } catch { }
    }
}
