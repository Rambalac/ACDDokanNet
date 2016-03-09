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
using Azi.Cloud.Common;

namespace Azi.ShellExtension
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible", Justification = "Must be COM visible")]
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFiles)]
    [COMServerAssociation(AssociationType.Directory)]
    public class ContextMenu : SharpContextMenu
    {
        protected new virtual IEnumerable<string> SelectedItemPaths
        {
            get { return base.SelectedItemPaths; }
        }

        protected static object ReadInfo(string path)
        {
            using (var info = FileSystem.GetAlternateDataStream(path, ACDDokanNetItemInfo.ACDDokanNetItemInfoStreamName).OpenText())
            {
                string text = info.ReadToEnd();
                var type = JsonConvert.DeserializeObject<NodeExtendedInfo>(text);
                if (type.Type == nameof(ACDDokanNetItemInfo))
                {
                    return JsonConvert.DeserializeObject<ACDDokanNetItemInfo>(text);
                }

                return null;
            }
        }

        protected override bool CanShowMenu()
        {
            return SelectedItemPaths.All((path) => FileSystem.AlternateDataStreamExists(path, ACDDokanNetItemInfo.ACDDokanNetItemInfoStreamName));
        }

        protected override ContextMenuStrip CreateMenu()
        {
            // Create the menu strip.
            var menu = new ContextMenuStrip();

            menu.Items.Add("-");
            if (SelectedItemPaths.Count() == 1)
            {
                var info = ReadInfo(SelectedItemPaths.Single());
                if (File.Exists(SelectedItemPaths.Single()) && info is INodeExtendedInfoTempLink)
                {
                    menu.Items.Add(new ToolStripMenuItem("Open as temp link", null, OpenAsUrl));
                }

                if (info is INodeExtendedInfoWebLink)
                {
                    menu.Items.Add(new ToolStripMenuItem("Open in Browser", null, OpenInBrowser));
                }
            }

            var firstFile = SelectedItemPaths.FirstOrDefault(File.Exists);
            if (firstFile != null)
            {
                var info = ReadInfo(firstFile);
                if (info is INodeExtendedInfoTempLink)
                {
                    menu.Items.Add(new ToolStripMenuItem("Copy temp links", null, CopyTempLink));
                }
            }

            // Return the menu.
            return menu;
        }

        protected void OpenAsUrl(object sender, EventArgs e)
        {
            var info = ReadInfo(SelectedItemPaths.Single()) as INodeExtendedInfoTempLink;
            if (info == null)
            {
                return;
            }

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
            var infoTemp = info as INodeExtendedInfoTempLink;
            var infoWeb = info as INodeExtendedInfoWebLink;
            string link = infoTemp?.TempLink ?? infoWeb?.WebLink;
            if (link != null)
            {
                Process.Start(infoTemp?.TempLink ?? infoWeb?.WebLink);
            }
        }

        protected void CopyTempLink(object sender, EventArgs e)
        {
            Clipboard.SetText(string.Join("\r\n", SelectedItemPaths.Select(path => ReadInfo(path) as INodeExtendedInfoTempLink)
                .Where(info => info.TempLink != null).Select(info => info.TempLink)));
        }
    }
}
