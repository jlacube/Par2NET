using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using CRC32NET;
using System.Collections.Concurrent;
using System.Threading;
using System.Security.Cryptography;
using System.Diagnostics;

namespace CRC32Test
{
    class Program
    {
        static BlockingCollection<byte[]> workingCRC32Data = new BlockingCollection<byte[]>();
        static BlockingCollection<byte[]> workingMD5Data = new BlockingCollection<byte[]>();

        static CRC32 crc32Hasher = new CRC32();
        static MD5 md5Hasher = MD5.Create();

        static int buffer_size = 128 * 1024;

        static CancellationTokenSource cancelToken = new CancellationTokenSource();

        private static void ComputeCRC32Block()
        {
            foreach (byte[] bytes in workingCRC32Data.GetConsumingEnumerable())
            {
                crc32Hasher.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }

            byte[] dummy = new byte[0];
            crc32Hasher.TransformFinalBlock(dummy, 0, 0);
        }

        private static void ComputeMD5Block()
        {
            foreach (byte[] bytes in workingMD5Data.GetConsumingEnumerable())
            {
                md5Hasher.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }

            byte[] dummy = new byte[0];
            md5Hasher.TransformFinalBlock(dummy, 0, 0);
        }

        static void ReadFile(string filename)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                long stop = br.BaseStream.Length - buffer_size;

                while (br.BaseStream.Position < stop)
                {
                    byte[] crc32bytes = br.ReadBytes(buffer_size);
                    byte[] md5bytes = new byte[crc32bytes.Length];
                    Buffer.BlockCopy(crc32bytes, 0, md5bytes, 0, md5bytes.Length);

                    workingCRC32Data.Add(crc32bytes);
                    workingMD5Data.Add(md5bytes);
                }

                byte[] finalcrc32bytes = br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position));
                byte[] finalmd5bytes = new byte[finalcrc32bytes.Length];
                Buffer.BlockCopy(finalcrc32bytes, 0, finalmd5bytes, 0, finalmd5bytes.Length);
                workingCRC32Data.Add(finalcrc32bytes);
                workingMD5Data.Add(finalmd5bytes);

                workingCRC32Data.CompleteAdding();
                workingMD5Data.CompleteAdding();
            }
        }

        static void Main(string[] args)
        {
            string filename = @"C:\USERS\jlacube011707\Downloads\en_visual_studio_2010_ultimate_x86_dvd_509116.iso";

            Console.ReadLine();

            cancelToken = new CancellationTokenSource();

            Console.WriteLine("round\tbuffer_size\thash\ttime");

            for (int i = 0; i < 1; i++)
            {
                workingCRC32Data = new BlockingCollection<byte[]>();
                workingMD5Data = new BlockingCollection<byte[]>();

                crc32Hasher = new CRC32();
                md5Hasher = MD5.Create();

                buffer_size = 128 * 1024;

                DateTime startTime = DateTime.Now;

                Task ComputingCRC32Task = Task.Factory.StartNew((bytes) =>
                {
                    ComputeCRC32Block();
                }, TaskCreationOptions.LongRunning);

                Task ComputingMD5Task = Task.Factory.StartNew((bytes) =>
                {
                    ComputeMD5Block();
                }, TaskCreationOptions.LongRunning);

                Task Readingtask = Task.Factory.StartNew((file) =>
                {
                    ReadFile((string)file);
                }, filename, TaskCreationOptions.LongRunning);

                Task.WaitAll(ComputingCRC32Task, ComputingMD5Task, Readingtask);

                TimeSpan duration = DateTime.Now - startTime;

                Console.WriteLine(string.Format("{0}\t{1}\t{2}\t{3}.{4}.{5}", i, buffer_size, CRC32.ToHex(crc32Hasher.Hash), duration.Minutes, duration.Seconds, duration.Milliseconds));
            }

            Console.WriteLine();

            //Console.WriteLine("round\thash\ttime");
            //for (int i = 0; i < 10; i++)
            //{
            //    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            //    {
            //        CRC32 crc32Hasher = new CRC32();

            //        DateTime startTime = DateTime.Now;

            //        byte[] output = crc32Hasher.ComputeHash(fs);

            //        TimeSpan duration = DateTime.Now - startTime;

            //        Console.WriteLine(string.Format("{0}\t{1}\t{2}.{3}.{4}", i, CRC32.ToHex(crc32Hasher.Hash), duration.Minutes, duration.Seconds, duration.Milliseconds));

            //    }
            //}
            //Console.WriteLine();
            
        }
    }
}
