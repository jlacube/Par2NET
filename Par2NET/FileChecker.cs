using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using Par2NET.Packets;

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

        public static void CheckFile(string filename, int blocksize, List<FileVerificationEntry> fileVerEntry, byte[] md5hash16k, byte[] md5hash)
        {
            //TODO : Maybe rewrite in TPL for slide calculation
            //TODO : Maybe search against all sourcefiles (case of misnamed files)

            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 2*blocksize)))
            {
                CRC32NET.CRC32 crc32Hasher = new CRC32NET.CRC32();
                MD5 md5Hasher = MD5.Create();

                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int toRead = Math.Min((2*blocksize), (int)(br.BaseStream.Length - br.BaseStream.Position));

                    // Prepare sliding & working buffer
                    byte[] buffer = br.ReadBytes(toRead);
                    byte[] workingBuffer = new byte[blocksize];
                    int offset = 0;
                    Buffer.BlockCopy(buffer, offset, workingBuffer, 0, blocksize);

                    do
                    {
                        // Compute crc32 & md5 hash for current slice
                        crc32Hasher.ComputeHash(workingBuffer);
                        uint crc32Value = crc32Hasher.CrcValue;
                        byte[] md5Value = md5Hasher.ComputeHash(workingBuffer);

                        // Do we have a match in the FileVerificationEntry
                        FileVerificationEntry entry = fileVerEntry.Find((FileVerificationEntry item) =>
                        {
                            if (item.crc == crc32Value && ToolKit.ToHex(item.hash) == ToolKit.ToHex(md5Value))
                                return true;

                            return false;
                        });

                        if (entry != null)
                        {
                            // We find a match, so go to next block !
                            offset = blocksize;
                            //TODO : record the block found at (br.BaseStream.Position - toRead) + offset
                        }
                        else
                        {
                            // We didn't find a match, maybe file is corrupted, so let's slide one byte and check again
                            ++offset;
                            Buffer.BlockCopy(buffer, offset, workingBuffer, 0, blocksize);

                            // Way too long for now, so until speed in calculations, we stop after a 10k slide
                            if (offset == 10240)
                                offset = blocksize;
                        }
                    } while (offset < blocksize); // Stop condition : When index is equal to blocksize, end of sliding buffer is reached, so we have to read from file
                }
            }
        }

        public static void CheckFile_orig(string filename, int blocksize, List<FileVerificationEntry> fileids, byte[] md5hash16k, byte[] md5hash)
        {
            byte[] targetMd5hash = new byte[16];
            byte[] targetMd5hash16k = MD5Hash16k(filename);

            int buffer_size = blocksize;

            CRC32NET.CRC32 crc32 = new CRC32NET.CRC32();
            MD5 md5Hasher = MD5.Create();
            MD5 md5FullHasher = MD5.Create();

            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, buffer_size)))
            {
                long stop = br.BaseStream.Length - buffer_size;

                while (br.BaseStream.Position < stop)
                {
                    byte[] bytes = br.ReadBytes(buffer_size);
                    byte[] fullbytes = (byte[])bytes.Clone();
                    crc32.ComputeHash(bytes);
                    uint crc = crc32.CrcValue;
                    byte[] md5 = md5Hasher.ComputeHash(bytes);
                    md5FullHasher.TransformBlock(fullbytes, 0, fullbytes.Length, fullbytes, 0);
                    //Console.WriteLine("crc:{0},md5:{1}", crc, ToolKit.ToHex(md5));
                }

                byte[] tmpbytes = br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position));
                byte[] finalbytes = new byte[buffer_size];
                Buffer.BlockCopy(tmpbytes, 0, finalbytes, 0, tmpbytes.Length);
                byte[] finalfullbytes = (byte[])finalbytes.Clone();
                crc32.ComputeHash(finalbytes);
                uint crcfinal = crc32.CrcValue;
                byte[] md5final = md5Hasher.ComputeHash(finalbytes);
                md5FullHasher.TransformFinalBlock(finalfullbytes, 0, finalfullbytes.Length);
                md5hash = md5FullHasher.Hash;

                //Console.WriteLine("crc:{0},md5:{1}", crcfinal, ToolKit.ToHex(md5final));
            }
        }
    }
}
