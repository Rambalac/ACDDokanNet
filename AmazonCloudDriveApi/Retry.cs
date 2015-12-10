using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmazonCloudDriveApi
{
    public static class Retry
    {
        public static bool Do(int times, Func<bool> act)
        {
            while (times > 0)
            {
                if (act()) return true;
                times--;
            }
            return false;
        }
    }
}
