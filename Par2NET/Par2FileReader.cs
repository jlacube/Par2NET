using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

using Par2NET.Packets;
using Par2NET.Interfaces;

namespace Par2NET
{
    public class Par2FileReader : BinaryStreamReader
    {
        int buffer_size = 1048576;

        public static byte[] packet_magic;
        public static byte[] fileverificationpacket_type;
        public static byte[] filedescriptionpacket_type;
        public static byte[] mainpacket_type;
        public static byte[] recoveryblockpacket_type;
        public static byte[] creatorpacket_type;

        Par2RecoverySet par2RecoverySet;
        FileInfo fileInfo;

        static Par2FileReader()
        {
            packet_magic = ToolKit.InitByteArrayFromChars('P', 'A', 'R', '2', '\0', 'P', 'K', 'T');
            fileverificationpacket_type = ToolKit.InitByteArrayFromChars('P', 'A', 'R', ' ', '2', '.', '0', '\0', 'I', 'F', 'S', 'C', '\0', '\0', '\0', '\0');
            filedescriptionpacket_type = ToolKit.InitByteArrayFromChars('P', 'A', 'R', ' ', '2', '.', '0', '\0', 'F', 'i', 'l', 'e', 'D', 'e', 's', 'c');
            mainpacket_type = ToolKit.InitByteArrayFromChars('P', 'A', 'R', ' ', '2', '.', '0', '\0', 'M', 'a', 'i', 'n', '\0', '\0', '\0', '\0');
            recoveryblockpacket_type = ToolKit.InitByteArrayFromChars('P', 'A', 'R', ' ', '2', '.', '0', '\0', 'R', 'e', 'c', 'v', 'S', 'l', 'i', 'c');
            creatorpacket_type = ToolKit.InitByteArrayFromChars('P', 'A', 'R', ' ', '2', '.', '0', '\0', 'C', 'r', 'e', 'a', 't', 'o', 'r', '\0');
        }

        private Par2FileReader(Stream stream, bool multithreadCPU, bool multithreadIO)
            : base(stream)
        {
            par2RecoverySet = new Par2RecoverySet(multithreadCPU, multithreadIO);

            
        }

        public Par2FileReader(string filename, bool multithreadCPU, bool multithreadIO)
            : this(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 10*1024*1024, FileOptions.Asynchronous), multithreadCPU, multithreadIO)
        {
            fileInfo = new FileInfo(filename);
        }

        void MoveTo(uint position)
        {
            BaseStream.Position = position;
        }

        private PacketType GetPacketType(byte[] type)
        {
            if (ToolKit.SafeCompare(type, filedescriptionpacket_type))
                return PacketType.DescriptionPacket;

            if (ToolKit.SafeCompare(type, creatorpacket_type))
                return PacketType.CreatorPacket;

            if (ToolKit.SafeCompare(type, fileverificationpacket_type))
                return PacketType.VerificationPacket;

            if (ToolKit.SafeCompare(type, mainpacket_type))
                return PacketType.MainPacket;

            if (ToolKit.SafeCompare(type, recoveryblockpacket_type))
                return PacketType.RecoveryPacket;

            if (ToolKit.SafeCompare(type, packet_magic))
                return PacketType.MagicPacket;

            return PacketType.Unknown;
        }

        public void GetMagicPackets()
        {
            int readsize = (int)Math.Min((long)10485760, fileInfo.Length);

            byte[] bytes = ReadBytes(readsize);

            int index = 0;

            do
            {
                index = ToolKit.IndexOf(bytes, packet_magic, index);

                if (index < 0)
                    break;

                Console.WriteLine(index);

                index++;

            } while (index >= 0);

        }


