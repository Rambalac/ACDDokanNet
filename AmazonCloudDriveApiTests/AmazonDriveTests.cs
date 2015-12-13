using Xunit;
using System;

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
                CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10));
        }

    }
}