using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FastGaloisFields;
using System.Runtime.InteropServices;

namespace FastGaloisFieldsUnsafe
{
    public sealed class FastGaloisFieldsUnsafeProcessor
    {
        static uint bits = 16;
        static uint generator = 0x1100B;

        static uint count = 0;
        static uint limit = 0;

        static ushort[] log = null;
        static ushort[] antilog = null;

        static FastGaloisFieldsUnsafeProcessor()
        {
            count = (uint)(1 << (int)bits);
            limit = count - 1;

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

        static ushort Multiply(ushort a, ushort b)
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

        static ushort Add(ushort a, ushort b)
        {
            return (ushort)(a ^ b);
        }

        public static bool InternalProcess(Galois16 factor, uint size, byte[] inputbuffer, byte[] outputbuffer, int startIndex, uint length)
        {
            try
            {
                unsafe
                {
                    fixed (byte* pInputBuffer = inputbuffer, pOutputBuffer = outputbuffer)
                    {
                        ushort uFactor = factor.Value;
                        UInt16* pInput = (UInt16*)pInputBuffer;
                        UInt16* pOutput = (UInt16*)(pOutputBuffer + startIndex);

                        for (int i = 0; i < inputbuffer.Length / 2; ++i)
                        {
                            *pOutput = Add(*pOutput, Multiply(*pInput, uFactor));

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

        public bool InternalProcess_orig(Galois16 factor, uint size, byte[] inputbuffer, byte[] outputbuffer)
        {
            try
            {
                GCHandle inputGCH = GCHandle.Alloc(inputbuffer);
                IntPtr inputPtr = GCHandle.ToIntPtr(inputGCH);

                unsafe
                {
                    fixed (byte* pInputBuffer = inputbuffer, pOutputBuffer = outputbuffer)
                    {
                        UInt16* pInput = (UInt16*)pInputBuffer;
                        UInt16* pOutput = (UInt16*)pOutputBuffer;

                        for (int i = 0; i < inputbuffer.Length / 2; ++i)
                        {
                            Galois16 gInput = *pInput;
                            Galois16 gOutput = *pOutput;

                            gOutput += gInput * factor.Value;

                            *pOutput = gOutput.Value;

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
            catch (Exception)
            {
#endif
                return false;
            }

        }
    }
}
