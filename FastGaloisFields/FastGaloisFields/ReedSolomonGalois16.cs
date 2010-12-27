using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastGaloisFields.GaloisTables;

namespace FastGaloisFields
{
    public class ReedSolomonGalois16 : ReedSolomon
    {
        private uint inputcount = 0;        // Total number of input blocks

        private uint datapresent = 0;       // Number of input blocks that are present 
        private uint datamissing = 0;       // Number of input blocks that are missing
        private uint[] datapresentindex = null;  // The index numbers of the data blocks that are present
        private uint[] datamissingindex = null;  // The index numbers of the data blocks that are missing

        //private Galois16[] database = null;        // The "base" value to use for each input block
        private ushort[] database = null;        // The "base" value to use for each input block

        private uint outputcount = 0;       // Total number of output blocks

        private uint parpresent = 0;        // Number of output blocks that are present
        private uint parmissing = 0;        // Number of output blocks that are missing
        private uint parpresentindex = 0;   // The index numbers of the output blocks that are present
        private uint parmissingindex = 0;   // The index numbers of the output blocks that are missing

        List<RSOutputRow> outputrows = new List<RSOutputRow>();   // Details of the output blocks

        Galois16[] leftmatrix = null;       // The main matrix

        GaloisLongMultiplyTable16 glmt;

        // When the matrices are initialised: values of the form base ^ exponent are
        // stored (where the base values are obtained from database[] and the exponent
        // values are obtained from outputrows[]).

        public ReedSolomonGalois16()
        {
            glmt = new GaloisLongMultiplyTable16();
        }

        public bool Process(uint size, uint inputindex, byte[] inputbuffer, uint outputindex, byte[] outputbuffer)
        {
            // Optimization: it occurs frequently the function exits early on, so inline the start.
            // This resulted in a speed gain of approx. 8% in repairing.

            // Look up the appropriate element in the RS matrix
            Galois16 factor = leftmatrix[outputindex * (datapresent + datamissing) + inputindex];

            // Do nothing if the factor happens to be 0
            if (factor.Value == 0)
                return true;

            return InternalProcess(factor, size, inputbuffer, outputbuffer);
        }

