using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastGaloisFields.GaloisTables
{
    public class GaloisTable16
    {
        public uint Bits { get; set; }
        public uint Count { get; set; }
        public uint Limit { get; set; }

        public ushort[] log = null;
        public ushort[] antilog = null;

        public GaloisTable16()
            : this(16, 0x1100B)
        {
        }

        public GaloisTable16(uint bits, uint generator)
        {
            Bits = bits;
            Count = (uint)(1 << (int)Bits);
            Limit = Count - 1;

            log = new ushort[Count];
            antilog = new ushort[Count];

            uint b = 1;

            for (uint l = 0; l < Limit; l++)
            {
                log[b] = (ushort)l;
                antilog[l] = (ushort)b;

                b <<= 1;
                if ((b & Count) != 0)
                    b ^= generator;
            }

            log[0] = (ushort)Limit;

            antilog[Limit] = 0;
        }
    }
}
