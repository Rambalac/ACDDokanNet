using Azi.ACDDokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACD.DokanNet.Gui
{
    public class ViewModel
    {
        public IList<char> DriveLetters => FSProvider.GetFreeDriveLettes();
    }
}
