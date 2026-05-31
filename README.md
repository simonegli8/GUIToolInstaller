# GUIToolsInstaller

This project helps adding an installer for dotnet GUI tools so they can be installed into the start menu.

First you'll install your tool with `dotnet tool install -g yourtool`. Then, if you include this library,
your tool can be called via `yourtool install` to create a shortcut in the start menu to start your tool.
Likewise it can be uninstalled by calling `yourtool uninstall`.

In order to make this work, you must include the following code in your Program.cs

```
public void Main(string[] args) {
	if (GUIToolInstaller.Installer.Run(args, "Application Name", "ApplicationIcon",
		"Description describing the application")) return;

	... your other code here ...

}
```