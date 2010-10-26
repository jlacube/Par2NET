using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace Par2NET
{
    public class FileChecker
    {
        private static byte[] MD5Hash16k(string filename)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 16384)))
            {
                return MD5.Create().ComputeHash(br.ReadBytes(16384));
            }
        }

        public static void CheckFile(string filename, int blocksize, List<byte[]> fileids, out byte[] md5hash16k, out byte[] md5hash)
        {
            md5hash = new byte[16];
            md5hash16k = new byte[16];

            int buffer_size = blocksize;

            CRC32NET.CRC32 crc32 = new CRC32NET.CRC32();
            MD5 md5Hasher = MD5.Create();

            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, buffer_size)))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int nbToRead = Math.Min(buffer_size, (int)(br.BaseStream.Length - br.BaseStream.Position));

                    byte[] bytes = br.ReadBytes(nbToRead);

                    if (bytes.Length < buffer_size)
                    {
                        byte[] newbytes = new byte[buffer_size];
                        Buffer.BlockCopy(bytes, 0, newbytes, 0, bytes.Length);
                        bytes = newbytes;
                    }

                    crc32.ComputeHash(bytes);
                    byte[] md5 = md5Hasher.ComputeHash(bytes);

                    Console.WriteLine("crc:{0},md5:{1}", crc32.CrcValue, ToolKit.ToHex(md5));
                }

                
            }

            crc32 = new CRC32NET.CRC32();
            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                crc32.ComputeHash(br.BaseStream);
            }

            md5Hasher = MD5.Create();
            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                md5hash = md5Hasher.ComputeHash(br.BaseStream);
            }

            Console.WriteLine("crc:{0},md5:{1}", crc32.CrcValue, ToolKit.ToHex(md5hash));
        }
    }
}
