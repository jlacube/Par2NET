using System;
namespace FastGaloisFieldsUnsafe
{
    public interface IFastGaloisFieldsProcessor
    {
        ushort Add(ushort a, ushort b);
        ushort Divide(ushort a, ushort b);
        /*IFastGaloisFieldsProcessor GetInstance { get; }*/
        bool InternalProcess(ushort factor, uint size, byte[] inputbuffer, byte[] outputbuffer, int startIndex, uint length);
        ushort Minus(ushort a, ushort b);
        ushort Multiply(ushort a, ushort b);
        ushort Pow(ushort a, ushort b);
        uint limit { get; }
        ushort GetAntilog(int index);
    }
}