        public Par2RecoverySet ReadRecoverySet()
        {
            //int readsize = (int)Math.Min((long)buffer_size, fileInfo.Length);

            int readsize = (int)fileInfo.Length;

            byte[] bytes = ReadBytes(readsize);

            int index = 0;

            int file_offset = 0;

            //while (BaseStream.Position < BaseStream.Length)
            {
                do
                {
                    index = ToolKit.IndexOf(bytes, packet_magic, index);

                    if (index < 0)
                        break;

                    PacketHeader header = PacketHeader.Create(bytes, index);

                    switch (GetPacketType(header.type))
                    {
                        case PacketType.CreatorPacket:
                            CreatorPacket createPacket = CreatorPacket.Create(header, bytes, index + header.GetSize());
                            par2RecoverySet.AddCreatorPacket(createPacket);
                            index += createPacket.GetSize() - header.GetSize();
                            break;
                        case PacketType.DescriptionPacket:
                            FileDescriptionPacket descPacket = FileDescriptionPacket.Create(header, bytes, index + header.GetSize());
                            par2RecoverySet.AddDescriptionPacket(descPacket);
                            index += descPacket.GetSize() - header.GetSize();
                            break;
                        case PacketType.MainPacket:
                            MainPacket mainPacket = MainPacket.Create(header, bytes, index + header.GetSize());
                            par2RecoverySet.AddMainPacket(mainPacket);
                            index += mainPacket.GetSize() - header.GetSize();
                            break;
                        case PacketType.RecoveryPacket:
                            RecoveryPacket recoveryPacket = RecoveryPacket.Create(header, bytes, index + header.GetSize(), fileInfo.FullName, file_offset);
                            par2RecoverySet.AddRecoveryPacket(recoveryPacket);
                            index += recoveryPacket.GetSize() - header.GetSize();
                            break;
                        case PacketType.VerificationPacket:
                            FileVerificationPacket verPacket = FileVerificationPacket.Create(header, bytes, index + header.GetSize());
                            par2RecoverySet.AddVerificationPacket(verPacket);
                            index += verPacket.GetSize() - header.GetSize();
                            break;
                        case PacketType.Unknown:
                            // Unknown packettype
                            break;
                    }

                } while (index >= 0);

                index = 0;

                readsize = (int)Math.Min((long)buffer_size, BaseStream.Length - BaseStream.Position);

                file_offset += bytes.Length;

                bytes = ReadBytes(readsize);
            }

            return par2RecoverySet;
        }
    }

    // Every packet starts with a packet header.
    public class PacketHeader
    {
        // Header
        public byte[] magic = new byte[8];    // = {'P', 'A', 'R', '2', '\0', 'P', 'K', 'T'}
        public UInt64 length;                 // Length of entire packet including header
        public byte[] hash = new byte[16];    // Hash of entire packet excepting the first 3 fields
        public byte[] setid = new byte[16];   // Normally computed as the Hash of body of "Main Packet"
        public byte[] type = new byte[16];    // Used to specify the meaning of the rest of the packet

        public int GetSize()
        {
            return 64;
        }

        public static PacketHeader Create(byte[] bytes, int index)
        {
            PacketHeader tmpHeader = new PacketHeader();

            int offset = 0;

            Buffer.BlockCopy(bytes, index + offset, tmpHeader.magic, 0, tmpHeader.magic.Length * sizeof(byte));
            offset += tmpHeader.magic.Length * sizeof(byte);
            tmpHeader.length = BitConverter.ToUInt64(bytes, index + offset);
            offset += sizeof(UInt64);
            Buffer.BlockCopy(bytes, index + offset, tmpHeader.hash, 0, tmpHeader.hash.Length * sizeof(byte));
            offset += tmpHeader.hash.Length * sizeof(byte);
            Buffer.BlockCopy(bytes, index + offset, tmpHeader.setid, 0, tmpHeader.setid.Length * sizeof(byte));
            offset += tmpHeader.setid.Length * sizeof(byte);
            Buffer.BlockCopy(bytes, index + offset, tmpHeader.type, 0, tmpHeader.type.Length * sizeof(byte));
            offset += tmpHeader.type.Length * sizeof(byte);

            return tmpHeader;
        }
    }


    
}