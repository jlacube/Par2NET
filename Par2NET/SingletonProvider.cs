using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET
{
    public sealed class SingletonProvider<T> where T : new()
    {
        SingletonProvider()
        {
        }

        public static T Instance
        {
            get
            {
                return SingletonCreator.instance;
            }
        }

        class SingletonCreator
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static SingletonCreator()
            {
            }

            internal static readonly T instance = new T();
        }
    }
}
