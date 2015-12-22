using Xunit;
using System;

namespace Azi.Amazon.CloudDrive.Tests
{
    public class AmazonDriveTests
    {
        [Fact]
        public async void SafeAuthenticationAsyncTest()
        {
            var amazon = new AmazonDrive(AmazonSecret.clientId, AmazonSecret.clientSecret);
            await amazon.SafeAuthenticationAsync(CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10));
        }

    }
}