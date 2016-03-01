using System;
using System.Runtime.InteropServices;

namespace Azi.ACDDokanNet
{

    public static class SymbolicLink
    {
        public static bool CreateFile(string targetPath, string symlinkPath)
        {
            return NativeMethods.CreateSymbolicLink(symlinkPath, targetPath, NativeMethods.SymbolicLink.File);
        }

        public static bool CreateDir(string targetPath, string symlinkPath)
        {
            return NativeMethods.CreateSymbolicLink(symlinkPath, targetPath, NativeMethods.SymbolicLink.Directory);
        }
    }
}
