using amazon_clouddrive_dokan;
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
            var cloudDrive = new CloudDrive("D:\\CloudDriveTestCache");
            try
            {
                cloudDrive.Mount("r:\\", DokanOptions.DebugMode | DokanOptions.StderrOutput | DokanOptions.RemovableDrive);
                Console.WriteLine("Success");
                Dokan.Unmount('r');
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine(File.ReadAllText("d:\\debug.txt"));
        }
    }
}
