using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    // The main packet is used to tie together the other packets in a recovery file.
    // It specifies the block size used to virtually slice the source files, a count
    // of the number of source files, and an array of Hash values used to specify
    // in what order the source files are processed.
    // Each entry in the fileid array corresponds with the fileid value
    // in a file description packet and a file verification packet.
    // The fileid array may contain more entries than the count of the number
    // of recoverable files. The extra entries correspond to files that were not
    // used during the creation of the recovery files and which may not therefore
    // be repaired if they are found to be damaged.
    public class MainPacket : IPar2Packet
    {
        public PacketHeader header;
        public UInt64 blocksize;
        public UInt32 recoverablefilecount;
        public UInt32 totalfilecount;
        public List<byte[]> fileids = new List<byte[]>();

        public int GetSize()
        {
            return header.GetSize() + sizeof(UInt64) + sizeof(UInt32) + (16 * sizeof(byte) * fileids.Count );
        }

        public static MainPacket Create(PacketHeader header, byte[] bytes, int index)
        {
            MainPacket tmpPacket = new MainPacket();
            tmpPacket.header = header;

            int offset = 0;

            tmpPacket.blocksize = BitConverter.ToUInt64(bytes, index + offset);
            offset += sizeof(UInt64);
            tmpPacket.recoverablefilecount = BitConverter.ToUInt32(bytes, index + offset);
            offset += sizeof(UInt32);

            tmpPacket.totalfilecount = ((uint)header.length - ((uint)header.GetSize() + sizeof(UInt64) + sizeof(UInt32))) / (16 * sizeof(byte));

            for (int i = 0; i < tmpPacket.totalfilecount; i++)
            {
                byte[] fileid = new byte[16];
                Buffer.BlockCopy(bytes, index + offset, fileid, 0, fileid.Length * sizeof(byte));
                offset += fileid.Length * sizeof(byte);
                tmpPacket.fileids.Add(fileid);
            }

            System.Threading.Tasks.Task.Factory.StartNew((b) =>
            {
                FastCRC32.FastCRC32 crc32 = new FastCRC32.FastCRC32((ulong)b);
            }, tmpPacket.blocksize);

            return tmpPacket;
        }
    }
}
