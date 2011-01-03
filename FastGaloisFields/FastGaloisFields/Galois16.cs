using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastGaloisFields
{
    public class Galois16
    {
        private static GaloisTables.GaloisTable16 _table = new GaloisTables.GaloisTable16();
        private FastGaloisFields.GaloisTables.GaloisTable16 table = Galois16._table;
        private ushort value = 0;

        public ushort Value
        {
            get { return value; }
        }

        public uint Bits
        {
            get { return table.Bits; }
        }

        public uint Count
        {
            get { return table.Count; }
        }

        public uint Limit
        {
            get { return table.Limit; }
        }

        public ushort Log() { return table.log[value]; }
        public ushort ALog() { return table.antilog[value]; }

        public Galois16()
        {
        }

        public Galois16(ushort i)
        {
            value = i;
        }

        public static implicit operator Galois16(Int32 i)
        {
            if (i < 0 || i > UInt16.MaxValue)
            {
                throw new ArgumentException();
            }

            return new Galois16((UInt16)i);
        }

        public static implicit operator Galois16(UInt16 i)
        {
            return new Galois16(i);
        }

        public override bool Equals(object o)
        {
            return this.value == ((Galois16)o).Value;
        }

        // TODO: To rewrite
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static int operator +(Galois16 a, Galois16 b)
        {
            return a.Value ^ b.Value;
        }

        public static int operator -(Galois16 a, Galois16 b)
        {
            return a.Value ^ b.Value;
        }

        public static int operator *(Galois16 a, Galois16 b)
        {
            if (a.Value == 0 || b.value == 0)
                return 0;

            int sum = a.table.log[a.Value] + a.table.log[b.Value];

            if (sum >= a.Limit)
            {
                return a.table.antilog[sum - a.Limit];
            }
            else
            {
                return a.table.antilog[sum];
            }
        }

        public static int operator /(Galois16 a, Galois16 b)
        {
            if (a.Value == 0) return 0;

            if (b.Value == 0)
                return 0; // Division by 0!

            int sum = a.table.log[a.Value] - a.table.log[b.Value];
            if (sum < 0)
            {
                return a.table.antilog[sum + a.Limit];
            }
            else
            {
                return a.table.antilog[sum];
            }
        }

        public static int Pow(Galois16 a, Galois16 b)
        {
            if (b.Value == 0)
                return 1;

            if (a.Value == 0)
                return 0;

            int sum = a.table.log[a.Value] * b.Value;

            sum = (int)((sum >> (int)a.Bits) + (sum & a.Limit));
            if (sum >= a.Limit) 
            {
                return a.table.antilog[sum-a.Limit];
            }
            else
            {
                return a.table.antilog[sum];
            }  
        }

        public static int operator ^(Galois16 a, Galois16 b)
        {
            if (b.Value == 0)
                return 1;

            if (a.Value == 0)
                return 0;

            uint sum = (uint)(a.table.log[a.Value] * b);

            sum = (uint)((sum >> (int)a.Bits) + (sum & a.Limit));

            if (sum >= a.Limit)
            {
                return a.table.antilog[sum - a.Limit];
            }
            else
            {
                return a.table.antilog[sum];
            }
        }
    }
}
