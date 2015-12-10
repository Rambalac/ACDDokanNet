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
            await amazon.SafeAuthenticationAsync("amzn1.application-oa2-client.ce45fa54a8ec4daeb7f082343fa1e8d8", CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromSeconds(60));
        }

    }
}