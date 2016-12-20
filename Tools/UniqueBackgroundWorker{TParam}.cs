namespace Azi.Tools
{
    using System;
    using System.Threading.Tasks;

    public class UniqueBackgroundWorker<TParam> : BackgroundWorker<TParam>
    {
        private readonly object lockObject = new object();

        public UniqueBackgroundWorker(Action<TParam> action)
            : base(action)
        {
        }

        public Task Task { get; private set; }

        public override Task Run(TParam param)
        {
            lock (lockObject)
            {
                if (Task != null && !Task.IsCompleted)
                {
                    return Task;
                }

                Task = base.Run(param);
                return Task;
            }
        }
    }
}