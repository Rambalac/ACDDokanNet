namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Tools;

    public class CommandLineProcessor
    {
        private static readonly Regex SplitRegex = new Regex(@"""(?<match>[^""]*)""|'(?<match>[^']*)'|(?<match>[^\s]+)");
        private readonly ViewModel model;
        private readonly string id;
        private bool isShuttingDown;

        public CommandLineProcessor(ViewModel model, string id)
        {
            this.model = model;
            this.id = id;
        }

        public void Stop()
        {
            isShuttingDown = true;
        }

        public void Start()
        {
            Task.Factory.StartNew(MainLoop, TaskCreationOptions.LongRunning);
        }

        private static async Task ShowCommands(TextWriter writer)
        {
            await writer.WriteLineAsync("Empty or unknown command");
            await writer.WriteLineAsync("Available commands:");
            await writer.WriteLineAsync("    shutdown");
            await writer.WriteLineAsync("    list");
            await writer.WriteLineAsync("    mount <drive letter or cloud name in quotas>");
            await writer.WriteLineAsync("    unmount <drive letter or cloud name in quotas>");
            await writer.WriteLineAsync("    version");
        }

        private async Task Process(Stream pipe)
        {
            var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, true);
            var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            try
            {
                var line = await reader.ReadLineAsync();
                var parts = SplitRegex.Matches(line).Cast<Match>().Select(m => m.Groups["match"].Value.ToLowerInvariant()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                await ProcessLine(parts, writer);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                try
                {
                    await writer.WriteLineAsync(ex.Message);
                }
                catch (Exception ex2)
                {
                    Log.Error(ex2);
                }
            }
            finally
            {
                await writer.FlushAsync();
            }
        }

        private async Task ProcessLine(IReadOnlyList<string> parts, TextWriter writer)
        {
            if (parts.Count > 0)
            {
                switch (parts[0])
                {
                    case "shutdown":
                        await writer.WriteLineAsync("Shutting down...");
                        await writer.WriteLineAsync("Done");
                        model.Shutdown();
                        break;
                    case "mount":
                        await CommandMount(writer, parts[1]);
                        break;
                    case "unmount":
                        await CommandUnmount(writer, parts[1]);
                        break;
                    case "list":
                        await CommandList(writer);
                        break;
                    case "version":
                        await writer.WriteLineAsync(model.Version);
                        await writer.WriteLineAsync("Done");
                        break;
                    default:
                        await ShowCommands(writer);
                        break;
                }
            }
            else
            {
                await ShowCommands(writer);
            }
        }

        private async Task CommandList(TextWriter writer)
        {
            foreach (var cloud in model.Clouds)
            {
                var mounted = cloud.IsMounted ? "mounted" : "unmounted";
                await writer.WriteLineAsync($"{cloud.CloudInfo.DriveLetter},\"{cloud.CloudInfo.Name}\",{mounted}");
            }
        }

        private async Task CommandMount(TextWriter writer, string v)
        {
            var cloud = model.Clouds.SingleOrDefault(c => c.CloudInfo.Name != null && v == c.CloudInfo.Name.ToLowerInvariant()) ??
                        model.Clouds.SingleOrDefault(c => v == c.CloudInfo.DriveLetter.ToString().ToLowerInvariant());

            if (cloud == null)
            {
                await writer.WriteLineAsync($"Registred clound with such name or predefined drive letter is not found: {v}");
                await writer.WriteLineAsync("Failed");
                return;
            }

            if (cloud.IsMounted)
            {
                await writer.WriteLineAsync($"Cloud is already mounted: {v}");
                await writer.WriteLineAsync("Failed");
                return;
            }

            if (!cloud.CanMount)
            {
                await writer.WriteLineAsync($"Cloud cannot be mounted now, try later: {v}");
                await writer.WriteLineAsync("Failed");
                return;
            }

            await cloud.MountAsync(false);

            await writer.WriteLineAsync("Done");
        }

        private async Task CommandUnmount(TextWriter writer, string v)
        {
            var cloud = model.Clouds.SingleOrDefault(c => c.CloudInfo.Name != null && v == c.CloudInfo.Name.ToLowerInvariant()) ??
                        model.Clouds.SingleOrDefault(c => c.MountLetter != null && v == c.MountLetter.ToString().ToLowerInvariant());

            if (cloud == null)
            {
                await writer.WriteLineAsync($"Registred clound with such name or mount drive letter is not found: {v}");
                await writer.WriteLineAsync("Failed");
                return;
            }

            if (!cloud.IsMounted)
            {
                await writer.WriteLineAsync($"Cloud is not mounted: {v}");
                await writer.WriteLineAsync("Failed");
                return;
            }

            if (!cloud.CanUnmount)
            {
                await writer.WriteLineAsync($"Cloud cannot be mounted now, try later: {v}");
                await writer.WriteLineAsync("Failed");
                return;
            }

            await cloud.UnmountAsync();

            await writer.WriteLineAsync("Done");
        }

        private async Task MainLoop()
        {
            do
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream("pipe" + id, PipeDirection.InOut, 2, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        await pipe.WaitForConnectionAsync();
                        await Process(pipe);
                        pipe.WaitForPipeDrain();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
            while (!isShuttingDown);
        }
    }
}
