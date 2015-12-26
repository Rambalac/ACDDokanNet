using Azi.ACDDokanNet;
using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Tests;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static async Task<AmazonDrive> Authenticate()
        {
            var amazon = new AmazonDrive(AmazonSecret.clientId, AmazonSecret.clientSecret);
            amazon.OnTokenUpdate = (token, renew, expire) =>
                  {
                      Properties.Settings.Default.AuthToken = token;
                      Properties.Settings.Default.AuthRenewToken = renew;
                      Properties.Settings.Default.AuthTokenExpiration = expire;
                      Properties.Settings.Default.Save();
                  };

            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.AuthRenewToken))
            {
                if (await amazon.Authentication(
                    Properties.Settings.Default.AuthToken,
                    Properties.Settings.Default.AuthRenewToken,
                    Properties.Settings.Default.AuthTokenExpiration)) return amazon;
            }
            if (await amazon.SafeAuthenticationAsync(CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10))) return amazon;
            return null;
        }
        static void Main(string[] args)
        {
            var amazon = Authenticate().Result;
            if (amazon == null)
            {
                Console.WriteLine("Can not Authenticate");
                return;
            }


            var provider = new FSProvider(amazon);
            using (var stream = provider.OpenFile("\\test.txt", System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None, System.IO.FileOptions.None))
            {
                var buf = Encoding.ASCII.GetBytes("asdfghjkl");
                stream.Write(0, buf, 0, buf.Length);
                stream.Close();
            }
            Console.ReadKey();

            //var cloudDrive = new VirtualDriveWrapper();
            //cloudDrive.Mount("r:\\");
        }
    }
}
