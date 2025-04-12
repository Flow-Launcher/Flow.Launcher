#nullable enable

using System;

namespace Flow.Launcher.Infrastructure
{
    public static class Helper
    {
        /// <summary>
        /// http://www.yinwang.org/blog-cn/2015/11/21/programming-philosophy
        /// </summary>
        public static T NonNull<T>(this T? obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
            else
            {
                return obj;
            }
        }

        public static void RequireNonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
        }
    }
}
