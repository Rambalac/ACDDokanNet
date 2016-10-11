namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Newtonsoft.Json;
    using Tools;

    public abstract class AStatisticFileInfo
    {
        public abstract long Total { get; }

        public abstract string Id { get; }

        public abstract string FileName { get; }

        public abstract string Path { get; }

        public string ErrorMessage { get; set; }

        public bool HasError => ErrorMessage != null;

        public long Done { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Id == ((AStatisticFileInfo)obj).Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}