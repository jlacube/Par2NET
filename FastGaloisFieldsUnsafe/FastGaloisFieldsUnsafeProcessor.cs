using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FastGaloisFields;
using System.Runtime.InteropServices;

namespace FastGaloisFieldsUnsafe
{
    public sealed class FastGaloisFieldsUnsafeProcessor : IFastGaloisFieldsProcessor
    {
        uint bits = 16;
        uint generator = 0x1100B;

        public uint count = 0;
        public uint _limit = 0;

        public ushort[] log = null;
        public ushort[] antilog = null;

        private static FastGaloisFieldsUnsafeProcessor instance = null;

        public static IFastGaloisFieldsProcessor GetInstance
        {
            get
            {
                if (instance == null)
                {
                    instance = new FastGaloisFieldsUnsafeProcessor();
                }

                return instance;
            }
        }

        private FastGaloisFieldsUnsafeProcessor()
        {
            count = (uint)(1 << (int)bits);
            _limit = count - 1;

            log = new ushort[count];
            antilog = new ushort[count];

            uint b = 1;

            for (uint l = 0; l < limit; l++)
            {
                log[b] = (ushort)l;
                antilog[l] = (ushort)b;

                b <<= 1;
                if ((b & count) != 0)
                    b ^= generator;
            }

            log[0] = (ushort)limit;

            antilog[limit] = 0;
        }

        public ushort Multiply(ushort a, ushort b)
        {
            if (a == 0 || b == 0)
                return 0;

            int sum = log[a] + log[b];

            if (sum >= limit)
            {
                return antilog[sum - limit];
            }
            else
            {
                return antilog[sum];
            }
        }

        public ushort Divide(ushort a, ushort b)
        {
            if (a == 0) return 0;

            if (b == 0)
                return 0; // Division by 0!

            int sum = log[a] - log[b];
            if (sum < 0)
            {
                return antilog[sum + limit];
            }
            else
            {
                return antilog[sum];
            }
        }

        public ushort Pow(ushort a, ushort b)
        {
            if (b == 0)
                return 1;

            if (a == 0)
                return 0;

            int sum = log[a] * b;

            sum = (int)((sum >> (int)bits) + (sum & limit));
            if (sum >= limit)
            {
                return antilog[sum - limit];
            }
            else
            {
                return antilog[sum];
            }
        }

        public ushort Add(ushort a, ushort b)
        {
            return (ushort)(a ^ b);
        }

        public ushort Minus(ushort a, ushort b)
        {
            return (ushort)(a ^ b);
        }

        public bool InternalProcess(ushort factor, uint size, byte[] inputbuffer, byte[] outputbuffer, int startIndex, uint length)
        {
            try
            {
                unsafe
                {
                    fixed (byte* pInputBuffer = inputbuffer, pOutputBuffer = outputbuffer)
                    {
                        UInt16* pInput = (UInt16*)pInputBuffer;
                        UInt16* pOutput = (UInt16*)(pOutputBuffer + startIndex);

                        for (int i = 0; i < inputbuffer.Length / 2; ++i)
                        {
                            *pOutput = Add(*pOutput, Multiply(*pInput, factor));

                            ++pInput;
                            ++pOutput;
                        }
                    }
                }

                return true;
            }
#if Debug
            catch (Exception ex)
            {

                Debug.WriteLine(ex);
#else
            catch(Exception)
            {
#endif
                return false;
            }
        }

        public uint limit
        {
            get { return _limit; }
        }

        public ushort GetAntilog(int index)
        {
            return antilog[index];
        }
    }
}
