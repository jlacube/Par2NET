using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastGaloisFields
{
    public class ReedSolomon
    {
        protected static uint gcd(uint a, uint b)
        {
            if (a != 0 && b != 0)
            {
                while (a != 0 && b != 0)
                {
                    if (a > b)
                    {
                        a = a % b;
                    }
                    else
                    {
                        b = b % a;
                    }
                }

                return a + b;
            }
            else
            {
                return 0;
            }
        }
    }
}
