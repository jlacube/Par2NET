using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace Par2NET.Packets
{
    // The creator packet is used to identify which program created a particular
    // recovery file. It is not required for verification or recovery of damaged
    // files.
    public class CreatorPacket : CriticalPacket, IPar2Packet
    {
        public string client;

        public int GetSize()
        {
            return header.GetSize() + client.Length * sizeof(byte);
        }

        public override bool WritePacket(DiskFile diskfile, ulong offset)
        {
            return base.WritePacket(diskfile, offset, this.client);
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

        public static CreatorPacket Create(byte[] setid)
        {
            //string creator = string.Format("Created by {0} version {1}.", Par2Library.PACKAGE, Par2Library.VERSION);
            string creator = "Created by par2cmdline version 0.4.";
            string pad = new string(new char[4 - (creator.Length % 4)]);

            CreatorPacket tmpPacket = new CreatorPacket();

            // Fill in the details the we know
            tmpPacket.header = new PacketHeader();
            tmpPacket.header.magic = Par2FileReader.packet_magic;
            
            tmpPacket.header.hash = new byte[16]; // Compute later
            tmpPacket.header.setid = setid;
            tmpPacket.header.type = Par2FileReader.creatorpacket_type;

            tmpPacket.client = creator + pad;

            tmpPacket.header.length = (ulong)(tmpPacket.GetSize());

            // Compute the packet hash
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(tmpPacket.header.setid);
                    bw.Write(tmpPacket.header.type);
                    bw.Write(tmpPacket.client);
                }

                byte[] buffer = ms.ToArray();
                // Compute the fileid from the hash, length, and name fields in the packet.
                MD5 md5Hasher = MD5.Create();
                tmpPacket.header.hash = md5Hasher.ComputeHash(buffer);
            }

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
                    bw.Write(client);

                    byte[] buffer = ms.ToArray();

                    header.hash = packetcontext.ComputeHash(buffer);
                }
            }
        }
    }
}
