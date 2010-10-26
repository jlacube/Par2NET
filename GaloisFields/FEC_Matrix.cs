// TODO: Add validation fonction
// TODO: Clean-up Gauss-Jordan elimination
using System;

namespace GaloisFields
{
    /// <summary>
    /// Overview:
    /// -------------------------------------------------------------------------------------------
    /// FEC_Matrix is a class that implement the basic matrix calulation using
    /// Galois Fields on 16 bits (GF16).
    /// 
    /// Public Methods:
    /// -------------------------------------------------------------------------------------------
    /// Invert - Invert the matrix by using Gauss-Jordan using GF16 operations
    /// CreateVandermondeMatrix - Create a Vandermonde matrix over GF16
    /// </summary>

    public class FEC_MatrixPAR2 : FEC_Matrix
    {
        private static int ExpToCorrectedExp(int exponent, int last_good_exponent)
        {
            int exp = last_good_exponent;
            bool found = false;

            do
            {
                // We want to discard exponent for which 2 ^ exponent hasn't order 65535
                if (exp % 3 != 0 && exp % 5 != 0 && exp % 17 != 0 && exp % 257 != 0)
                    found = true;

                ++exp;

            } while (!found);

            return exp;
        }

        /// <summary>
        /// Create a Vandermonde matrix of size row x column over GF16
        /// </summary>
        /// <remarks>
        /// The Vandermonde matrix is typically used to create the encoding matrix where:
        /// - The number of Columns of the matrix correspond to number of checksum 
        /// packets.
        /// - The number of Rows of the matrix correspond to number of data packets. 
        /// </remarks>
        /// <param name="columns">The number of columns of the Vandermonde matrix</param>
        /// <param name="rows">The number of rows of the Vandermode matrix</param>
        /// <returns></returns>
        new public static UInt16[,] CreateVandermondeMatrix(int columns, int rows)
        {
            // TODO: Add input validation

            // maxChecksumPackets will be the max number of Columns of the encoding static matrix
            // maxDataPackets will be the max number of Rows of the encoding static matrix

            UInt16[,] vandermondeMtx = new UInt16[columns, rows];

            // Creation of the Vandermonde Matrix over GF16 with the following
            // (2^column)^row

            // As an example, a 5 x 3 Vandermonde Matrix over GF16 (5 data packets, 3 checksum packets)
            // would give the following:
            //
            // 1^0 2^0 4^0
            // 1^1 2^1 4^1
            // 1^2 2^2 4^2
            // 1^3 2^3 4^3
            // 1^4 2^4 4^4
            //
            // Which gives:
            //
            // 1   1   1
            // 1   2   4
            // 1   4   16
            // 1   8   64
            // 1   16  256

            int last_good_exponent = 0;

            for (int col = 0; col < columns; col++)
            {
                // multFactor is the number to multiply to get the value in the next row
                // for a given column of the Vandermonde matrix
                last_good_exponent = ExpToCorrectedExp(col, last_good_exponent);

                UInt16 multFactor = GF16.Power(2, (uint)last_good_exponent);
                for (int row = 0; row < rows; row++)
                {
                    if (row == 0)
                    {
                        // Special case the first row (power of zero)
                        vandermondeMtx[col, row] = 1;
                    }
                    else
                    {
                        // Each element of the Vandermonde matrix is calculated as (2^column)^row over GF16

                        // This algorithm uses the previous row to compute the next one to improve
                        // the performances (instead of recalculating (2^column)^row)
                        vandermondeMtx[col, row] = GF16.Multiply(vandermondeMtx[col, row - 1], multFactor);
                    }
                }
            }
            return vandermondeMtx;
        }
    }
}
