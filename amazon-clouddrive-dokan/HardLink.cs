using System;
using System.Runtime.InteropServices;

namespace Azi.ACDDokanNet
{

    public static class HardLink
    {
        public static bool Create(string targetPath, string hardLinkPath)
        {
            return NativeMethods.CreateHardLink(hardLinkPath, targetPath, IntPtr.Zero);
        }
    }
}
