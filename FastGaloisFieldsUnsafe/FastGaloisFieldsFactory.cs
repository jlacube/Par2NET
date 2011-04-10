
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastGaloisFieldsUnsafe
{
    public class FastGaloisFieldsFactory
    {
        public static IFastGaloisFieldsProcessor GetProcessor()
        {
            return FastGaloisFieldsNativeProcessor.GetInstance;
            //return FastGaloisFieldsUnsafeProcessor.GetInstance;
        }
    }
}
