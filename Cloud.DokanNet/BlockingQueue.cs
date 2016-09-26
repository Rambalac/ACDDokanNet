namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class BlockingQueue<T>
    {
        private Queue<T> queue = new Queue<T>();

        private SemaphoreSlim itemsSem = new SemaphoreSlim(0, int.MaxValue);

        public void Enqueue(T item)
        {
            lock (queue)
            {
                queue.Enqueue(item);
                itemsSem.Release();
            }
        }

        public async Task<T> Dequeue(CancellationToken token)
        {
            await itemsSem.WaitAsync(token).ConfigureAwait(false);
            lock (queue)
            {
                return queue.Dequeue();
            }
        }

        public bool TryDequeue(out T item)
        {
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    item = queue.Dequeue();
                    return true;
                }
            }

            item = default(T);
            return false;
        }

        public async Task<T> BlockingPeek(CancellationToken token)
        {
            await itemsSem.WaitAsync(token).ConfigureAwait(false);
            lock (queue)
            {
                itemsSem.Release();
                return queue.Peek();
            }
        }

        public T Peek()
        {
            lock (queue)
            {
                return queue.Peek();
            }
        }
    }
}
