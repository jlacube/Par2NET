using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Par2NET.Packets
{
    public abstract class CriticalPacket : IPar2Packet
    {
        public PacketHeader header;

        public ulong PacketLength()
        {
            return header.length;
        }

        // C# to convert a string to a byte array.
        private byte[] StrToByteArray(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        private void WriteObject(BinaryWriter bw, object obj)
        {
            switch (obj.GetType().ToString().ToLower())
            {
                case "system.string":
                    bw.Write(StrToByteArray((string)obj));
                    break;
                case "system.uint64":
                case "system.ulong":
                    bw.Write((ulong)obj);
                    break;
                case "system.uint32":
                    bw.Write((uint)obj);
                    break;
                case "system.byte[]":
                    bw.Write((byte[])obj);
                    break;
                case "fileverificationentry":
                    FileVerificationEntry entry = (FileVerificationEntry)obj;
                    bw.Write(entry.hash);
                    bw.Write(entry.crc);
                    break;
                case "system.collections.generic.list`1[par2net.packets.fileverificationentry]":
                    foreach (FileVerificationEntry item in ((List<FileVerificationEntry>)obj))
                    {
                        bw.Write(item.hash);
                        bw.Write(item.crc);
                    }
                    break;
                case "system.collections.generic.list`1[system.byte[]]":
                    foreach (byte[] item in ((List<byte[]>)obj))
                    {
                        bw.Write(item);
                    }
                    break;
                default:
                    Debug.Assert(false);
                    throw new NotImplementedException(string.Format("Case '{0}' is not implemented for method 'WriteObject' of class CriticalPacket", obj.GetType()));
            }
        }

        protected bool WritePacket(DiskFile diskfile, ulong offset, params object[] objects)
        {
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
                    foreach (object obj in objects)
                    {
                        if (obj.GetType().IsArray)
                        {
                            if (obj.GetType().ToString().ToLower() != "system.byte[]")
                            {
                                foreach (object innerObj in (object[])obj)
                                {
                                    WriteObject(bw, innerObj);
                                }
                            }
                            else
                            {
                                WriteObject(bw, obj);
                            }
                        }
                        else
                        {
                            WriteObject(bw, obj);
                        }
                    }

                    byte[] buffer = ms.ToArray();

                    return diskfile.Write(offset, buffer, (uint)buffer.Length);
                }
            }
        }

        public abstract bool WritePacket(DiskFile diskfile, ulong offset);

        public abstract void FinishPacket(byte[] setid);
    }
}
