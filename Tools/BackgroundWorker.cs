namespace Azi.Tools
{
    using System;
    using System.Threading.Tasks;

    public class BackgroundWorker : IBackgroundWorker
    {
        public BackgroundWorker(Action action)
        {
            Action = action;
        }

        public Action Action { get; set; }

        public virtual Task Run()
        {
            return Task.Run(Action);
        }
    }
}