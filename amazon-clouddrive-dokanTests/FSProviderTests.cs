using Xunit;
using System.IO;
using System.Threading;

namespace Azi.ACDDokanNet.Tests
{

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
            Provider.CreateDir(dir);

            Assert.True(Provider.Exists(dir));

            Provider.DeleteDir(dir);

            Assert.False(Provider.Exists(dir));
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
            using (var file = Provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
            {
                file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
            }

            while (Provider.GetItem(path).IsUploading)
            {
                Thread.Sleep(500);
            }

            var info = Provider.GetItem(path);
            Assert.Equal(10, info.Length);

            var buf = new byte[10];
            using (var file = Provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
            {
                file.Read(0, buf, 0, 10);
            }

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, buf);

            using (var file = Provider.OpenFile(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, FileOptions.None))
            {
                file.Write(9, new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }, 0, 8);
            }

            while (Provider.GetItem(path).IsUploading)
            {
                Thread.Sleep(500);
            }

            info = Provider.GetItem(path);
            Assert.Equal(17, info.Length);

            buf = new byte[17];
            using (var file = Provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
            {
                file.Read(0, buf, 0, 17);
            }

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, buf);

            Assert.True(Directory.GetFiles("TempCache\\Upload").Length == 0);

            var buf2 = new byte[17];
            int red = Amazon.Files.Download(info.Id, buf2, 0, 0, 17).Result;

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, buf2);
        }

        [Fact]
        public void OpenNewFileAndReadTest()
        {
            var path = Testdir + "TestFile.txt";
            using (var file = Provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
            {
                file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);

                var buf = new byte[5];
                using (var reader = Provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
                {
                    reader.Read(2, buf, 0, 5);
                }

                Assert.Equal(new byte[] { 3, 4, 5, 6, 7 }, buf);
            }

            while (Provider.GetItem(path).IsUploading)
            {
                Thread.Sleep(500);
            }

            Assert.True(Directory.GetFiles("TempCache\\Upload").Length == 0);

            var info = Provider.GetItem(path);
            Assert.Equal(10, info.Length);

            var buf2 = new byte[17];
            int red = Amazon.Files.Download(info.Id, buf2, 0, 0, 17).Result;

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
