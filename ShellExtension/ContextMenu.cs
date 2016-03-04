using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Newtonsoft.Json;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using Trinet.Core.IO.Ntfs;

namespace ShellExtension
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible", Justification = "Must be COM visible")]
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFiles)]
    [COMServerAssociation(AssociationType.Directory)]
    public class ContextMenu : SharpContextMenu
    {
        public const string ACDDokanNetInfoStreamName = "ACDDokanNetInfo";

        protected new virtual IEnumerable<string> SelectedItemPaths
        {
            get { return base.SelectedItemPaths; }
        }

        protected static ACDDokanNetItemInfo ReadInfo(string path)
        {
            using (var info = FileSystem.GetAlternateDataStream(path, ACDDokanNetInfoStreamName).OpenText())
            {
                return JsonConvert.DeserializeObject<ACDDokanNetItemInfo>(info.ReadToEnd());
            }
        }

        protected override bool CanShowMenu()
        {
            return SelectedItemPaths.All((path) => FileSystem.AlternateDataStreamExists(path, ACDDokanNetInfoStreamName));
        }

        protected override ContextMenuStrip CreateMenu()
        {
            // Create the menu strip.
            var menu = new ContextMenuStrip();

            menu.Items.Add("-");
            if (SelectedItemPaths.Count() == 1)
            {
                if (File.Exists(SelectedItemPaths.Single()))
                {
                    menu.Items.Add(new ToolStripMenuItem("Open as temp link", null, OpenAsUrl));
                }

                menu.Items.Add(new ToolStripMenuItem("Open in Browser", null, OpenInBrowser));
            }

            if (SelectedItemPaths.Any(File.Exists))
            {
                menu.Items.Add(new ToolStripMenuItem("Copy temp links", null, CopyTempLink));
            }

            // Return the menu.
            return menu;
        }

        protected void OpenAsUrl(object sender, EventArgs e)
        {
            var info = ReadInfo(SelectedItemPaths.Single());
            if (info.TempLink == null)
            {
                return;
            }

            var command = NativeMethods.AssocQueryString(Path.GetExtension(SelectedItemPaths.Single()));

            command = command.Replace("%1", info.TempLink);
            command = command.Replace("%L", info.TempLink);

            var process = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/S /C \"{command}\"";
            process.StartInfo = startInfo;
            process.Start();
        }

        protected void OpenInBrowser(object sender, EventArgs e)
        {
            var info = ReadInfo(SelectedItemPaths.Single());
            Process.Start(info.TempLink ?? info.WebLink);
        }

        protected void CopyTempLink(object sender, EventArgs e)
        {
            Clipboard.SetText(string.Join("\r\n", SelectedItemPaths.Select(path => ReadInfo(path))
                .Where(info => info.TempLink != null).Select(info => info.TempLink)));
        }
    }
}
