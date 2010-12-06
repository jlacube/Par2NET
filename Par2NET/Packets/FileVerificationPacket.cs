using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    // The file verification packet is used to determine whether or not any
    // parts of a damaged file are useable.
    // It contains a FileId used to pair it with a corresponding file description
    // packet, followed by an array of hash and crc values. The number of entries in
    // the array can be determined from the packet_length.

    public class FileVerificationEntry
    {
        public byte[] hash = new byte[16];
        public UInt32 crc;
        public DataBlock datablock = new DataBlock();

        public static int GetSize()
        {
            return 16 * sizeof(byte) + sizeof(UInt32);
        }

        internal void SetBlock(DiskFile diskfile, int offset)
        {
            datablock.SetLocation(diskfile, (ulong)offset);
        }
    }

    public class FileVerificationPacket : IPar2Packet
    {
        public PacketHeader header;
        // Body
        public byte[] fileid = new byte[16];            // MD5hash of file_hash_16k, file_length, file_name
        public List<FileVerificationEntry> entries;

        public ulong blockcount = 0;

        public int GetSize()
        {
            return header.GetSize() + 16 * sizeof(byte) + (entries.Count * FileVerificationEntry.GetSize());
        }

        public static FileVerificationPacket Create(PacketHeader header, byte[] bytes, int index)
        {
            FileVerificationPacket tmpPacket = new FileVerificationPacket();
            tmpPacket.header = header;

            int offset = 0;

            Buffer.BlockCopy(bytes, index + offset, tmpPacket.fileid, 0, tmpPacket.fileid.Length * sizeof(byte));
            offset += tmpPacket.fileid.Length * sizeof(byte);

            int nbEntries = ((int)header.length - header.GetSize() - (16 * sizeof(byte))) / FileVerificationEntry.GetSize();

            tmpPacket.entries = new List<FileVerificationEntry>();

            tmpPacket.blockcount = (ulong)((header.length - (ulong)tmpPacket.GetSize()) / (ulong)FileVerificationEntry.GetSize());

            for (int i = 0; i < nbEntries; i++)
            {
                FileVerificationEntry entry = new FileVerificationEntry();

                Buffer.BlockCopy(bytes, index + offset, entry.hash, 0, entry.hash.Length * sizeof(byte));
                offset += entry.hash.Length * sizeof(byte);
                entry.crc = BitConverter.ToUInt32(bytes, index + offset);
                offset += sizeof(UInt32);

                tmpPacket.entries.Add(entry);
            }

            return tmpPacket;
        }
    }
}
