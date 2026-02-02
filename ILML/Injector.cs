using System;
using System.IO;
using System.Text;

internal static class Injector
{
    public static void Inject(
        Native.PROCESS_INFORMATION pi,
        string dllPath
    )
    {
        byte[] dllBytes = Encoding.Unicode.GetBytes(dllPath + "\0");

        IntPtr remoteMem = Native.VirtualAllocEx(
            pi.hProcess,
            IntPtr.Zero,
            (uint)dllBytes.Length,
            Native.MEM_COMMIT | Native.MEM_RESERVE,
            Native.PAGE_READWRITE
        );

        if (remoteMem == IntPtr.Zero)
            throw new Exception("VirtualAllocEx failed");

        Native.WriteProcessMemory(
            pi.hProcess,
            remoteMem,
            dllBytes,
            (uint)dllBytes.Length,
            out _
        );

        IntPtr loadLibrary = Native.GetProcAddress(
            Native.GetModuleHandle("kernel32.dll"),
            "LoadLibraryW"
        );

        if (loadLibrary == IntPtr.Zero)
            throw new Exception("LoadLibraryW not found");

        Native.CreateRemoteThread(
            pi.hProcess,
            IntPtr.Zero,
            0,
            loadLibrary,
            remoteMem,
            0,
            IntPtr.Zero
        );
    }
}
