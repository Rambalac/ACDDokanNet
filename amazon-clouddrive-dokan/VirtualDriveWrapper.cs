namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using global::DokanNet;
    using Tools;

    public class VirtualDriveWrapper
    {
        private readonly VirtualDrive virtualDrive;

        private char mountLetter;

        public VirtualDriveWrapper(FSProvider provider)
        {
            virtualDrive = new VirtualDrive(provider);
            virtualDrive.OnMount = () =>
            {
                Mounted?.Invoke(mountLetter);
            };
            virtualDrive.OnUnmount = () =>
            {
                Unmounted?.Invoke(mountLetter);
            };
        }

        public Action<char> Mounted { get; set; }

        public Action<char> Unmounted { get; set; }

        public static IList<char> GetFreeDriveLettes()
        {
            return Enumerable.Range('C', 'Z' - 'C' + 1).Select(c => (char)c).Except(Environment.GetLogicalDrives().Select(s => s[0])).ToList();
        }

        public static void Unmount(char letter)
        {
            Dokan.Unmount(letter);
        }

        public void Mount(char letter, bool readOnly)
        {
            Contract.Ensures(letter > 'C' && letter <= 'Z');
            try
            {
                virtualDrive.ReadOnly = readOnly;
                virtualDrive.MountPath = letter + ":\\";
                mountLetter = letter;
#if DEBUG
                virtualDrive.Mount(virtualDrive.MountPath, DokanOptions.DebugMode | DokanOptions.AltStream | DokanOptions.FixedDrive, 0, 800, TimeSpan.FromSeconds(30));
#else
                virtualDrive.Mount(virtualDrive.MountPath, DokanOptions.AltStream | DokanOptions.FixedDrive, 0, 800, TimeSpan.FromSeconds(30));
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