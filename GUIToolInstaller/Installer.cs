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
        var assemblies = new[] { GetCallerAssembly(), Assembly.GetExecutingAssembly() };
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
        var path = Path.Combine(ProgramFiles, AppId);
        SaveBinary($"{AppIcon}.ico", path);
        var shortcut = new WindowsShortcut()
        {
            IconLocation = Path.Combine(path, $"{AppIcon}.ico"),
            Path = ToolExe,
            ShowCommand = ProcessWindowStyle.Normal
        };
        shortcut.Save(Path.Combine(path, AppId + ".lnk"));
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        shortcut.Save(Path.Combine(startMenu, AppId + ".lnk"));
    }

    public void UninstallWindows()
    {
        var path = Path.Combine(ProgramFiles, AppId);
        var shortcut = Path.Combine(path, AppId + ".lnk");
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var startMenuShortcut = Path.Combine(startMenu, AppId + ".lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
        if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);
        Directory.Delete(path, true);
    }
    public string ReadPassword()
    {
        var password = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b"); // erase last *
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        }

        return password.ToString();
    }
    public void RestartAsRoot()
    {
        if (OSInfo.IsLinux || OSInfo.IsMac)
        {
            if (Unix.getuid() != 0)
            {
                var commandLine = Environment.CommandLine;
                Console.WriteLine("Installation must run as Administrator.");
                Console.WriteLine("Please provide your password:");
                var password = ReadPassword();
                var shell = Shell.Standard.Clone;
                shell.LogOutput += msg => Console.WriteLine(msg);
                shell.LogError += msg => Console.Error.WriteLine(msg);
                shell = Shell.Standard.Clone.ExecAsync($"sudo -S {commandLine}");
                shell.Input.WriteLine(password);
                shell.Wait();
            }
        }
    }

    public void InstallLinux()
    {
        RestartAsRoot();

        var pixmaps = "/usr/share/pixmaps";
        var applications = "/usr/share/applications";
        var tmp = "/tmp";
        try
        {
            SaveText($"{AppId}.desktop", applications);
        } catch (FileNotFoundException) {
            SaveText("Application.desktop", tmp);
            File.Move(Path.Combine(tmp, "Application.desktop"), Path.Combine(applications, $"{AppId}.desktop"), true);
        }
        SaveBinary($"{AppIcon}.png", pixmaps);
    }

    public void UninstallLinux()
    {
        RestartAsRoot();

        var pixmaps = "/usr/share/pixmaps";
        var applications = "/usr/share/applications";
        var desktop = Path.Combine(applications, $"{AppId}.desktop");
        var icon = Path.Combine(pixmaps, $"{AppIcon}.png");
        if (File.Exists (desktop)) File.Delete(desktop);
        if (File.Exists(icon)) File.Delete(icon);
    }

    public void InstallMac()
    {
        RestartAsRoot();

        var path = Applications;
        var name = AppId;
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
        RestartAsRoot();

        var path = Applications;
        var name = AppId;
        path = Path.Combine(path, name + ".app");
        Directory.Delete(path, true);
    }

    public static bool Run(string[] args, string appName, string iconName = null,
        string description = "", string version = null)
    {
        if (args.Length > 0 && args[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) args = args.Skip(1).ToArray();

        if (args.Length > 0)
        {
            try {
                if (args[0] == "install")
                {
                    if (string.IsNullOrEmpty(version))
                    {
                        var assembly = GetCallerAssembly();
                        version = assembly.GetName().Version.ToString(3);
                    }
                    if (string.IsNullOrEmpty(iconName)) iconName = appName;
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
                    if (string.IsNullOrEmpty(version))
                    {
                        var assembly = GetCallerAssembly();
                        version = assembly.GetName().Version.ToString(3);
                    }
                    if (string.IsNullOrEmpty(iconName)) iconName = appName;
                    var installer = new Installer() { AppName = appName, AppIcon = iconName,
                        AppDescription = description, AppVersion = version };
                    if (OSInfo.IsWindows) installer.UninstallWindows();
                    else if (OSInfo.IsLinux) installer.UninstallLinux();
                    else if (OSInfo.IsMac) installer.UninstallMac();
                    Console.WriteLine($"{appName} uninstalled.");
                    return true;
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return true;
            }
        }
        return false;
    }
}
