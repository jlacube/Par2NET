using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    // The recovery block packet contains a single block of recovery data along
    // with the exponent value used during the computation of that block.
    public class RecoveryPacket : IPar2Packet
    {
        public PacketHeader header;
        public UInt32 exponent;
        //public byte[] data; 
        public string filename;
        public Int32 offset;
        public Int32 length;

        public int GetSize()
        {
            return header.GetSize() + sizeof(UInt32);
        }

        public static RecoveryPacket Create(PacketHeader header, byte[] bytes, int index, string filename, int file_offset)
        {
            RecoveryPacket tmpPacket = new RecoveryPacket();
            tmpPacket.header = header;

            int offset = 0;

            tmpPacket.exponent = BitConverter.ToUInt32(bytes, index + offset);
            offset += sizeof(UInt32);

            tmpPacket.offset = index + offset + file_offset;
            tmpPacket.length = (int)header.length - header.GetSize() - sizeof(UInt32);
            tmpPacket.filename = filename;

            //// Data is specific to read since it's dependant of packet.length
            //int data_offset = index + offset;
            //int data_size = (int)header.length - header.GetSize() - sizeof(UInt32);

            //tmpPacket.data = new byte[data_size];

            //Buffer.BlockCopy(bytes, index + offset, tmpPacket.data, 0, tmpPacket.data.Length * sizeof(byte));
            //offset += tmpPacket.data.Length * sizeof(byte);

            return tmpPacket;
        }
    }
}
