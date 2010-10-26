using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    // The creator packet is used to identify which program created a particular
    // recovery file. It is not required for verification or recovery of damaged
    // files.
    public class CreatorPacket : IPar2Packet
    {
        public PacketHeader header;
        public string client;

        public int GetSize()
        {
            return header.GetSize() + client.Length * sizeof(byte);
        }

        public static CreatorPacket Create(PacketHeader header, byte[] bytes, int index)
        {
            CreatorPacket tmpPacket = new CreatorPacket();
            tmpPacket.header = header;

            int offset = 0;

            // Name is specific to read since it's dependant of packet.length
            int name_offset = index + offset;
            int name_size = (int)header.length - header.GetSize();

            byte[] name = new byte[name_size];
            Buffer.BlockCopy(bytes, name_offset, name, 0, name_size);

            tmpPacket.client = ToolKit.ByteArrayToString(name);

            return tmpPacket;
        }
    }
}
