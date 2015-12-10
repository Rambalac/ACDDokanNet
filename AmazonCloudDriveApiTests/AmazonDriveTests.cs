using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;

namespace Azi.Amazon.CloudDrive.Tests
{
    public class AmazonDriveTests
    {
        [Fact]
        public async void SafeAuthenticationAsyncTest()
        {
            var amazon = new AmazonDrive();
            await amazon.SafeAuthenticationAsync(
                AmazonSecret.clientId,
                AmazonSecret.clientSecret,
                CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromSeconds(60));
        }

    }
}