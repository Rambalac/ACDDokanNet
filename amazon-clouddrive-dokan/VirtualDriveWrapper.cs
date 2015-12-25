using DokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.AccessControl;
using FileAccess = DokanNet.FileAccess;
using Azi.Tools;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Azi.ACDDokanNet
{
    public class VirtualDriveWrapper
    {
        readonly VirtualDrive virtualDrive;
        public VirtualDriveWrapper(FSProvider provider)
        {
            virtualDrive = new VirtualDrive(provider);

        }

        public static void Unmount(char letter)
        {
            Dokan.Unmount(letter);
        }
        public void Mount(string path)
        {
            try
            {
#if DEBUG
                virtualDrive.Mount(path, DokanOptions.DebugMode | DokanOptions.NetworkDrive);
#else
                this.Mount(path, DokanOptions.NetworkDrive);
#endif
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}