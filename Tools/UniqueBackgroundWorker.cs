namespace Azi.Tools
{
    using System;
    using System.Threading.Tasks;

    public class UniqueBackgroundWorker<P> : BackgroundWorker<P>
    {
        private object lockObject = new object();

        public UniqueBackgroundWorker(Action<P> action)
            : base(action)
        {
        }

        public Task Task { get; private set; }

        public override Task Run(P param)
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

    public class UniqueBackgroundWorker : BackgroundWorker
    {
        private object lockObject = new object();

        public UniqueBackgroundWorker(Action action)
            : base(action)
        {
        }

        public Task Task { get; private set; }

        public override Task Run()
        {
            lock (lockObject)
            {
                if (Task != null && !Task.IsCompleted)
                {
                    return Task;
                }

                Task = base.Run();
                return Task;
            }
        }
    }
}