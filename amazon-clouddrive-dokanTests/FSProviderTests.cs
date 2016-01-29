using Xunit;
using Azi.ACDDokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive;
using System.IO;
using System.Threading;

namespace Azi.ACDDokanNet.Tests
{
    public abstract class FSProviderTestsBase : IDisposable
    {
        protected FSProvider provider;
        protected AmazonDrive amazon;

        protected async Task<AmazonDrive> Authenticate()
        {
            var settings = Tests.Properties.Settings.Default;
            var amazon = new AmazonDrive(AmazonSecret.clientId, AmazonSecret.clientSecret);
            amazon.OnTokenUpdate = (token, renew, expire) =>
            {
                settings.AuthToken = token;
                settings.AuthRenewToken = renew;
                settings.AuthTokenExpiration = expire;
                settings.Save();
            };

            if (!string.IsNullOrWhiteSpace(settings.AuthRenewToken))
            {
                if (await amazon.Authentication(
                    settings.AuthToken,
                    settings.AuthRenewToken,
                    settings.AuthTokenExpiration))
                {
                    return amazon;
                }
            }

            if (await amazon.SafeAuthenticationAsync(CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10)))
            {
                return amazon;
            }

            return null;
        }

        protected const string Testdir = "\\ACDDokanNetTest\\";

        protected FSProviderTestsBase()
        {
            amazon = Authenticate().Result;
            if (amazon == null)
            {
                throw new InvalidOperationException("Authentication failed");
            }

            DeleteDir("TempCache");

            provider = new FSProvider(amazon);
            provider.CachePath = "TempCache";
            provider.SmallFilesCacheSize = 20 * (1 << 20);
            provider.SmallFileSizeLimit = 1000 * (1 << 20);

            provider.DeleteDir("\\ACDDokanNetTest");
            provider.CreateDir("\\ACDDokanNetTest");
        }

        public void Dispose()
        {
            provider.DeleteDir("\\ACDDokanNetTest");
            provider.Dispose();
            try { DeleteDir(provider.CachePath); }
            catch (UnauthorizedAccessException)
            {
                // Ignore
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

        public class FSProviderTests : FSProviderTestsBase
        {
            [Fact]
            public void FSProviderTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void DeleteFileTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void CreateDeleteExistsDirTest()
            {
                var dir = Testdir + "DeleteTest";
                provider.CreateDir(dir);

                Assert.True(provider.Exists(dir));

                provider.DeleteDir(dir);

                Assert.False(provider.Exists(dir));
            }

            [Fact]
            public void ClearSmallFilesCacheTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void ExistsTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void CreateDirTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void OpenFileReadWriteTest()
            {
                var path = Testdir + "TestFile.txt";
                using (var file = provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
                {
                    file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
                }

                while (provider.GetItem(path).IsUploading)
                {
                    Thread.Sleep(500);
                }

                var info = provider.GetItem(path);
                Assert.Equal(10, info.Length);

                var buf = new byte[10];
                using (var file = provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
                {
                    file.Read(0, buf, 0, 10);
                }

                Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, buf);

                using (var file = provider.OpenFile(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, FileOptions.None))
                {
                    file.Write(9, new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }, 0, 8);
                }

                while (provider.GetItem(path).IsUploading)
                {
                    Thread.Sleep(500);
                }

                info = provider.GetItem(path);
                Assert.Equal(17, info.Length);

                buf = new byte[17];
                using (var file = provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
                {
                    file.Read(0, buf, 0, 17);
                }

                Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, buf);

                Assert.True(Directory.GetFiles("TempCache\\Upload").Length == 0);

                var buf2 = new byte[17];
                int red = amazon.Files.Download(info.Id, buf2, 0, 0, 17).Result;

                Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, buf2);
            }

            [Fact]
            public void OpenNewFileAndReadTest()
            {
                var path = Testdir + "TestFile.txt";
                using (var file = provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
                {
                    file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);

                    var buf = new byte[5];
                    using (var reader = provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
                    {
                        reader.Read(2, buf, 0, 5);
                    }

                    Assert.Equal(new byte[] { 3, 4, 5, 6, 7 }, buf);
                }

                while (provider.GetItem(path).IsUploading)
                {
                    Thread.Sleep(500);
                }

                Assert.True(Directory.GetFiles("TempCache\\Upload").Length == 0);

                var info = provider.GetItem(path);
                Assert.Equal(10, info.Length);

                var buf2 = new byte[17];
                int red = amazon.Files.Download(info.Id, buf2, 0, 0, 17).Result;

                Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, buf2);
            }

            [Fact]
            public void GetDirItemsTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void GetItemTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void MoveFileTest()
            {
                Assert.True(false, "This test needs an implementation");
            }

            [Fact]
            public void DisposeTest()
            {
                Assert.True(false, "This test needs an implementation");
            }
        }
    }
}