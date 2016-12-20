using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Azi.Cloud.DokanNet.Tests
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
        public async Task CreateDeleteExistsDirTest()
        {
            var dir = Testdir + "DeleteTest";
            await Provider.CreateDir(dir);

            Assert.True(await Provider.Exists(dir));

            await Provider.DeleteDir(dir);

            Assert.False(await Provider.Exists(dir));
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
        public async Task OpenFileReadWriteTest()
        {
            var path = Testdir + "TestFile.txt";
            using (var file = await Provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
            {
                await file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
            }

            while ((await Provider.FetchNode(path)).IsUploading)
            {
                await Task.Delay(500);
            }

            var info = await Provider.FetchNode(path);
            Assert.Equal(10, info.Length);

            var buf = new byte[10];
            using (var file = await Provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
            {
                await file.Read(0, buf, 0, 10, 30000);
            }

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, buf);

            using (var file = await Provider.OpenFile(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, FileOptions.None))
            {
                await file.Write(9, new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }, 0, 8, 30000);
            }

            while ((await Provider.FetchNode(path)).IsUploading)
            {
                await Task.Delay(500);
            }

            info = await Provider.FetchNode(path);
            Assert.Equal(17, info.Length);

            buf = new byte[17];
            using (var file = await Provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
            {
                await file.Read(0, buf, 0, 17, 30000);
            }

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, buf);

            Assert.True(Directory.GetFiles("TempCache\\Upload").Length == 0);

            var buf2 = new byte[17];
            var stream = await Amazon.Files.Download(info.Id);
            await stream.ReadAsync(buf2, 0, 17);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, buf2);
        }

        [Fact]
        public async Task OpenNewFileAndReadTest()
        {
            var path = Testdir + "TestFile.txt";
            using (var file = await Provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
            {
                await file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
                file.Flush();

                var buf = new byte[5];
                using (var reader = await Provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
                {
                    await reader.Read(2, buf, 0, 5);
                }

                Assert.Equal(new byte[] { 3, 4, 5, 6, 7 }, buf);
            }

            while ((await Provider.FetchNode(path)).IsUploading)
            {
                await Task.Delay(500);
            }

            Assert.True(Directory.GetFiles("TempCache\\Upload").Length == 0);

            var info = await Provider.FetchNode(path);
            Assert.Equal(10, info.Length);

            var buf2 = new byte[10];
            var stream = await Amazon.Files.Download(info.Id);
            await stream.ReadAsync(buf2, 0, 10);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, buf2);
        }

        [Theory]
        [InlineData("test&file.txt")]
        [InlineData("test%file.txt")]
        [InlineData("t&.txt")]
        [InlineData("t%.txt")]
        public async Task OpenNewFileWithNameAndReadTest(string name)
        {
            var path = Testdir + name;
            using (var file = await Provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
            {
                await file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
            }

            while ((await Provider.FetchNode(path)).IsUploading)
            {
                await Task.Delay(500);
            }

            Assert.True(Directory.GetFiles("TempCache\\Upload").Length == 0);

            var info = await Provider.FetchNode(path);
            Assert.Equal(10, info.Length);

            var buf2 = new byte[10];
            var stream = await Amazon.Files.Download(info.Id);
            await stream.ReadAsync(buf2, 0, 10);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, buf2);
        }

        [Fact]
        public async Task OpenNewBigFileAndReadTest()
        {
            var path = Testdir + "TestFile.txt";
            const int size = 1500000;
            var buffer = Enumerable.Range(1, size).Select(b => (byte)(b & 255)).ToArray();
            using (var file = await Provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
            {
                await file.Write(0, buffer, 0, buffer.Length);
                Debug.WriteLine("Uploaded");
            }

            while ((await Provider.FetchNode(path)).IsUploading)
            {
                await Task.Delay(500);
            }

            var info = await Provider.FetchNode(path);
            Assert.Equal(buffer.Length, info.Length);

            var newbuf = new byte[buffer.Length];
            var redtotal = 0;
            using (var file = await Provider.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
            {
                int red;
                var nextred = 1000000;
                do
                {
                    red = await file.Read(redtotal, newbuf, redtotal, 4096);
                    redtotal += red;
                    if (redtotal > nextred)
                    {
                        Debug.WriteLine($"Downloaded: {redtotal / 1000000}M");
                        nextred += 1000000;
                    }
                } while (red > 0);
            }

            Assert.Equal(buffer.Length, redtotal);

            Assert.Equal(Enumerable.Range(1, size).Select(b => (byte)(b & 255)), newbuf);
        }

        [Fact]
        public async Task ReadWrongStreamTest()
        {
            var path = Testdir + "TestFile.txt";
            using (var file = await Provider.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.None))
            {
                await file.Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
            }

            while ((await Provider.FetchNode(path)).IsUploading)
            {
                await Task.Delay(500);
            }

            var info = await Provider.FetchNode(path);
            Assert.Equal(10, info.Length);

            var file2 = await Provider.OpenFile(path + "\\:ACDDokanNetInfo:$DATA", FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None);
            Assert.Null(file2);
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