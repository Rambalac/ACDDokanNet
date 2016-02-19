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
    [COMServerAssociation(AssociationType.Drive, ".txt")]
    public class ContextMenu : SharpContextMenu
    {
        public const string ACDDokanNetInfoStreamName = "ACDDokanNetInfo";
        protected override bool CanShowMenu()
        {
            return SelectedItemPaths.All((path) => File.Exists($"{path}:{ACDDokanNetInfoStreamName}"));
        }

        protected override ContextMenuStrip CreateMenu()
        {
            //  Create the menu strip.
            var menu = new ContextMenuStrip();

            //  Create a 'count lines' item.
            var itemCountLines = new ToolStripMenuItem
            {
                Text = "Amazon Cloud Drive"
            };

            //  Add the item to the context menu.
            menu.Items.Add(itemCountLines);

            //  Return the menu.
            return menu;
        }
    }
}
