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
            var orig = Enumerable.Range(0, 256).Select(n => (byte)n).ToArray();

            var amazon = Authenticate().Result;
            if (amazon == null)
            {
                Console.WriteLine("Can not Authenticate");
                return;
            }


            var provider = new FSProvider(amazon);
            provider.CachePath = "%TEMP%\\ACDDokanNetCache";
            provider.SmallFileSizeLimit = 10;


            //if (provider.GetItem("\\test.txt") != null) provider.DeleteFile("\\test.txt");
            //using (var stream = provider.OpenFile("\\test.txt", System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None, System.IO.FileOptions.None))
            //{
            //    stream.Write(0, orig, 0, orig.Length);
            //    stream.Close();
            //}


            //Console.WriteLine("Written");

            //while (provider.GetItem("\\test.txt").IsFake)
            //{
            //    Console.WriteLine("Fake");
            //    Thread.Sleep(1000);
            //}

            var newbuf = new byte[300];

            long pos = 0;
            using (var stream = provider.OpenFile("\\test.txt", FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.None))
            {
                var buf = new byte[100];
                
                while (pos < newbuf.Length)
                {
                    int red = stream.Read(pos, buf, 0, buf.Length, 10000);
                    if (red == 0) break;
                    Array.Copy(buf, 0, newbuf, pos, red);
                    pos += red;
                }
            }

            if (pos != orig.Length) Console.WriteLine("Wrong length:" + pos);

            for (int i = 0; i < orig.Length; i++)
            {
                if (orig[i] != newbuf[i]) { Console.WriteLine("Wrong pos:" + i); break; }
            }
            Console.WriteLine("Done");

            //var cloudDrive = new VirtualDriveWrapper();
            //cloudDrive.Mount("r:\\");
        }
    }
}
