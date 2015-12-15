using System;
using System.Threading.Tasks;

namespace AmazonCloudDriveApi
{
    public static class Retry
    {
        public static bool Do(int times, Func<bool> act)
        {
            while (times > 0)
            {
                if (act()) return true;
                times--;
            }
            return false;
        }

        public static bool Do(int times, Func<int, bool> act)
        {
            for (int time = 0; time < times - 1; time++)
            {
                if (act(time)) return true;
            }
            return false;
        }

        static bool DefaultExceptionProcessor(Exception ex) => true;

        public static async Task<bool> Do(int times, Func<int, TimeSpan> retryDelay, Func<Task<bool>> act)
        {
            return await Do(times, retryDelay, act, DefaultExceptionProcessor);
        }

        public static async Task<bool> Do(int times, Func<int, TimeSpan> retryDelay, Func<Task<bool>> act, Func<Exception, bool> exceptionPocessor)
        {
            for (int time = 0; time < times - 1; time++)
            {
                try
                {
                    if (await act()) return true;
                }
                catch (Exception ex)
                {
                    if (exceptionPocessor(ex)) return false;
                }
                await Task.Delay(retryDelay(time));
            }
            return await act();
        }
    }
}
