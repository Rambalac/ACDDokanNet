namespace Azi.Tools
{
    using System;
    using System.IO.Pipes;
    using System.Threading.Tasks;

    public static class PipeExtensions
    {
        public static async Task WaitForConnectionAsync(this NamedPipeServerStream server)
        {
            var completition = new TaskCompletionSource<int>();
            server.BeginWaitForConnection(
                ar =>
                {
                    try
                    {
                        var pipeServer = (NamedPipeServerStream)ar.AsyncState;
                        pipeServer.EndWaitForConnection(ar);
                        completition.SetResult(0);
                    }
                    catch (Exception ex)
                    {
                        completition.SetException(ex);
                    }
                },
                server);
            await completition.Task;
        }
    }
}
