using amazon_clouddrive_dokan;
using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Tests;
using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                cloudDrive.Mount("r:\\", DokanOptions.DebugMode | DokanOptions.StderrOutput | DokanOptions.NetworkDrive);
        }
    }
}
