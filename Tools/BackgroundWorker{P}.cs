namespace Azi.Tools
{
    using System;
    using System.Threading.Tasks;

    public class BackgroundWorker<P> : IBackgroundWorker<P>
    {
        public BackgroundWorker(Action<P> action)
        {
            Action = action;
        }

        public Action<P> Action { get; set; }

        public virtual Task Run(P param)
        {
            return Task.Run(() => Action(param));
        }
    }
}