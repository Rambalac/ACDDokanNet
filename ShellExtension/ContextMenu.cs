using Microsoft.Win32;
using Newtonsoft.Json;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Trinet.Core.IO.Ntfs;

namespace ShellExtension
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFiles)]
    [COMServerAssociation(AssociationType.Directory)]
    public class ContextMenu : SharpContextMenu
    {
        public const string ACDDokanNetInfoStreamName = "ACDDokanNetInfo";
        protected override bool CanShowMenu()
        {
            return SelectedItemPaths.All((path) => FileSystem.AlternateDataStreamExists(path, ACDDokanNetInfoStreamName));
        }

        protected override ContextMenuStrip CreateMenu()
        {
            //  Create the menu strip.
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
            if (SelectedItemPaths.Any(path => File.Exists(path)))
                menu.Items.Add(new ToolStripMenuItem("Copy temp links", null, CopyTempLink));

            //  Return the menu.
            return menu;
        }

        private void OpenAsUrl(object sender, EventArgs e)
        {
            var info = ReadInfo(SelectedItemPaths.Single());
            if (info.TempLink == null) return;
            var command = NativeMethdos.AssocQueryString(Path.GetExtension(SelectedItemPaths.Single()));

            command = command.Replace("%1", info.TempLink);

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C "+command;
            process.StartInfo = startInfo;
            process.Start();
        }

        private void OpenInBrowser(object sender, EventArgs e)
        {
            var info = ReadInfo(SelectedItemPaths.Single());
            Process.Start(info.TempLink ?? info.WebLink);
        }

        private void CopyTempLink(object sender, EventArgs e)
        {
            Clipboard.SetText(string.Join("\r\n", SelectedItemPaths.Select(path => ReadInfo(path))
                .Where(info => info.TempLink != null).Select(info => info.TempLink)));
        }

        private static ACDDokanNetItemInfo ReadInfo(string path)
        {
            using (var info = FileSystem.GetAlternateDataStream(path, ACDDokanNetInfoStreamName).OpenText())
            {
                return JsonConvert.DeserializeObject<ACDDokanNetItemInfo>(info.ReadToEnd());
            }
        }
    }
}
