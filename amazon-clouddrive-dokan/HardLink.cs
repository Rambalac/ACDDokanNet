namespace Azi.Cloud.DokanNet
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    public static class HardLink
    {
        public static void Create(string targetPath, string hardLinkPath)
        {
            var res = NativeMethods.CreateHardLink(hardLinkPath, targetPath, IntPtr.Zero);
            if (!res)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}