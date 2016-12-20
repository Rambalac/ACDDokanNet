namespace Azi.Tools
{
    using System;
    using System.Threading.Tasks;

    public class BackgroundWorker<TParam> : IBackgroundWorker<TParam>
    {
        public BackgroundWorker(Action<TParam> action)
        {
            Action = action;
        }

        public Action<TParam> Action { get; set; }

        public virtual Task Run(TParam param)
        {
            return Task.Run(() => Action(param));
        }
    }
}