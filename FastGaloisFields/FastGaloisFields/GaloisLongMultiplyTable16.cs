using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastGaloisFields.GaloisTables
{
    public class GaloisLongMultiplyTable16
    {
        public uint Bytes { get; set; }
        public uint Count { get; set; }

        public Galois16[] tables;

        public GaloisLongMultiplyTable16()
        {
            Bytes = ((16 + 7) >> 3);
            Count = (( Bytes * (Bytes + 1)) / 2);
            
            tables = new Galois16[Count * 256 * 256];

            int index = 0;
            for (int i = 0; i < Bytes; i++)
            {
                for (int j = i; j < Bytes; j++)
                {
                    for (int ii = 0; ii < 256; ii++)
                    {
                        for (int jj = 0; jj < 256; jj++)
                        {
                            tables[index] = new Galois16((ushort)(ii << (8 * i))) * new Galois16((ushort)(jj << (8 * j)));
                        }
                    }
                }
            }
        }
    }
}
