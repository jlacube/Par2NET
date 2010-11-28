using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastGaloisFields.GaloisTables
{
    public class GaloisTable8
    {
        public byte[] log = null;
        public byte[] antilog = null;

        public GaloisTable8()
            : this(8, 0x11D)
        {
        }

        public GaloisTable8(uint bits, uint generator)
        {
            uint count = (uint)(1 << (int)bits);
            uint limit = count - 1;

            log = new byte[count];
            antilog = new byte[count];

            uint b = 1;

            for (uint l = 0; l < limit; l++)
            {
                log[b] = (byte)l;
                antilog[l] = (byte)b;

                b <<= 1;
                if ((b & count) != 0)
                    b ^= generator;
            }

            log[0] = (byte)limit;

            antilog[limit] = 0;
        }
    }
}
