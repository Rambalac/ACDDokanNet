using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Azi.ACDDokanNet
{
    public class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        public enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }
    }

    public class SymbolicLink
    {
        public static bool CreateFile(string TargetPath, string SymlinkPath)
        {
            return NativeMethods.CreateSymbolicLink(SymlinkPath, TargetPath, NativeMethods.SymbolicLink.File);
        }
        public static bool CreateDir(string TargetPath, string SymlinkPath)
        {
            return NativeMethods.CreateSymbolicLink(SymlinkPath, TargetPath, NativeMethods.SymbolicLink.Directory);
        }
    }
}
