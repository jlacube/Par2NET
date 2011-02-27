using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using Par2NET.Packets;
using Par2NET.Interfaces;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

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

        private static object _syncObject = new object();

        private static object _readerSyncObject = new object();

        private static void CheckBuffer(byte[] buffer, DiskFile diskFile, string filename, int blocksize, Dictionary<uint, FileVerificationEntry> hashfull, ref MatchType matchType, int globalOffset)
        {
            uint partial_key = (uint)(Path.GetFileName(filename).GetHashCode());

            MD5 md5Hasher = MD5.Create();
            FastCRC32.FastCRC32 crc32 = new FastCRC32.FastCRC32((ulong)blocksize);

            int offset = 0;

            byte inch = 0;
            byte outch = 0;

            uint crc32Value = 0;
            
            crc32Value = crc32.CRCUpdateBlock(0xFFFFFFFF, (uint)blocksize, buffer, (uint)offset) ^ 0xFFFFFFFF;

            while (offset < (buffer.Length - blocksize))
            {
                uint key = crc32Value ^ partial_key; 
                
                FileVerificationEntry entry = null;

                if (hashfull.ContainsKey(key))
                {
                    entry = hashfull[key];

                    byte[] blockhash = md5Hasher.ComputeHash(buffer, offset, blocksize);

                    if (ToolKit.ToHex(blockhash) == ToolKit.ToHex(entry.hash))
                    {
                        // We found a complete match, so go to next block !

                        //Console.WriteLine("block found at offset {0}, crc {1}", globalOffset + offset, entry.crc);
                        if (entry.datablock.diskfile == null)
                        {
                            lock (entry)
                            {
                                if (entry.datablock.diskfile == null)
                                {
                                    entry.SetBlock(diskFile, (int)(globalOffset + offset));
                                }
                            }
                        }

                        offset += blocksize;

                        crc32Value = crc32.CRCUpdateBlock(0xFFFFFFFF, (uint)(Math.Min(blocksize, buffer.Length - offset)), buffer, (uint)offset) ^ 0xFFFFFFFF;
                    }
                    else
                    {
                        if (offset + blocksize > buffer.Length)
                            return;

                        matchType = MatchType.PartialMatch;

                        inch = buffer[offset + blocksize];
                        outch = buffer[offset];

                        crc32Value = crc32.windowMask ^ crc32.CRCSlideChar(crc32.windowMask ^ crc32Value, inch, outch);

                        ++offset;
                    }
                }
                else
                {
                    if (offset + blocksize > buffer.Length)
                        return;

                    matchType = MatchType.PartialMatch;

                    inch = buffer[offset + blocksize];
                    outch = buffer[offset];

                    crc32Value = crc32.windowMask ^ crc32.CRCSlideChar(crc32.windowMask ^ crc32Value, inch, outch);

                    ++offset;
                }
            }
        }

        public static void CheckFile(DiskFile diskFile, string filename, int blocksize, List<FileVerificationEntry> fileVerEntry, byte[] md5hash16k, byte[] md5hash, ref MatchType matchType, Dictionary<uint,FileVerificationEntry> hashfull, Dictionary<uint,FileVerificationEntry> hash, List<FileVerificationEntry> expectedList, ref int expectedIndex, bool multithreadCPU)
        {
            //int THRESHOLD = ((int)((50 * 1024 * 1024) / blocksize) * blocksize); // 50Mo threshold
            int THRESHOLD = 5 * blocksize;

            matchType = MatchType.FullMatch;

            Console.WriteLine("Checking file '{0}'", Path.GetFileName(filename));

            long filesize = new FileInfo(filename).Length;

            if (filesize <= THRESHOLD)
            {
                using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    CheckBuffer(br.ReadBytes((int)filesize), diskFile, filename, blocksize, hashfull, ref matchType, 0);
                }
            }
            else
            {
                if (multithreadCPU)
                {
                    List<Task> tasks = new List<Task>();

                    int buffer_size = THRESHOLD;
                    int overlap = 1 * blocksize; // part which will be check in double to avoid missing moving blocks

                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int offset = 0;

                        byte[] buffer = new byte[buffer_size];

                        fs.Read(buffer, 0, buffer_size);

                        tasks.Add(Task.Factory.StartNew((arg)
                                =>
                        {
                            try
                            {
                                object[] args = (object[])arg;

                                byte[] b = (byte[])args[0];
                                DiskFile df = (DiskFile)args[1];
                                string f = (string)args[2];
                                int bs = (int)args[3];
                                Dictionary<uint, FileVerificationEntry> hf = (Dictionary<uint, FileVerificationEntry>)args[4];
                                MatchType mt = (MatchType)args[5];
                                int o = (int)args[6];

                                CheckBuffer(b, df, f, bs, hf, ref mt, o);
                            }
                            finally
                            {
                                //concurrencySemaphore.Release();
                            }
                        }, new object[] { buffer, diskFile, filename, blocksize, hashfull, matchType, offset }));

                        while (fs.Position < fs.Length)
                        {
                            Buffer.BlockCopy(buffer, buffer.Length - overlap, buffer, 0, overlap);

                            int nbRead = fs.Read(buffer, overlap, buffer.Length - overlap);

                            offset += nbRead;

                            if (nbRead < buffer.Length - overlap)
                                Array.Clear(buffer, (nbRead + overlap), buffer_size - (nbRead + overlap));

                            tasks.Add(Task.Factory.StartNew((arg)
                                =>
                            {
                                try
                                {
                                    object[] args = (object[])arg;

                                    byte[] b = (byte[])args[0];
                                    DiskFile df = (DiskFile)args[1];
                                    string f = (string)args[2];
                                    int bs = (int)args[3];
                                    Dictionary<uint, FileVerificationEntry> hf = (Dictionary<uint, FileVerificationEntry>)args[4];
                                    MatchType mt = (MatchType)args[5];
                                    int o = (int)args[6];

                                    CheckBuffer(b, df, f, bs, hf, ref mt, o);
                                }
                                finally
                                {
                                    //concurrencySemaphore.Release();
                                }
                            }, new object[] { (byte[])buffer.Clone(), diskFile, filename, blocksize, hashfull, matchType, offset }));

                        }
                    }

                    long startWait = DateTime.Now.Ticks;
                    Task.WaitAll(tasks.ToArray());
                    long endWait = DateTime.Now.Ticks;
                    double duration = ((double)(endWait - startWait)) / 10000;
                    Console.WriteLine("Wait : {0}ms", duration);
                }
                else
                {
                    int buffer_size = THRESHOLD;
                    int overlap = 1 * blocksize; // part which will be check in double to avoid missing moving blocks

                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int offset = 0;

                        byte[] buffer = new byte[buffer_size];

                        fs.Read(buffer, 0, buffer_size);

                        CheckBuffer(buffer, diskFile, filename, blocksize, hashfull, ref matchType, offset);

                        while (fs.Position < fs.Length)
                        {
                            Buffer.BlockCopy(buffer, buffer.Length - overlap, buffer, 0, overlap);

                            int nbRead = fs.Read(buffer, overlap, buffer.Length - overlap);

                            offset += nbRead;

                            if (nbRead < buffer.Length - overlap)
                                Array.Clear(buffer, (nbRead + overlap), buffer_size - (nbRead + overlap));

                            CheckBuffer(buffer, diskFile, filename, blocksize, hashfull, ref matchType, offset);

                        }
                    }
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

        public static void CheckFile_good(DiskFile diskFile, string filename, int blocksize, List<FileVerificationEntry> fileVerEntry, byte[] md5hash16k, byte[] md5hash, ref MatchType matchType, Dictionary<uint, FileVerificationEntry> hashfull, Dictionary<uint, FileVerificationEntry> hash, List<FileVerificationEntry> expectedList, ref int expectedIndex, bool multithreadCPU)
        {
            // Rewrite with :
            //  <= 50Mo, one step full buffer
            // > 50 Mo, two buffer 50Mo with 2*blocksize overlap

            matchType = MatchType.FullMatch;

            Console.WriteLine("Checking file '{0}'", Path.GetFileName(filename));

            if (multithreadCPU)
            {
                List<Task> tasks = new List<Task>();

                int readsize = 5 * blocksize;

                using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(Environment.ProcessorCount))
                {
                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        //int readsize = 5 * blocksize;

                        byte[] buffer1 = new byte[readsize];
                        byte[] buffer2 = new byte[readsize];

                        int offset = 0;

                        int nbRead1 = fs.Read(buffer1, 0, readsize);

                        while (fs.Position < fs.Length)
                        {
                            int nbRead2 = fs.Read(buffer2, 0, readsize);

                            byte[] buffer = new byte[nbRead1 + nbRead2];

                            Buffer.BlockCopy(buffer1, 0, buffer, 0, nbRead1);
                            Buffer.BlockCopy(buffer2, 0, buffer, nbRead1, nbRead2);

                            tasks.Add(Task.Factory.StartNew((arg)
                                =>
                            {
                                try
                                {
                                    object[] args = (object[])arg;

                                    byte[] b = (byte[])args[0];
                                    DiskFile df = (DiskFile)args[1];
                                    string f = (string)args[2];
                                    int bs = (int)args[3];
                                    Dictionary<uint, FileVerificationEntry> hf = (Dictionary<uint, FileVerificationEntry>)args[4];
                                    MatchType mt = (MatchType)args[5];
                                    int o = (int)args[6];

                                    CheckBuffer(b, df, f, bs, hf, ref mt, o);
                                }
                                finally
                                {
                                    //concurrencySemaphore.Release();
                                }
                            }, new object[] { buffer, diskFile, filename, blocksize, hashfull, matchType, offset }));

                            offset += buffer.Length;

                            Array.Clear(buffer1, 0, nbRead1);
                            byte[] tmpBuf = buffer1;

                            buffer1 = buffer2;

                            buffer2 = tmpBuf;
                        }
                    }

                    long startWait = DateTime.Now.Ticks;
                    Task.WaitAll(tasks.ToArray());
                    long endWait = DateTime.Now.Ticks;
                    double duration = ((double)(endWait - startWait)) / 10000;
                    Console.WriteLine("Wait : {0}ms", duration);
                }
            }
            else
            {
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int readsize = 5 * blocksize;

                    byte[] buffer1 = new byte[readsize];
                    byte[] buffer2 = new byte[readsize];

                    int offset = 0;

                    int nbRead1 = fs.Read(buffer1, 0, readsize);

                    while (fs.Position < fs.Length)
                    {
                        int nbRead2 = fs.Read(buffer2, 0, readsize);

                        byte[] buffer = new byte[nbRead1 + nbRead2];

                        Buffer.BlockCopy(buffer1, 0, buffer, 0, nbRead1);
                        Buffer.BlockCopy(buffer2, 0, buffer, nbRead1, nbRead2);

                        CheckBuffer(buffer, diskFile, filename, blocksize, hashfull, ref matchType, offset);

                        offset += buffer.Length;

                        Array.Clear(buffer1, 0, nbRead1);
                        byte[] tmpBuf = buffer1;

                        buffer1 = buffer2;

                        buffer2 = tmpBuf;
                    }
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
        
        public static void CheckFile_orig(DiskFile diskFile, string filename, int blocksize, List<FileVerificationEntry> fileVerEntry, byte[] md5hash16k, byte[] md5hash, ref MatchType matchType, Dictionary<uint, FileVerificationEntry> hashfull, Dictionary<uint, FileVerificationEntry> hash, List<FileVerificationEntry> expectedList, ref int expectedIndex, bool multithreadCPU)
        {
            matchType = MatchType.FullMatch;

            Console.WriteLine("Checking file '{0}'", Path.GetFileName(filename));

            if (multithreadCPU)
            {
                List<Task> tasks = new List<Task>();

                int readsize = 1 * blocksize;

                using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(Environment.ProcessorCount))
                {

                    //using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, readsize*2, FileOptions.SequentialScan)))
                    using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, readsize, FileOptions.SequentialScan)))
                    {
                        byte[] buffer1 = null;
                        byte[] buffer2 = null;

                        //int readsize = 5 * blocksize;

                        int offset = 0;

                        buffer1 = br.ReadBytes(readsize);

                        while (br.BaseStream.Position < br.BaseStream.Length)
                        {
                            buffer2 = br.ReadBytes(readsize);

                            byte[] buffer = new byte[buffer1.Length + buffer2.Length];

                            Buffer.BlockCopy(buffer1, 0, buffer, 0, buffer1.Length);
                            Buffer.BlockCopy(buffer2, 0, buffer, buffer1.Length, buffer2.Length);
                            //concurrencySemaphore.Wait();

                            tasks.Add(Task.Factory.StartNew((arg)
                                =>
                            {
                                try
                                {
                                    object[] args = (object[])arg;

                                    byte[] b = (byte[])args[0];
                                    DiskFile df = (DiskFile)args[1];
                                    string f = (string)args[2];
                                    int bs = (int)args[3];
                                    Dictionary<uint, FileVerificationEntry> hf = (Dictionary<uint, FileVerificationEntry>)args[4];
                                    MatchType mt = (MatchType)args[5];
                                    int o = (int)args[6];

                                    CheckBuffer(b, df, f, bs, hf, ref mt, o);
                                }
                                finally
                                {
                                    //concurrencySemaphore.Release();
                                }
                            }, new object[] { buffer, diskFile, filename, blocksize, hashfull, matchType, offset }));


                            offset += buffer.Length;

                            buffer1 = buffer2;
                        }

                    }

                    long startWait = DateTime.Now.Ticks;
                    Task.WaitAll(tasks.ToArray());
                    long endWait = DateTime.Now.Ticks;
                    double duration = ((double)(endWait - startWait)) / 10000;
                    Console.WriteLine("Wait : {0}ms", duration);
                }
            }
            else
            {
                using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    byte[] buffer1 = null;
                    byte[] buffer2 = null;

                    int readsize = 5 * blocksize;

                    int offset = 0;

                    buffer1 = br.ReadBytes(readsize);

                    while (br.BaseStream.Position < br.BaseStream.Length)
                    {
                        buffer2 = br.ReadBytes(readsize);

                        byte[] buffer = new byte[buffer1.Length + buffer2.Length];

                        Buffer.BlockCopy(buffer1, 0, buffer, 0, buffer1.Length);
                        Buffer.BlockCopy(buffer2, 0, buffer, buffer1.Length, buffer2.Length);

                        CheckBuffer(buffer, diskFile, filename, blocksize, hashfull, ref matchType, offset);

                        offset += buffer.Length;

                        buffer1 = buffer2;
                    }
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

