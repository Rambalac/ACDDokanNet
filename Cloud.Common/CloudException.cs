namespace Azi.Cloud.Common
{
    using System;
    using System.Net;

    [Serializable]
    public class CloudException : Exception
    {
        public CloudException(HttpStatusCode error, Exception ex)
            : base(error.ToString(), ex)
        {
            Error = error;
        }

        public HttpStatusCode Error { get; }
    }
}
