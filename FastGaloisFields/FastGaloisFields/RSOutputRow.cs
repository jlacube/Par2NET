using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastGaloisFields
{
    public class RSOutputRow
    {
        public bool present = false;
        public ushort exponent = 0;

        public RSOutputRow()
            : this(false, 0)
        {
        }

        public RSOutputRow(bool _present, ushort _exponent)
        {
            present = _present;
            exponent = _exponent;
        }
    }
}
