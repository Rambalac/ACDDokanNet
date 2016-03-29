using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Azi.Cloud.Common;
using Azi.Cloud.AmazonCloudDrive;

namespace Azi.Cloud.DokanNet.Tests
{
    public abstract class FSProviderTestsBase : IDisposable, IAuthUpdateListener
    {
        protected const string Testdir = "\\ACDDokanNetTest\\";

        private bool disposedValue = false; // To detect redundant calls

        protected FSProviderTestsBase()
        {
            Amazon = Authenticate().Result;
            if (Amazon == null)
            {
                throw new InvalidOperationException("Authentication failed");
            }

            DeleteDir("TempCache");

            Provider = new FSProvider(Amazon);
            Provider.CachePath = "TempCache";
            Provider.SmallFilesCacheSize = 20 * (1 << 20);
            Provider.SmallFileSizeLimit = 1 * (1 << 20);

            Provider.DeleteDir("\\ACDDokanNetTest");
            Provider.CreateDir("\\ACDDokanNetTest");
        }

        protected FSProvider Provider { get; set; }

        protected IHttpCloud Amazon { get; set; }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected async Task<IHttpCloud> Authenticate()
        {
            var settings = Properties.Settings.Default;
            var amazon = new AmazonCloud();
            amazon.OnAuthUpdated = this;
            var cs = new CancellationTokenSource();

            if (!string.IsNullOrWhiteSpace(settings.AuthToken))
            {
                if (await amazon.AuthenticateSaved(cs.Token, settings.AuthToken))
                {
                    return amazon;
                }
            }

            if (await amazon.AuthenticateNew(cs.Token))
            {
                return amazon;
            }

            return null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Provider.DeleteDir("\\ACDDokanNetTest");
                    Provider.Dispose();
                    try
                    {
                        DeleteDir(Provider.CachePath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        private void DeleteDir(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDir(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Thread.Sleep(100);
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
                Directory.Delete(path, true);
            }
        }

        public void OnAuthUpdated(IHttpCloud sender, string authinfo)
        {
            var settings = Properties.Settings.Default;
            settings.AuthToken = authinfo;
            settings.Save();
        }
    }
}
