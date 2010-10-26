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
        static BlockingCollection<byte[]> resultsCRC32 = new BlockingCollection<byte[]>();
        static BlockingCollection<byte[]> resultsMD5 = new BlockingCollection<byte[]>();

        static CancellationTokenSource cancelToken = new CancellationTokenSource();

        static void ComputeFile(string filename)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int nbToRead = (int)Math.Min((128* 1048576), br.BaseStream.Length - br.BaseStream.Position);

                    byte[] bytes = br.ReadBytes(nbToRead);

                    Task.Factory.StartNew((entry) =>
                    {
                        CRC32 crc32Hasher = new CRC32();

                        resultsCRC32.Add(crc32Hasher.ComputeHash((byte[])entry));

                    }, bytes, TaskCreationOptions.AttachedToParent);

                    Task.Factory.StartNew((entry) =>
                    {
                        MD5 md5Hasher = MD5.Create();

                        resultsMD5.Add(md5Hasher.ComputeHash((byte[])entry));

                    }, bytes, TaskCreationOptions.AttachedToParent);
                }
            }
        }

        static void Main(string[] args)
        {
            string filename = @"C:\USERS\jlacube011707\Downloads\en_visual_studio_2010_ultimate_x86_dvd_509116.iso";

            DateTime startTime = DateTime.Now;

            byte[] finalCRC32Hash = null;
            byte[] finalMD5Hash = null;

            cancelToken = new CancellationTokenSource();

            Task aggregationCRC32Task = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        foreach (byte[] item in resultsCRC32.GetConsumingEnumerable(cancelToken.Token))
                        {
                            if (finalCRC32Hash == null)
                                finalCRC32Hash = new byte[item.Length];

                            for (int i = 0; i < finalCRC32Hash.Length; i++)
                            {
                                finalCRC32Hash[i] ^= item[i];
                            }
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        Debug.WriteLine(oce);
                    }
                }, cancelToken.Token);

            Task aggregationMD5Task = Task.Factory.StartNew(() =>
            {
                try
                {
                    foreach (byte[] item in resultsMD5.GetConsumingEnumerable(cancelToken.Token))
                    {
                        if (finalMD5Hash == null)
                            finalMD5Hash = new byte[item.Length];

                        for (int i = 0; i < finalMD5Hash.Length; i++)
                        {
                            finalMD5Hash[i] ^= item[i];
                        }
                    }
                }
                catch (OperationCanceledException oce)
                {
                    Debug.WriteLine(oce);
                }
            }, cancelToken.Token);

            Task.Factory.StartNew((file) =>
                {
                    ComputeFile((string)file);
                }, filename).Wait();

            cancelToken.Cancel();

            resultsCRC32.CompleteAdding();
            resultsMD5.CompleteAdding();

            Console.WriteLine(CRC32.ToHex(finalCRC32Hash));
            Console.WriteLine(CRC32.ToHex(finalMD5Hash));

            //using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            //{
            //    CRC32 crc32Hasher = new CRC32();

            //    byte[] output = crc32Hasher.ComputeHash(fs);

            //    Console.WriteLine(CRC32.ToHex(output));
            //}

            TimeSpan duration = DateTime.Now - startTime;

            Console.WriteLine("multithread time : {0} m {1} s {2} ms", duration.Minutes, duration.Seconds, duration.Milliseconds);

            cancelToken.Cancel(); 
        }
    }
}
