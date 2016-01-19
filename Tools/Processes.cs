using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace Azi.Tools
{
    public static class Processes
    {
        class ProcInfo
        {
            internal string userName;
            internal readonly DateTime expire = DateTime.UtcNow.AddMilliseconds(10000);
        }
        static readonly ConcurrentDictionary<int, ProcInfo> pidOwner = new ConcurrentDictionary<int, ProcInfo>();

        static string GetOwner(int processId)
        {
            string query = "Select * From Win32_Process Where ProcessID = " + processId;
            var searcher = new ManagementObjectSearcher(query);
            var processList = searcher.Get();

            foreach (ManagementObject obj in processList)
            {
                string[] argList = { string.Empty, string.Empty };
                int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
                if (returnVal == 0)
                {
                    // return DOMAIN\user
                    return argList[1] + "\\" + argList[0];
                }
            }

            return "NO OWNER";
        }

        public static string GetProcessOwner(int id)
        {
            var result = pidOwner.GetOrAdd(id, (processId) => new ProcInfo { userName = GetOwner(processId) });
            if (DateTime.UtcNow > result.expire)
            {
                result = new ProcInfo { userName = GetOwner(id) };
                pidOwner[id] = result;
            }

            return result.userName;
        }
    }
}
