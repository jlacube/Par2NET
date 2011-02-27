using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace FastGaloisFields
{
    public class FastGaloisFieldsSafeProcessor
    {
        public bool InternalProcess(Galois16 factor, uint size, byte[] inputbuffer, byte[] outputbuffer)
        {
            try
            {
                GCHandle inputGCH = GCHandle.Alloc(inputbuffer);
                IntPtr inputPtr = GCHandle.ToIntPtr(inputGCH);

                GCHandle outputGCH = GCHandle.Alloc(outputbuffer);
                IntPtr outputPtr = GCHandle.ToIntPtr(outputGCH);

                UInt16 iInput = 0;
                UInt16 iOutput = 0;

                for (int i = 0; i < inputbuffer.Length / 2; ++i)
                {

                    Marshal.PtrToStructure(inputPtr, iInput);
                    Marshal.PtrToStructure(outputPtr, iOutput);

                    Galois16 gInput = new Galois16(iInput);
                    Galois16 gOutput = new Galois16(iOutput);

                    gOutput += gInput * factor.Value;

                    Marshal.StructureToPtr(gOutput.Value, outputPtr, false);

                    IntPtr.Add(inputPtr, sizeof(UInt16));
                    IntPtr.Add(outputPtr, sizeof(UInt16));
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
