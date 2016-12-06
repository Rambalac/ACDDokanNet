namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Tools;

    public class CommandLineProcessor
    {
        private static readonly Regex SplitRegex = new Regex(@"""(?<match>[^""]*)""|'(?<match>[^']*)'|(?<match>[^\s]+)");
        private readonly ViewModel model;

        public CommandLineProcessor(ViewModel model)
        {
            this.model = model;
        }

        public async Task Process(Stream pipe)
        {
            var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, true);
            var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            string line = null;
            try
            {
                line = await reader.ReadLineAsync();
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

        private async Task ProcessLine(List<string> parts, TextWriter writer)
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
                    case "umount":
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

        private async Task ShowCommands(TextWriter writer)
        {
            await writer.WriteLineAsync("Empty or unknown command");
            await writer.WriteLineAsync("Available commands:");
            await writer.WriteLineAsync("    shutdown");
            await writer.WriteLineAsync("    list");
            await writer.WriteLineAsync("    mount <drive letter or cloud name in quotas>");
            await writer.WriteLineAsync("    unmount <drive letter or cloud name in quotas>");
            await writer.WriteLineAsync("    version");
        }

        private async Task CommandMount(TextWriter writer, string v)
        {
            var cloud = model.Clouds.SingleOrDefault(c => c.CloudInfo.Name != null && v == c.CloudInfo.Name.ToLowerInvariant());
            if (cloud == null)
            {
                cloud = model.Clouds.SingleOrDefault(c => v == c.CloudInfo.DriveLetter.ToString().ToLowerInvariant());
            }

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
            var cloud = model.Clouds.SingleOrDefault(c => c.CloudInfo.Name != null && v == c.CloudInfo.Name.ToLowerInvariant());
            if (cloud == null)
            {
                cloud = model.Clouds.SingleOrDefault(c => c.MountLetter != null && v == c.MountLetter.ToString().ToLowerInvariant());
            }

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
    }
}