        private bool InternalProcess(Galois16 factor, uint size, byte[] inputbuffer, byte[] outputbuffer)
        {
            //TODO : Rewrite with TPL

            //if (true)
            //{

            //}
            //else
            //{
                // Treat the buffers as arrays of 16-bit Galois values.
                // Awfully long to execute

            //// Create Galois16 inputbuffer
            //Galois16[] inputGalois16 = new Galois16[inputbuffer.Length / 2];

            //// Create Galois16 outputbuffer
            //Galois16[] outputGalois16 = new Galois16[inputGalois16.Length];

            //// Fill Galois16 input buffer with inpuntbuffer values
            //for (int i = 0; i < inputGalois16.Length; ++i)
            //{
            //    inputGalois16[i] = (Galois16)(inputbuffer[2*i+1] << 8 | inputbuffer[2*i]);
            //    outputGalois16[i] = (Galois16)(outputbuffer[2 * i + 1] << 8 | outputbuffer[2 * i]);
            //}

            //// Process the data
            //for (uint i = 0; i < outputGalois16.Length; ++i)
            //{
            //    outputGalois16[i] += inputGalois16[i] * factor.Value;
            //}
            ////}

            //// Copy back data from Galois16 output buffer to outputbuffer (byte)
            //for (int i = 0; i < outputGalois16.Length; ++i)
            //{
            //    outputbuffer[2*i+1] = (byte)(outputGalois16[i].Value >> 8 & 0xFF);
            //    outputbuffer[2*i] = (byte)(outputGalois16[i].Value & 0xFF);
            //}

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

        public bool SetOutput(bool present, ushort exponent)
        {
            // Store the exponent and whether or not the recovery block is present or missing
            outputrows.Add(new RSOutputRow(present, exponent));

            outputcount++;

            // Update the counts.
            if (present)
            {
                parpresent++;
            }
            else
            {
                parmissing++;
            }

            return true;
        }

        internal bool SetOutput(bool present, ushort lowexponent, ushort highexponent)
        {
            for (uint exponent = lowexponent; exponent <= highexponent; exponent++)
            {
                if (!SetOutput(present, (ushort)exponent))
                    return false;
            }

            return true;
        }

        // Set which of the source files are present and which are missing
        // and compute the base values to use for the vandermonde matrix.
        public bool SetInput(bool[] present)
        {
            inputcount = (uint)present.Length;

            datapresentindex = new uint[inputcount];
            datamissingindex = new uint[inputcount];
            database = new ushort[inputcount];

            uint logbase = 0;

            Galois16 G = new Galois16();

            for (uint index = 0; index < inputcount; index++)
            {
                // Record the index of the file in the datapresentindex array 
                // or the datamissingindex array
                if (present[index])
                {
                    datapresentindex[datapresent++] = index;
                }
                else
                {
                    datamissingindex[datamissing++] = index;
                }

                // Determine the next useable base value.
                // Its log must must be relatively prime to 65535
                while (gcd(G.Limit, logbase) != 1)
                {
                    logbase++;
                }
                if (logbase >= G.Limit)
                {
                    //cerr << "Too many input blocks for Reed Solomon matrix." << endl;
                    return false;
                }


                ushort ibase = new Galois16((ushort)logbase++).ALog();

                database[index] = ibase;
            }

            return true;
        }

        // Record that the specified number of source files are all present
        // and compute the base values to use for the vandermonde matrix.
        internal bool SetInput(uint count)
        {
            inputcount = count;

            datapresentindex = new uint[inputcount];
            datamissingindex = new uint[inputcount];
            database = new ushort[inputcount];

            uint logbase = 0;

            Galois16 G = new Galois16();

            for (uint index = 0; index < count; index++)
            {
                // Record that the file is present
                datapresentindex[datapresent++] = index;

                // Determine the next useable base value.
                // Its log must must be relatively prime to 65535
                while (gcd(G.Limit, logbase) != 1)
                {
                    logbase++;
                }
                if (logbase >= G.Limit)
                {
                    //cerr << "Too many input blocks for Reed Solomon matrix." << endl;
                    return false;
                }

                ushort ibase = new Galois16((ushort)logbase++).ALog();

                database[index] = ibase;
            }

            return true;
        }

        // Construct the Vandermonde matrix and solve it if necessary
        public bool Compute()
        {
            uint outcount = datamissing + parmissing;
            uint incount = datapresent + datamissing;

            if (datamissing > parpresent)
            {
                //cerr << "Not enough recovery blocks." << endl;
                return false;
            }
            else if (outcount == 0)
            {
                //cerr << "No output blocks." << endl;
                return false;
            }

            //if (noiselevel > CommandLine::nlQuiet)
            //  cout << "Computing Reed Solomon matrix." << endl;

            /*  Layout of RS Matrix:

                                                 parpresent
                               datapresent       datamissing         datamissing       parmissing
                         /                     |             \ /                     |           \
             parpresent  |           (ppi[row])|             | |           (ppi[row])|           |
             datamissing |          ^          |      I      | |          ^          |     0     |
                         |(dpi[col])           |             | |(dmi[col])           |           |
                         +---------------------+-------------+ +---------------------+-----------+
                         |           (pmi[row])|             | |           (pmi[row])|           |
             parmissing  |          ^          |      0      | |          ^          |     I     |
                         |(dpi[col])           |             | |(dmi[col])           |           |
                         \                     |             / \                     |           /
            */

            // Allocate the left hand matrix

            leftmatrix = new Galois16[outcount * incount];

            // Allocate the right hand matrix only if we are recovering

            Galois16[] rightmatrix = null;
            if (datamissing > 0)
            {
                rightmatrix = new Galois16[outcount * outcount];
            }

            // Fill in the two matrices:

            // One row for each present recovery block that will be used for a missing data block
            for (uint row = 0; row < outputrows.Count; row++)
            {
                RSOutputRow outputrow = outputrows[(int)row];

                // Define MPDL to skip reporting and speed things up
                //#ifndef MPDL
                //    if (noiselevel > CommandLine::nlQuiet)
                //    {
                //      int progress = row * 1000 / (datamissing+parmissing);
                //      cout << "Constructing: " << progress/10 << '.' << progress%10 << "%\r" << flush;
                //    }
                //#endif

                // Get the exponent of the next present recovery block
                while (!outputrow.present)
                    row++;

                ushort exponent = outputrow.exponent;

                // One column for each present data block
                for (uint col = 0; col < datapresent; col++)
                {
                    leftmatrix[row * incount + col] = Galois16.Pow(new Galois16(database[datapresentindex[col]]), new Galois16(exponent));
                }

                // One column for each each present recovery block that will be used for a missing data block
                for (uint col = 0; col < datamissing; col++)
                {
                    leftmatrix[row * incount + col + datapresent] = (row == col) ? 1 : 0;
                }

                if (datamissing > 0)
                {
                    // One column for each missing data block
                    for (uint col = 0; col < datamissing; col++)
                    {
                        rightmatrix[row * outcount + col] = Galois16.Pow(new Galois16(database[datamissingindex[col]]), new Galois16(exponent));
                    }
                    // One column for each missing recovery block
                    for (uint col = 0; col < parmissing; col++)
                    {
                        rightmatrix[row * outcount + col + datamissing] = 0;
                    }
                }

                row++;
            }

            // One row for each recovery block being computed
            for (uint row = 0; row < parmissing; row++)
            {
                RSOutputRow outputrow = outputrows[(int)row];

                // Define MPDL to skip reporting and speed things up
                //#ifndef MPDL
                //if (noiselevel > CommandLine::nlQuiet)
                //{
                //  int progress = (row+datamissing) * 1000 / (datamissing+parmissing);
                //  cout << "Constructing: " << progress/10 << '.' << progress%10 << "%\r" << flush;
                //}
                //#endif

                // Get the exponent of the next missing recovery block
                while (outputrow.present)
                {
                    outputrow = outputrows[(int)++row];
                }
                ushort exponent = outputrow.exponent;

                // One column for each present data block
                for (uint col = 0; col < datapresent; col++)
                {
                    leftmatrix[(row + datamissing) * incount + col] = Galois16.Pow(new Galois16(database[datapresentindex[col]]), new Galois16(exponent));
                }
                // One column for each each present recovery block that will be used for a missing data block
                for (uint col = 0; col < datamissing; col++)
                {
                    leftmatrix[(row + datamissing) * incount + col + datapresent] = 0;
                }

                if (datamissing > 0)
                {
                    // One column for each missing data block
                    for (uint col = 0; col < datamissing; col++)
                    {
                        rightmatrix[(row + datamissing) * outcount + col] = Galois16.Pow(new Galois16(database[datamissingindex[col]]), new Galois16(exponent));
                    }
                    // One column for each missing recovery block
                    for (uint col = 0; col < parmissing; col++)
                    {
                        rightmatrix[(row + datamissing) * outcount + col + datamissing] = (row == col) ? 1 : 0;
                    }
                }

                row++;
            }

            //if (noiselevel > CommandLine::nlQuiet)
            //  cout << "Constructing: done." << endl;

            // Solve the matrices only if recovering data
            if (datamissing > 0)
            {
                // Perform Gaussian Elimination and then delete the right matrix (which 
                // will no longer be required).
                //bool success = ReedSolomon.GaussElim(noiselevel, outcount, incount, leftmatrix, rightmatrix, datamissing);
                bool success = ReedSolomon.GaussElim(outcount, incount, leftmatrix, rightmatrix, datamissing);
                //return success;
            }

            return true;
        }
    }
}
