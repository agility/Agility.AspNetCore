using System;
using System.Collections.Generic;
using System.Linq;

namespace Agility.Web.Extensions
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> Randomize<T>(this IEnumerable<T> source)
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            return source.OrderBy<T, int>((item => rnd.Next()));
        }
    }
}