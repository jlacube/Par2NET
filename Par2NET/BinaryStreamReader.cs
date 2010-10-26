using System.IO;

namespace Par2NET
{
    public class BinaryStreamReader : BinaryReader
    {

        public BinaryStreamReader(Stream stream)
            : base(stream)
        {
        }

        protected void Advance(int bytes)
        {
            BaseStream.Seek(bytes, SeekOrigin.Current);
        }
    }
}
