using Azi.Cloud.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cloud.Common
{
    public interface IAuthUpdateListener
    {
        void OnAuthUpdated(IHttpCloud sender, string authinfo);
    }
}
