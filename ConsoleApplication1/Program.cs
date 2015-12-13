using amazon_clouddrive_dokan;
using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Tests;
using System;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
                var amazon = new AmazonDrive();
                amazon.SafeAuthenticationAsync(
                    AmazonSecret.clientId,
                    AmazonSecret.clientSecret,
                    CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10)).Wait();


                var cloudDrive = new VirtualDrive(new FSProvider(amazon));
                cloudDrive.Mount("r:\\");
        }
    }
}
