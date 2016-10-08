using Xunit;
using Azi.Cloud.DokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using System.IO;

namespace Azi.Cloud.DokanNet.Tests
{
    public class BlockTests
    {
        [Fact]
        public async Task ReadFromStreamAndWaitTest()
        {
            var mockItem = new Mock<IAbsoluteCacheItem>();

            var block = new Block(mockItem.Object, 0, 20);
            var task = Task.Factory.StartNew(async () =>
              {
                  var str = new MemoryStream(new byte[] { 111 });
                  for (int i = 0; i < 10; i++)
                  {
                      var red = await block.ReadFromStream(str).ConfigureAwait(false);
                      str.Position = 0;
                      await Task.Delay(10).ConfigureAwait(false);
                  }
                  block.MakeComplete();
              });

            var lastUpdate = DateTime.MinValue;
            int lastSize = 0;
            while (!block.IsComplete)
            {
                var newUpdate = await block.WaitUpdate(lastUpdate).ConfigureAwait(false);
                Assert.NotEqual(lastUpdate, newUpdate);

                lastUpdate = newUpdate;
                var curSize = block.CurrentSize;
                Assert.NotEqual(lastSize, curSize);
            }
            Assert.Equal(10, block.CurrentSize);
        }

    }
}