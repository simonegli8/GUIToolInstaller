using System.Diagnostics;

namespace GUIToolsIntallerTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("-debug"))
            {
                Console.WriteLine("Waiting for debugger to attach...");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(200);
                }
                Debugger.Break();
            }
            if (GUIToolInstaller.Installer.Run(args, "TestTool", "TestTool")) return;
            Console.WriteLine("Hello, World!");
        }
    }
}
