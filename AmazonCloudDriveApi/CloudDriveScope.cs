using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azi.Amazon.CloudDrive
{
    [Flags]
    public enum CloudDriveScope
    {
        ReadImage = 1,
        ReadVideo = 2,
        ReadDocument = 4,
        ReadOther = 8,
        ReadAll = 16,
        Write = 32
    }
}
