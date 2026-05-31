# GUIToolsInstaller

This project helps adding a installer for dotnet GUI tools so they can be installed into the start menu.

If you include this library, your tool can be called via `yourtool install` to create a shortcut in the start menu
to start your tool. Likewise it can be uninstalled by calling `yourtool uninstall`.

In order to make this work, you must include the following code in your Program.cs

```
public void