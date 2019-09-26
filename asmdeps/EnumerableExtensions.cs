using System.Collections.Generic;
using System.Linq;

namespace asmdeps
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Reversed<T>(this IEnumerable<T> input)
        {
            return input
                .Select((o, i) => new { o, i })
                .OrderByDescending(o => o.i)
                .Select(o => o.o);
        }
    }
}