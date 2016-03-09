using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Azi.Cloud.Common
{
    public class CloudException : Exception
    {
        public HttpStatusCode Error { get; }

        public CloudException(HttpStatusCode error, Exception ex) : base(error.ToString(), ex)
        {
            Error = error;
        }
    }
}
