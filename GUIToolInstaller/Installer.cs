using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using WindowsShortcutFactory;

namespace GUIToolInstaller;

public class Installer
{
    public static Assembly? GetCallerAssembly(int skipFrames = 2)
    {
        var stack = new StackTrace();
        Assembly current = null;
        for (int i = skipFrames; i < stack.FrameCount; i++)
        {
            var method = stack.GetFrame(i)?.GetMethod();
            var assembly = method?.DeclaringType?.Assembly;
            if (i == skipFrames) current = assembly;

            if (assembly != null && assembly != typeof(object).Assembly && assembly != current)
                return assembly;
        }

        return null;
    }

    public string AppName;
    public string AppIcon;
    public string AppVersion;
    public string AppShortVersion => Regex.Match(AppVersion, @"[0-9]+\.[0-9]+").Value;
    public string AppDescription;
    public string AppId => Regex.Replace(AppName, "[- .&]", "");
    public string AppExe => Regex.IsMatch(ToolExe, @"dotnet(\.exe)?$") ?
        $"{ToolExe} \"{Environment.GetCommandLineArgs()[0]}\"" :
        ToolExe;
    public void SaveResource(string resource, string path, bool isText = true)
    {
        Directory.CreateDirectory(path);
        var assemblies = new[] { Assembly.GetExecutingAssembly(), GetCallerAssembly() };
        var resnames = assemblies.SelectMany(a => a.GetManifestResourceNames()
            .Select(name => new
            {
                Assembly = a,
                Name = name
            }));
        var resourcepath = "." + resource;
        var res = resnames.FirstOrDefault(name => name.Name.EndsWith(resourcepath, StringComparison.OrdinalIgnoreCase));
        if (res == null) throw new System.IO.FileNotFoundException($"Resource {resource} not found");
        var dest = Path.Combine(path, resource);
        using (var stream = res.Assembly.GetManifestResourceStream(res.Name))
        {
            if (isText)
            {
                using (var reader = new StreamReader(stream))
                {
                    var text = reader.ReadToEnd()
                        .Replace("{{AppName}}", AppName)
                        .Replace("{{AppIcon}}", AppIcon)
                        .Replace("{{AppId}}", AppId)
                        .Replace("{{AppVersion}}", AppVersion)
                        .Replace("{{AppShortVersion}}", AppShortVersion)
                        .Replace("{{AppDescription}}", AppDescription)
                        .Replace("{{AppExe}}", AppExe);
                    if (!OSInfo.IsWindows) text = text.Replace("\r\n", "\n");
                    else text = Regex.Replace(text, "(?<!\r)\n", "\r\n");
                    File.WriteAllText(dest, text);
                }
            }
            else
            {
                using (var file = new FileStream(dest, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }
            }
        }
        if (!OSInfo.IsWindows)
        {
            Unix.SetFilePermissions(dest,
                UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupExecute | UnixFileMode.GroupRead |
                UnixFileMode.OtherExecute | UnixFileMode.OtherRead);
        }
    }
    public void SaveBinary(string resource, string path) => SaveResource(resource, path, false);
    public void SaveText(string resource, string path) => SaveResource(resource, path, true);

    public bool ForAllUsers => false;

    public string Applications => ForAllUsers ?
        "/Applications" :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Applications");

    public string ProgramFiles => ForAllUsers ?
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) :
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
    public string ToolExe => Environment.ProcessPath!;

    public void InstallWindows()
    {
        var path = Path.Combine(ProgramFiles, AppName);
        SaveBinary($"{AppIcon}.ico", path);
        var shortcut = new WindowsShortcut()
        {
            IconLocation = Path.Combine(path, $"{AppIcon}.ico"),
            Path = ToolExe,
            ShowCommand = ProcessWindowStyle.Normal
        };
        shortcut.Save(Path.Combine(path, AppName + ".lnk"));
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        shortcut.Save(Path.Combine(startMenu, AppName + ".lnk"));
    }

    public void UninstallWindows()
    {
        var path = Path.Combine(ProgramFiles, AppName);
        var shortcut = Path.Combine(path, AppName + ".lnk");
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var startMenuShortcut = Path.Combine(startMenu, AppName + ".lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
        if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);
        Directory.Delete(path, true);
    }

    public void InstallLinux()
    {
        var pixmaps = "/usr/share/pixmaps";
        var applications = "/usr/share/applications";
        var tmp = "/tmp";
        try
        {
            SaveText($"{AppName}.desktop", applications);
        } catch (FileNotFoundException) {
            SaveText("Application.desktop", tmp);
            File.Move(Path.Combine(tmp, "Application.desktop"), Path.Combine(applications, $"{AppName}.desktop"), true);
        }
        SaveBinary($"{AppIcon}.png", pixmaps);
    }

    public void UninstallLinux()
    {
        var pixmaps = "/usr/share/pixmaps";
        var applications = "/usr/share/applications";
        var desktop = Path.Combine(applications, $"{AppName}.desktop");
        var icon = Path.Combine(pixmaps, $"{AppIcon}.png");
        if (File.Exists (desktop)) File.Delete(desktop);
        if (File.Exists(icon)) File.Delete(icon);
    }

    public void InstallMac()
    {
        var path = Applications;
        var name = AppName;
        path = Path.Combine(path, name + ".app");
        var contents = Path.Combine(path, "Contents");
        var MacOS = Path.Combine(contents, "MacOS");
        var resources = Path.Combine(contents, "Resources");
        SaveText("Info.plist", contents);
        SaveText("launcher.sh", MacOS);
        SaveBinary($"{AppIcon}.icns", resources);
    }

    public void UninstallMac()
    {
        var path = Applications;
        var name = AppName;
        path = Path.Combine(path, name + ".app");
        Directory.Delete(path, true);
    }

    public static bool Run(string[] args, string appName, string iconName,
        string version = "1.0.0", string description = "")
    {
        if (args.Length > 0 && args[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) args = args.Skip(1).ToArray();

        if (args.Length > 0)
        {
            if (args[0] == "install")
            {
                var installer = new Installer() { AppName = appName, AppIcon = iconName,
                    AppDescription = description, AppVersion = version };
                if (OSInfo.IsWindows) installer.InstallWindows();
                else if (OSInfo.IsLinux) installer.InstallLinux();
                else if (OSInfo.IsMac) installer.InstallMac();
                Console.WriteLine($"{appName} installed. You can uninstall by running '{installer.ToolExe} uninstall'.");
                return true;
            }
            else if (args[0] == "uninstall")
            {
                var installer = new Installer() { AppName = appName, AppIcon = iconName,
                    AppDescription = description, AppVersion = version }
            ;
            if (OSInfo.IsWindows) installer.UninstallWindows();
                else if (OSInfo.IsLinux) installer.UninstallLinux();
                else if (OSInfo.IsMac) installer.UninstallMac();
                Console.WriteLine($"{appName} uninstalled.");
                return true;
            }
        }
        return false;
    }
}
