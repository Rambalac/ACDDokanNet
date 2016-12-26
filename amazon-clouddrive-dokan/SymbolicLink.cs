namespace Azi.Cloud.DokanNet
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    public static class SymbolicLink
    {
        public static void CreateFile(string targetPath, string symlinkPath)
        {
            var res = NativeMethods.CreateSymbolicLink(symlinkPath, targetPath, NativeMethods.SymbolicLink.File);
            if (!res)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public static void CreateDir(string targetPath, string symlinkPath)
        {
            var res = NativeMethods.CreateSymbolicLink(symlinkPath, targetPath, NativeMethods.SymbolicLink.Directory);
            if (!res)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}