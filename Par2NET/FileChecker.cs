using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using Par2NET.Packets;
using System.Diagnostics;

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

        private static byte[] MD5Hash(string filename)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                return MD5.Create().ComputeHash(br.BaseStream);
            }
        }

        public static bool QuickCheckFile(string filename, int blocksize, out long filesize, out uint nb_blocks, out byte[] md5hash16k, out byte[] md5hash)
        {
            filesize = 0;
            nb_blocks = 0;
            md5hash = null;
            md5hash16k = null;

            try
            {
                FileInfo fiFile = new FileInfo(filename);

                if (!fiFile.Exists)
                    return false;

                filesize = fiFile.Length;
                nb_blocks = blocksize > 0 ? (filesize % blocksize == 0 ? (uint)(filesize / blocksize) : (uint)(filesize / blocksize + 1)) : 0;
                md5hash = MD5Hash(filename);
                md5hash16k = MD5Hash16k(filename);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);

                return false;
            }
        }

        public static void CheckFile(string filename, int blocksize, List<FileVerificationEntry> fileVerEntry, byte[] md5hash16k, byte[] md5hash, ref MatchType matchType, Dictionary<uint,FileVerificationEntry> hashfull, Dictionary<uint,FileVerificationEntry> hash)
        {
            //TODO : Maybe rewrite in TPL for slide calculation
            //TODO : Maybe search against all sourcefiles (case of misnamed files)

            matchType = MatchType.FullMatch;

            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 2*blocksize)))
            {
                CRC32NET.CRC32 crc32Hasher = new CRC32NET.CRC32();
                MD5 md5Hasher = MD5.Create();
                FastCRC32.FastCRC32 crc32 = new FastCRC32.FastCRC32((ulong)blocksize);

                uint partial_key = (uint)(17 * Path.GetFileName(filename).GetHashCode());

                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int nbRead = Math.Min((2*blocksize), (int)(br.BaseStream.Length - br.BaseStream.Position));

                    // Prepare buffer
                    byte[] buffer = br.ReadBytes(nbRead);
                    int offset = 0;

                    bool stepping = false;

                    byte inch = 0;
                    byte outch = 0;

                    uint crc32Value = 0;

                    do
                    {
                        // Compute crc32 for current slice

                        if (!stepping)
                        {
                            crc32Value = crc32.CRCUpdateBlock(0xFFFFFFFF, (uint)blocksize, buffer, (uint)offset) ^ 0xFFFFFFFF;
                        }
                        else
                        {
                            inch = buffer[offset + blocksize - 1];

                            crc32Value = crc32.windowMask ^ crc32.CRCSlideChar(crc32.windowMask ^ crc32Value, inch, outch);
                        }

                        stepping = false;

                        // Do we have a match in the FileVerificationEntry
                        //FileVerificationEntry entry = fileVerEntry.Find((FileVerificationEntry item) =>
                        //{
                        //    if (item.crc == crc32Value)
                        //    {
                        //        // We find a CRC32 match, let's check the MD5 hash now
                        //        byte[] md5Value = md5Hasher.ComputeHash(buffer, offset, blocksize);

                        //        return ToolKit.ToHex(item.hash) == ToolKit.ToHex(md5Value);
                        //    }

                        //    return false;
                        //});

                        FileVerificationEntry entry = null;

                        uint key = crc32Value ^ partial_key;

                        if (hashfull.ContainsKey(key))
                        {
                            entry = hashfull[key];
                        }
                        else
                        {
                            if (hash.ContainsKey(crc32Value))
                                entry = hash[crc32Value];
                        }

                        if (entry != null)
                        {
                            // We found a complete match, so go to next block !

                            //TODO : correct offset counter for last block
                            Console.WriteLine("block found at offset {0}, crc {1}", (br.BaseStream.Position - nbRead + offset), entry.crc);

                            entry.SetBlock(new DiskFile(filename), (int)(br.BaseStream.Position - nbRead + offset));

                            offset += blocksize;
                        }
                        else
                        {
                            if (br.BaseStream.Position == br.BaseStream.Length)
                                break;
                            else
                            {
                                matchType = MatchType.PartialMatch;
                                outch = buffer[offset];
                                ++offset;
                                stepping = true;
                            }
                        }

                        if (offset >= 2 * blocksize)
                            break;

                        if (offset >= blocksize)
                        {
                            byte[] newBuffer = new byte[buffer.Length];
                            Buffer.BlockCopy(buffer, offset, newBuffer, 0, 2 * blocksize - offset);

                            byte[] readBytes = br.ReadBytes(Math.Min(offset, (int)(br.BaseStream.Length - br.BaseStream.Position)));

                            Buffer.BlockCopy(readBytes, 0, newBuffer, 2 * blocksize - offset, readBytes.Length);

                            offset = 0;

                            buffer = newBuffer;
                        }

                    } while (offset < 2*blocksize); // Stop condition : When index is equal to blocksize, end of sliding buffer is reached, so we have to read from file
                }
            }

            bool atLeastOne = false;
            foreach (FileVerificationEntry entry in fileVerEntry)
            {
                if (entry.datablock.diskfile != null)
                {
                    atLeastOne = true;
                    break;
                }
            }

            if (!atLeastOne)
                matchType = MatchType.NoMatch;
        }
    }
}

