using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            return true;
            //return SelectedItemPaths.All((path) => File.Exists($"{path}:{ACDDokanNetInfoStreamName}"));
        }

        protected override ContextMenuStrip CreateMenu()
        {
            //  Create the menu strip.
            var menu = new ContextMenuStrip();

            //  Add the item to the context menu.
            menu.Items.Add(new ToolStripMenuItem("Amazon Cloud Drive", null,
                    new ToolStripMenuItem("Copy temp link", null, CopyTempLink)
                ));

            //  Return the menu.
            return menu;
        }

        private void CopyTempLink(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
