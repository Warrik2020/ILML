using System;
using System.IO;

class Program
{
    static void Main()
    {
        string gameExe = "Iron Lung.exe";
        string coreDll = Path.GetFullPath("ModLoader.Core.Native.dll");

        if (!File.Exists(gameExe))
        {
            Console.WriteLine("Iron Lung.exe not found");
            return;
        }

        if (!File.Exists(coreDll))
        {
            Console.WriteLine("Core native DLL not found");
            return;
        }

        var si = new Native.STARTUPINFO
        {
            cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.STARTUPINFO>()
        };

        if (!Native.CreateProcessW(
            gameExe,
            " -logFile log.txt",
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            Native.CREATE_SUSPENDED,
            IntPtr.Zero,
            null,
            ref si,
            out var pi
        ))
        {
            Console.WriteLine("Failed to start game");
            return;
        }

        Injector.Inject(pi, coreDll);
        Native.ResumeThread(pi.hThread);

        Console.WriteLine("Injected successfully");
    }
}
