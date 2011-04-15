using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Par2NET.Interfaces;
using System.Security.Cryptography;
using System.IO;

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
    public class MainPacket : CriticalPacket, IPar2Packet
    {
        public UInt64 blocksize;
        public UInt32 recoverablefilecount;
        public UInt32 totalfilecount;
        public List<byte[]> fileids = new List<byte[]>();

        public int GetSize()
        {
            return header.GetSize() + sizeof(UInt64) + sizeof(UInt32) + (16 * sizeof(byte) * fileids.Count );
        }

        public override bool WritePacket(DiskFile diskfile, ulong offset)
        {
            return base.WritePacket(diskfile, offset, this.blocksize, this.recoverablefilecount, this.fileids);
        }

        public static MainPacket Create(Par2LibraryArguments args)
        {
            MainPacket tmpPacket = new MainPacket();
            tmpPacket.header = new PacketHeader();
            tmpPacket.header.setid = new byte[16];
            tmpPacket.header.magic = Par2FileReader.packet_magic;
            tmpPacket.header.type = Par2FileReader.mainpacket_type;

            tmpPacket.blocksize = (ulong)args.blocksize;
            tmpPacket.recoverablefilecount = (uint)args.inputFiles.Length;
            tmpPacket.fileids = new List<byte[]>();
            tmpPacket.header.length = (ulong)(tmpPacket.GetSize() + (16 * sizeof(byte) * args.inputFiles.Length));

            // setid calculation and fileids insertion will occur in Par2RecoverySet.OpenSourceFiles method

            System.Threading.Tasks.Task.Factory.StartNew((b) =>
            {
                //FastCRC32.FastCRC32 crc32 = new FastCRC32.FastCRC32((ulong)b);
                FastCRC32.FastCRC32 crc32 = FastCRC32.FastCRC32.GetCRC32Instance((ulong)b);
            }, tmpPacket.blocksize);

            return tmpPacket;
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
                //FastCRC32.FastCRC32 crc32 = new FastCRC32.FastCRC32((ulong)b);
                FastCRC32.FastCRC32 crc32 = FastCRC32.FastCRC32.GetCRC32Instance((ulong)b);
            }, tmpPacket.blocksize);

            return tmpPacket;
        }

        public override void FinishPacket(byte[] setid)
        {
            header.setid = setid;

            MD5 packetcontext = MD5.Create();

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    // PacketHeader section
                    bw.Write(header.setid);
                    bw.Write(header.type);

                    //Packet section
                    bw.Write(blocksize);
                    bw.Write(recoverablefilecount);

                    foreach (byte[] fileid in fileids)
                    {
                        bw.Write(fileid);
                    }

                    byte[] buffer = ms.ToArray();

                    header.hash = packetcontext.ComputeHash(buffer);
                }
            }
        }
    }
}
