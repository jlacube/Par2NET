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
            //FastCRC32.FastCRC32 crc32 = new FastCRC32.FastCRC32((ulong)blocksize);
            FastCRC32.FastCRC32 crc32 = FastCRC32.FastCRC32.GetCRC32Instance((ulong)blocksize);

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
                    long startCheck = DateTime.Now.Ticks;

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

                    long endCheck = DateTime.Now.Ticks;
                    double duration = ((double)(endCheck - startCheck)) / 10000;
                    Console.WriteLine("Check : {0} ms", duration);
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

        public static void CheckFile_async(DiskFile diskFile, string filename, int blocksize, List<FileVerificationEntry> fileVerEntry, byte[] md5hash16k, byte[] md5hash, ref MatchType matchType, Dictionary<uint,FileVerificationEntry> hashfull, Dictionary<uint,FileVerificationEntry> hash, List<FileVerificationEntry> expectedList, ref int expectedIndex, bool multithreadCPU)
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

                    List<ManualResetEvent> list = new List<ManualResetEvent>();

                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1024, true))
                    {
                        int offset = 0;

                        byte[] buffer = new byte[buffer_size];

                        ManualResetEvent mre = new ManualResetEvent(false);
                        list.Add(mre);

                        CheckBufferState state = new CheckBufferState(buffer, diskFile, filename, blocksize, hashfull, matchType, offset, overlap);

                        fs.BeginRead(buffer, 0, buffer_size, new AsyncCallback(TaskEndReadCallback), new FileCheckerState(fs, buffer, mre, state));

                        //tasks.Add(Task.Factory.StartNew((arg)
                        //        =>
                        //{
                        //    try
                        //    {
                        //        object[] args = (object[])arg;

                        //        byte[] b = (byte[])args[0];
                        //        DiskFile df = (DiskFile)args[1];
                        //        string f = (string)args[2];
                        //        int bs = (int)args[3];
                        //        Dictionary<uint, FileVerificationEntry> hf = (Dictionary<uint, FileVerificationEntry>)args[4];
                        //        MatchType mt = (MatchType)args[5];
                        //        int o = (int)args[6];

                        //        CheckBuffer(b, df, f, bs, hf, ref mt, o);
                        //    }
                        //    finally
                        //    {
                        //        //concurrencySemaphore.Release();
                        //    }
                        //}, new object[] { buffer, diskFile, filename, blocksize, hashfull, matchType, offset }));

                        while (fs.Position < fs.Length)
                        {
                            Buffer.BlockCopy(buffer, buffer.Length - overlap, buffer, 0, overlap);

                            ManualResetEvent mre2 = new ManualResetEvent(false);
                            list.Add(mre2);

                            CheckBufferState state2 = new CheckBufferState(buffer, diskFile, filename, blocksize, hashfull, matchType, offset, overlap);

                            fs.BeginRead(buffer, overlap, buffer.Length - overlap, new AsyncCallback(TaskEndReadCallback), new FileCheckerState(fs, buffer, mre2, state2));

                            int nbRead = fs.Read(buffer, overlap, buffer.Length - overlap);

                            offset += buffer.Length - overlap;

                            //if (nbRead < buffer.Length - overlap)
                            //    Array.Clear(buffer, (nbRead + overlap), buffer_size - (nbRead + overlap));

                            //tasks.Add(Task.Factory.StartNew((arg)
                            //    =>
                            //{
                            //    try
                            //    {
                            //        object[] args = (object[])arg;

                            //        byte[] b = (byte[])args[0];
                            //        DiskFile df = (DiskFile)args[1];
                            //        string f = (string)args[2];
                            //        int bs = (int)args[3];
                            //        Dictionary<uint, FileVerificationEntry> hf = (Dictionary<uint, FileVerificationEntry>)args[4];
                            //        MatchType mt = (MatchType)args[5];
                            //        int o = (int)args[6];

                            //        CheckBuffer(b, df, f, bs, hf, ref mt, o);
                            //    }
                            //    finally
                            //    {
                            //        //concurrencySemaphore.Release();
                            //    }
                            //}, new object[] { (byte[])buffer.Clone(), diskFile, filename, blocksize, hashfull, matchType, offset }));

                        }
                    }

                    //long startWait = DateTime.Now.Ticks;
                    //Task.WaitAll(tasks.ToArray());
                    //long endWait = DateTime.Now.Ticks;
                    //double duration = ((double)(endWait - startWait)) / 10000;
                    //Console.WriteLine("Wait : {0}ms", duration);

                    WaitHandle.WaitAll(list.ToArray());
                }
                else
                {
                    long startCheck = DateTime.Now.Ticks;

                    int buffer_size = THRESHOLD;
                    int overlap = 1 * blocksize; // part which will be check in double to avoid missing moving blocks

                    List<ManualResetEvent> list = new List<ManualResetEvent>();

                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 2 * buffer_size, true))
                    {
                        int offset = 0;

                        byte[] buffer = new byte[buffer_size];

                        CheckBufferState state = new CheckBufferState(buffer, diskFile, filename, blocksize, hashfull, matchType, offset, overlap);

                        ManualResetEvent mre = new ManualResetEvent(false);
                        list.Add(mre);

                        // Create a synchronization object that gets 
                        // signaled when verification is complete.
                        fs.BeginRead(buffer, 0, buffer_size, new AsyncCallback(EndReadCallback), new FileCheckerState(fs, buffer, mre, state));

                        //CheckBuffer(buffer, diskFile, filename, blocksize, hashfull, ref matchType, offset);

                        while (fs.Position < fs.Length)
                        {
                            Buffer.BlockCopy(buffer, buffer.Length - overlap, buffer, 0, overlap);

                            //int nbRead = fs.Read(buffer, overlap, buffer.Length - overlap);

                            CheckBufferState state2 = new CheckBufferState(buffer, diskFile, filename, blocksize, hashfull, matchType, offset, overlap);

                            // Create a synchronization object that gets 
                            // signaled when verification is complete.
                            ManualResetEvent mre2 = new ManualResetEvent(false);
                            list.Add(mre2);

                            fs.BeginRead(buffer, 0, buffer_size, new AsyncCallback(EndReadCallback), new FileCheckerState(fs, buffer, mre2, state2));

                            offset += buffer_size;

                            //if (nbRead < buffer.Length - overlap)
                            //    Array.Clear(buffer, (nbRead + overlap), buffer_size - (nbRead + overlap));

                            //CheckBuffer(buffer, diskFile, filename, blocksize, hashfull, ref matchType, offset);

                        }

                        WaitHandle.WaitAll(list.ToArray());
                    }

                    long endCheck = DateTime.Now.Ticks;
                    double duration = ((double)(endCheck - startCheck)) / 10000;
                    Console.WriteLine("Check : {0} ms", duration);
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

        // When BeginRead is finished reading data from the file, the 
        // EndReadCallback method is called to end the asynchronous 
        // read operation and then verify the data.
        static void EndReadCallback(IAsyncResult asyncResult)
        {
            FileCheckerState tempState = (FileCheckerState)asyncResult.AsyncState;
            int readCount = tempState.FStream.EndRead(asyncResult);

            if (readCount < tempState.State.Buffer.Length - tempState.State.Overlap)
                Array.Clear(tempState.State.Buffer, (readCount + tempState.State.Overlap), tempState.State.Buffer.Length - (readCount + tempState.State.Overlap));

            CheckBufferState state = tempState.State;

            MatchType matchType = MatchType.FullMatch;

            CheckBuffer(state.Buffer, state.DiskFile, state.FileName, state.BlockSize, state.HashFull, ref matchType, state.Offset);

            state.MatchType = matchType;

            //tempState.FStream.Close();

            // Signal the main thread that the verification is finished.
            tempState.ManualEvent.Set();
        }

        static void TaskEndReadCallback(IAsyncResult asyncResult)
        {
            FileCheckerState tempState = (FileCheckerState)asyncResult.AsyncState;
            int readCount = tempState.FStream.EndRead(asyncResult);

            if (readCount < tempState.State.Buffer.Length - tempState.State.Overlap)
                Array.Clear(tempState.State.Buffer, (readCount + tempState.State.Overlap), tempState.State.Buffer.Length - (readCount + tempState.State.Overlap));

            CheckBufferState state = tempState.State;

            Task.Factory.StartNew((arg)
                =>
            {
                try
                {
                    CheckBufferState innerState =  (CheckBufferState)arg;
                    
                    MatchType matchType = MatchType.FullMatch;
                    CheckBuffer(state.Buffer, state.DiskFile, state.FileName, state.BlockSize, state.HashFull, ref matchType, state.Offset);
                    state.MatchType = matchType;

                    // Signal the main thread that the verification is finished.
                    tempState.ManualEvent.Set();
                }
                finally
                {
                    //concurrencySemaphore.Release();
                }
            }, state);

            //tempState.FStream.Close();
        }
    }
}

