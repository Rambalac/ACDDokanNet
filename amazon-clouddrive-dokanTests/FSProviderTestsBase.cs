using Azi.Cloud.AmazonCloudDrive;
using Azi.Cloud.Common;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azi.Tools;

namespace Azi.Cloud.DokanNet.Tests
{
    public abstract class FSProviderTestsBase : IDisposable, IAuthUpdateListener
    {
        protected const string Testdir = "\\ACDDokanNetTest\\";

        private bool disposedValue; // To detect redundant calls

        protected async Task Init()
        {
            Amazon = await Authenticate();
            if (Amazon == null)
            {
                throw new InvalidOperationException("Authentication failed");
            }

            DeleteDir("TempCache");

            Provider = new FSProvider(Amazon, (a, b, c) => Task.FromResult(0))
            {
                CachePath = "TempCache",
                SmallFilesCacheSize = 20*(1 << 20),
                SmallFileSizeLimit = 1*(1 << 20)
            };

            try
            {
                await Provider.DeleteDir("\\ACDDokanNetTest");
            }
            catch (FileNotFoundException)
            {
                //Ignore
            }
            await Provider.CreateDir("\\ACDDokanNetTest");
        }

        protected FSProviderTestsBase()
        {
            UnitTestDetector.IsUnitTest = true;
            StartSTATask(Init).Wait();
        }

        public static Task StartSTATask(Func<Task> action)
        {
            var tcs = new TaskCompletionSource<object>();
            var thread = new Thread(async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult(new object());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return tcs.Task;
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
            var amazon = new AmazonCloud
            {
                OnAuthUpdated = this,
                Id = "UnitTest" + Guid.NewGuid()
            };

            using (var cs = new CancellationTokenSource())
            {

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
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Provider != null)
                    {
                        Provider.DeleteDir("\\ACDDokanNetTest").Wait();
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

            foreach (var directory in Directory.GetDirectories(path))
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