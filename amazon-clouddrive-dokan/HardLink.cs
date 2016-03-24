using System;

namespace Azi.Cloud.DokanNet
{
    public static class HardLink
    {
        public static bool Create(string targetPath, string hardLinkPath)
        {
            return NativeMethods.CreateHardLink(hardLinkPath, targetPath, IntPtr.Zero);
        }
    }
}
