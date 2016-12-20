using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;

namespace Cloud.DokanNet.Console
{
    class Program
    {
        private const string AppName = "ACDDokanNet";

        static int Main(string[] args)
        {
            using (var pipe = new NamedPipeClientStream(".", "pipe" + AppName, PipeDirection.InOut))
            {
                try
                {
                    pipe.Connect(1000);
                }
                catch (TimeoutException)
                {
                    System.Console.WriteLine("Application is not started.");
                    return 1;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Console.WriteLine("Main application has higher priviledges level than console application.");
                    return 1;
                }

                try
                {
                    var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, true);
                    var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
                    writer.WriteLine(string.Join(" ", args.Select(s => s.Contains(' ') ? "\"" + s + "\"" : s)));
                    writer.Flush();
                    string lastString = null;
                    string response;
                    while ((response = reader.ReadLine()) != null)
                    {
                        lastString = response;

                        if (response != "Done")
                        {
                            System.Console.WriteLine(response);
                        }
                    }
                    return (lastString == "Done") ? 0 : 1;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine(ex);
                    return 1;
                }
            }
        }
    }
}
