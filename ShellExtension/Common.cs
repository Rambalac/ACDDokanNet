namespace Azi.ShellExtension
{
    using System.IO;
    using System.Linq;
    using Cloud.Common;
    using Newtonsoft.Json;
    using Trinet.Core.IO.Ntfs;

    public static class Common
    {
        public static INodeExtendedInfo ReadInfo(string path)
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

        public static string ReadString(string path, params string[] commands)
        {
            var streamName = string.Join(",", new[] { CloudDokanNetItemInfo.StreamName }.Concat(commands ?? Enumerable.Empty<string>()));
            using (var info = FileSystem.GetAlternateDataStream(path, streamName).OpenText())
            {
                return info.ReadToEnd();
            }
        }

        public static void WriteObject(object obj, string path, params string[] commands)
        {
            var str = JsonConvert.SerializeObject(obj);

            var streamName = string.Join(",", new[] { CloudDokanNetItemInfo.StreamName }.Concat(commands ?? Enumerable.Empty<string>()));
            var info = FileSystem.GetAlternateDataStream(path, streamName).OpenWrite();
            using (var writer = new StreamWriter(info))
            {
                writer.Write(str);
            }
        }
    }
}
