using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using GaloisFields;

namespace GaloisFieldsConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            ushort[,] matrix1 = FEC_Matrix.CreateVandermondeMatrix(5, 3);

            ushort[,] matrix2 = FEC_MatrixPAR2.CreateVandermondeMatrix(5, 3);

            ushort[,] matrix3 = FEC_MatrixPAR2.CreateVandermondeMatrix(10, 5);

        }
    }
}
