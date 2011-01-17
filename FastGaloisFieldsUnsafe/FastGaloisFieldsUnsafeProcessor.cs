using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FastGaloisFields;

namespace FastGaloisFieldsUnsafe
{
    public class FastGaloisFieldsUnsafeProcessor
    {
        public bool InternalProcess(Galois16 factor, uint size, byte[] inputbuffer, byte[] outputbuffer)
        {
            try
            {

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
            catch(Exception)
            {
#endif
                return false;
            }

        }
    }
}
