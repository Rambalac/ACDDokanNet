using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tools
{
    public class FuncEqualityComparer<T> : IEqualityComparer<T>
    {
        readonly Func<T, T, bool> func;
        readonly Func<T, int> hashFunc;
        public FuncEqualityComparer(Func<T, T, bool> func, Func<T, int> hashFunc)
        {
            this.func = func;
            this.hashFunc = hashFunc;
        }

        public FuncEqualityComparer(Func<T, object> extract)
        {
            this.func = (a, b) => extract(a).Equals(extract(b));
            this.hashFunc = (a) => extract(a).GetHashCode();
        }

        public bool Equals(T x, T y) => func(x, y);

        public int GetHashCode(T obj) => hashFunc(obj);
    }
}
