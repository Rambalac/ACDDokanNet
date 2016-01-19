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

        public static IList<char> GetFreeDriveLettes()
        {
            return Enumerable.Range('C', 'Z' - 'C' + 1).Select(c => (char)c).Except(Environment.GetLogicalDrives().Select(s => s[0])).ToList();
        }

        public VirtualDriveWrapper(FSProvider provider)
        {
            virtualDrive = new VirtualDrive(provider);
            virtualDrive.OnMount = () =>
            {
                Mounted?.Invoke();
            };
            virtualDrive.OnUnmount = () =>
            {
                Unmounted?.Invoke();
            };
        }

        public static void Unmount(char letter)
        {
            Dokan.Unmount(letter);
        }

        public Action Mounted;
        public Action Unmounted;
        public void Mount(string path, bool readOnly)
        {
            try
            {
                virtualDrive.ReadOnly = readOnly;
#if DEBUG
                virtualDrive.Mount(path, DokanOptions.DebugMode | DokanOptions.FixedDrive, 0, 800, TimeSpan.FromSeconds(30));
#else
                virtualDrive.Mount(path, DokanOptions.FixedDrive,0, 800, TimeSpan.FromSeconds(30));
                virtualDrive.MountPath = path;
#endif
            }
            catch (DokanException e)
            {
                Log.Error(e);
                throw new InvalidOperationException(e.Message, e);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}