namespace Azi.Tools
{
    using System;
    using System.Collections.Concurrent;
    using System.Management;

    public static class Processes
    {
        private static readonly ConcurrentDictionary<int, ProcInfo> PidOwner = new ConcurrentDictionary<int, ProcInfo>();

        public static string GetProcessOwner(int id)
        {
            var result = PidOwner.GetOrAdd(id, (processId) => new ProcInfo { UserName = GetOwner(processId) });
            if (DateTime.UtcNow > result.Expire)
            {
                result = new ProcInfo { UserName = GetOwner(id) };
                PidOwner[id] = result;
            }

            return result.UserName;
        }

        private static string GetOwner(int processId)
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

        private class ProcInfo
        {
            public string UserName { get; set; }

            public DateTime Expire { get; } = DateTime.UtcNow.AddMilliseconds(10000);
        }
    }
}
