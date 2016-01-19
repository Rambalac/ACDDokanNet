using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Azi.ACDDokanNet
{
    public static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        internal static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool CreateHardLink(
          string lpFileName,
          string lpExistingFileName,
          IntPtr lpSecurityAttributes
          );


        public enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }
    }

    public static class HardLink
    {
        public static bool Create(string TargetPath, string HardLinkPath)
        {
            return NativeMethods.CreateHardLink(HardLinkPath, TargetPath, IntPtr.Zero);
        }
    }
    public static class SymbolicLink
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
