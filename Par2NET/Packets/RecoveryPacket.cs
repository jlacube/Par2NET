using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace Par2NET.Packets
{
    // The recovery block packet contains a single block of recovery data along
    // with the exponent value used during the computation of that block.
    public class RecoveryPacket : IPar2Packet
    {
        public UInt32 exponent;
        public DataBlock datablock;
        public string filename;
        public Int32 offset; // rewrite to long
        public Int32 length;
        public DiskFile diskfile = null;
        public PacketHeader header = null;

        private MD5 packetcontext = null;

        public int GetSize()
        {
            return header.GetSize() + sizeof(UInt32);
        }

        public ulong PacketLength()
        {
            return (ulong)length;
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

            tmpPacket.diskfile = new DiskFile();
            tmpPacket.diskfile.Open(filename);

            tmpPacket.datablock = new DataBlock();
            // Set the data block to immediatly follow the header on disk
            tmpPacket.datablock.SetLocation(tmpPacket.diskfile, (ulong)tmpPacket.offset);
            tmpPacket.datablock.SetLength((ulong)tmpPacket.length);

            return tmpPacket;
        }

        internal DataBlock GetDataBlock()
        {
            return datablock;
        }

        internal static RecoveryPacket Create(DiskFile diskFile, ulong offset, ulong blocksize, uint exponent, byte[] setid)
        {
            RecoveryPacket tmpPacket = new RecoveryPacket();

            // Fill in the details the we know
            tmpPacket.header = new PacketHeader();
            tmpPacket.header.magic = Par2FileReader.packet_magic;

            tmpPacket.header.hash = new byte[16]; // Compute later
            tmpPacket.header.setid = setid;
            tmpPacket.header.type = Par2FileReader.recoveryblockpacket_type;

            tmpPacket.diskfile = diskFile;
            tmpPacket.offset = (int)offset;

            tmpPacket.exponent = exponent;

            //tmpPacket.length = 0; //
            tmpPacket.header.length = (ulong)(tmpPacket.header.GetSize() + sizeof(UInt32) + (int)blocksize);
            tmpPacket.length = (int)tmpPacket.header.length;

            // Start computation of the packet hash
            tmpPacket.packetcontext = MD5.Create();
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    // PacketHeader section
                    bw.Write(tmpPacket.header.setid);
                    bw.Write(tmpPacket.header.type);

                    //Packet section
                    bw.Write(tmpPacket.exponent);

                    byte[] buffer = ms.ToArray();

                    tmpPacket.packetcontext.TransformBlock(buffer, 0, buffer.Length, null, 0);
                }
            }

            // Set the data block to immediatly follow the header on disk
            tmpPacket.datablock = new DataBlock();
            tmpPacket.datablock.SetLocation(tmpPacket.diskfile, (ulong)(tmpPacket.offset + tmpPacket.GetSize()));
            tmpPacket.datablock.SetLength(blocksize);

            return tmpPacket;
        }

        // Write the header of the packet to disk
        internal bool WriteHeader()
        {
            // Finish computing the packet hash
            header.hash = packetcontext.Hash;

            // Write the header to disk
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    // PacketHeader section
                    bw.Write(header.magic);
                    bw.Write(header.length);
                    bw.Write(header.hash);
                    bw.Write(header.setid);
                    bw.Write(header.type);

                    //Packet section
                    bw.Write(exponent);

                    byte[] buffer = ms.ToArray();

                    return diskfile.Write((ulong)offset, buffer, (uint)buffer.Length);
                }
            }
        }

        // Write data from the buffer to the data block on disk
        internal bool WriteData(ulong position, ulong size, byte[] buffer, ulong start)
        {
            // Update the packet hash
            packetcontext.TransformFinalBlock(buffer, (int)start, (int)size); 
            //packetcontext.ComputeHash(buffer, (int)start, (int)size);

            // Write the data to the data block
            uint wrote;
            return datablock.WriteData(position, (uint)size, buffer, start, out wrote);
        }

        public void FinishPacket(byte[] setid)
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
                    bw.Write(exponent);

                    //TODO: To check

                    byte[] buffer = ms.ToArray();

                    header.hash = packetcontext.ComputeHash(buffer);
                }
            }
        }
    }
}
