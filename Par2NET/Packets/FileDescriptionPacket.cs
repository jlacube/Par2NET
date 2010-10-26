using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    // The file description packet is used to record the name of the file,
    // its size, and the Hash of both the whole file and the first 16k of
    // the file.
    // If the name of the file is an exact multiple of 4 characters in length
    // then it may not have a NULL termination. If the name of the file is not
    // an exact multiple of 4, then it will be padded with 0 bytes at the
    // end to make it up to a multiple of 4.
    public class FileDescriptionPacket : IPar2Packet
    {
        public PacketHeader header;
        public byte[] fileid = new byte[16];    // MD5hash of [hash16k, length, name]
        public byte[] hashfull = new byte[16];  // MD5 Hash of the whole file
        public byte[] hash16k = new byte[16];   // MD5 Hash of the first 16k of the file
        public UInt64 length;                   // Length of the file
        public string name;                     // Name of the file, padded with 1 to 3 zero bytes to reach 
        // a multiple of 4 bytes.
        // Actual length can be determined from overall packet
        // length and then working backwards to find the first non
        // zero character.

        public int GetSize()
        {
            return header.GetSize() + 3 * (16 * sizeof(byte)) + sizeof(UInt64) + name.Length * sizeof(byte);
        }

        public static FileDescriptionPacket Create(PacketHeader header, byte[] bytes, int index)
        {
            FileDescriptionPacket tmpPacket = new FileDescriptionPacket();
            tmpPacket.header = header;

            int offset = 0;

            Buffer.BlockCopy(bytes, index + offset, tmpPacket.fileid, 0, tmpPacket.fileid.Length * sizeof(byte));
            offset += tmpPacket.fileid.Length * sizeof(byte);
            Buffer.BlockCopy(bytes, index + offset, tmpPacket.hashfull, 0, tmpPacket.hashfull.Length * sizeof(byte));
            offset += tmpPacket.hashfull.Length * sizeof(byte);
            Buffer.BlockCopy(bytes, index + offset, tmpPacket.hash16k, 0, tmpPacket.hash16k.Length * sizeof(byte));
            offset += tmpPacket.hash16k.Length * sizeof(byte);
            tmpPacket.length = BitConverter.ToUInt64(bytes, index + offset);
            offset += sizeof(UInt64);

            // Name is specific to read since it's dependant of packet.length
            int name_offset = index + offset;
            int name_size = (int)header.length - header.GetSize() - tmpPacket.fileid.Length * sizeof(byte) - tmpPacket.hashfull.Length * sizeof(byte) - tmpPacket.hash16k.Length * sizeof(byte) - sizeof(UInt64);

            byte[] name = new byte[name_size];
            Buffer.BlockCopy(bytes, name_offset, name, 0, name_size);

            tmpPacket.name = ToolKit.ByteArrayToString(name).TrimEnd('\0');

            return tmpPacket;
        }
    }
}
