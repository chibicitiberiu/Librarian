using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Librarian.Util
{
    public static class QueueExtensions
    {
        public static void EnqueueRange<T>(this Queue<T> queue, IEnumerable<T> elements)
        {
            foreach (var element in elements)
                queue.Enqueue(element);
        }
    }
}
