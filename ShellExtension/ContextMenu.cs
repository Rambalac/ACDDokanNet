namespace Azi.ShellExtension
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    using Cloud.Common;
    using Newtonsoft.Json;
    using SharpShell.Attributes;
    using SharpShell.SharpContextMenu;
    using Trinet.Core.IO.Ntfs;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible", Justification = "Must be COM visible")]
    [Guid("CAEE83F5-A0B4-4FAD-A94B-8CEB0A78EA54")]
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFiles)]
    [COMServerAssociation(AssociationType.Directory)]
    [COMServerAssociation(AssociationType.UnknownFiles)]
    [COMServerAssociation(AssociationType.Directory)]
    [COMServerAssociation(AssociationType.Drive)]
    public class ContextMenu : SharpContextMenu
    {
        protected new virtual IEnumerable<string> SelectedItemPaths
        {
            get { return (base.SelectedItemPaths.Count() > 0) ? base.SelectedItemPaths : new string[] { FolderPath }; }
        }

        protected static INodeExtendedInfo ReadInfo(string path)
        {
            using (var info = FileSystem.GetAlternateDataStream(path, CloudDokanNetItemInfo.StreamName).OpenText())
            {
                var text = info.ReadToEnd();
                var type = JsonConvert.DeserializeObject<NodeExtendedInfo>(text);
                if (type.Type == nameof(CloudDokanNetItemInfo))
                {
                    return JsonConvert.DeserializeObject<CloudDokanNetItemInfo>(text);
                }

                return null;
            }
        }

        protected static string ReadString(string path, params string[] commands)
        {
            var streamName = string.Join(",", new[] { CloudDokanNetItemInfo.StreamName }.Concat(commands ?? Enumerable.Empty<string>()));
            using (var info = FileSystem.GetAlternateDataStream(path, streamName).OpenText())
            {
                return info.ReadToEnd();
            }
        }

        protected static void WriteObject(object obj, string path, params string[] commands)
        {
            var str = JsonConvert.SerializeObject(obj);

            var streamName = string.Join(",", new[] { CloudDokanNetItemInfo.StreamName }.Concat(commands ?? Enumerable.Empty<string>()));
            using (var info = FileSystem.GetAlternateDataStream(path, streamName).OpenWrite())
            using (var writer = new StreamWriter(info))
            {
                writer.Write(str);
            }
        }

        protected override bool CanShowMenu()
        {
#if DEBUG
            EventLog.WriteEntry("ACDDokan.Net", $"ContextMenu in {FolderPath} create: {string.Join(";", SelectedItemPaths)}", EventLogEntryType.Warning, 0, 0);
#endif
            if (SelectedItemPaths.Count() > 0)
            {
                return SelectedItemPaths.All((path) => FileSystem.AlternateDataStreamExists(path, CloudDokanNetItemInfo.StreamName));
            }
            else
            {
                return FileSystem.AlternateDataStreamExists(FolderPath, CloudDokanNetItemInfo.StreamName);
            }
        }

        protected void CopyTempLink(object sender, EventArgs e)
        {
            Clipboard.SetText(string.Join("\r\n", SelectedItemPaths.Select(path => ReadInfo(path) as INodeExtendedInfoTempLink)
                .Where(info => info.TempLink != null).Select(info => info.TempLink)));
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

                if (Directory.Exists(SelectedItemPaths.Single()) && Clipboard.ContainsFileDropList())
                {
                    menu.Items.Add(new ToolStripMenuItem("Upload here", null, UploadHere));
                }

                if (info is INodeExtendedInfoWebLink)
                {
                    menu.Items.Add(new ToolStripMenuItem("Open in Browser", null, OpenInBrowser));
                }

                var romenu = new ToolStripMenuItem("Copy ReadOnly link", null, CopyReadOnlyLink);
                var rwmenu = new ToolStripMenuItem("Copy ReadWrite link", null, CopyReadWriteLink);

                if (info.CanShareReadOnly && info.CanShareReadWrite)
                {
                    menu.Items.Add(new ToolStripMenuItem(
                        "Share",
                        null,
                        new ToolStripMenuItem[] { romenu, rwmenu }));
                }
                else if (info.CanShareReadOnly)
                {
                    menu.Items.Add(romenu);
                }
                else if (info.CanShareReadWrite)
                {
                    menu.Items.Add(rwmenu);
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

            menu.Items.Add("-");

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

        private void OpenInBrowser(object sender, EventArgs e)
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

        private void UploadHere(object sender, EventArgs e)
        {
            var files = new CloudDokanNetUploadHereInfo
            {
                Files = Clipboard.GetFileDropList().Cast<string>().ToList()
            };
            var path = SelectedItemPaths.Single();
            WriteObject(files, path, CloudDokanNetUploadHereInfo.StreamName);
        }

        private void CopyReadOnlyLink(object sender, EventArgs e)
        {
            var path = SelectedItemPaths.Single();
            var link = ReadString(path, CloudDokanNetAssetInfo.StreamNameShareReadOnly);
            Clipboard.SetText(link);
        }

        private void CopyReadWriteLink(object sender, EventArgs e)
        {
            var path = SelectedItemPaths.Single();
            var link = ReadString(path, CloudDokanNetAssetInfo.StreamNameShareReadWrite);
            Clipboard.SetText(link);
        }
    }
}