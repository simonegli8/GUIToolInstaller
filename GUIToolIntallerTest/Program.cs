namespace GUIToolsIntallerTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (GUIToolInstaller.Installer.Run(args, "TestTool", "TestTool")) return;
            Console.WriteLine("Hello, World!");

        }
    }
}
