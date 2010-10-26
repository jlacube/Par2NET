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

        static CRC32 crc32Hasher = new CRC32();
        static MD5 md5Hasher = MD5.Create();

        static CancellationTokenSource cancelToken = new CancellationTokenSource();

        static object syncObject = new object();

        //static int[] buffer_sizes = new int[] { 1, 2, 4, 8, 16, 32, 64, 128,256 };
        static int[] buffer_sizes = new int[] { 8, 16, 32, 64, 128 };

        static int currentIndex = buffer_sizes.Length-1;

        static void SetBiggerBufferSize()
        {
            lock (syncObject)
            {
                if (currentIndex <= buffer_sizes.Length)
                    currentIndex++;
            }
        }

        static void SetSmallerBufferSize()
        {
            lock (syncObject)
            {
                if (currentIndex > 0)
                    currentIndex--;
            }
        }

        static int GetBufferSize()
        {
            lock (syncObject)
            {
                return (buffer_sizes[currentIndex] * 1048576);
            }
        }

        static void ComputeFileChained(string filename)
        {
            Task previousTask = null;

            //long buffer_size = 512 * 1048576;
            long buffer_size = GetBufferSize();
            double lastSpeed = 0;
            double lastlastSpeed = 0;

            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int nbToRead = (int)Math.Min((buffer_size), br.BaseStream.Length - br.BaseStream.Position);

                    DateTime startSpeedTime = DateTime.Now;
                    byte[] bytes = br.ReadBytes(nbToRead);
                    TimeSpan timeToRead = DateTime.Now - startSpeedTime;

                    bool last = nbToRead < buffer_size;

                    if (timeToRead.TotalMilliseconds > 0)
                    {
                        double newSpeed = (double)nbToRead / (double)timeToRead.TotalMilliseconds;

                        if (lastSpeed > 0)
                        {
                            if (lastlastSpeed > 0)
                            {
                                // Choose a new buffer size
                                if (newSpeed > lastSpeed && newSpeed > lastlastSpeed)
                                    SetBiggerBufferSize();
                                else
                                    SetSmallerBufferSize();

                                buffer_size = GetBufferSize();
                            }

                            lastlastSpeed = lastSpeed;
                        }

                        lastSpeed = newSpeed;
                        //Console.WriteLine("buffer_size = {0}, current speed = {1}", buffer_size, lastSpeed);
                        //Console.WriteLine("current_index = {0}, current speed = {1}", currentIndex, ((lastSpeed*1000)/1048576));
                    }

                    if (previousTask == null)
                    {
                        previousTask = Task.Factory.StartNew((entry) =>
                         {
                             byte[] input = (byte[])entry;
                             if (last)
                             {
                                 crc32Hasher.TransformFinalBlock(input, 0, input.Length);
                             }
                             else
                             {
                                 crc32Hasher.TransformBlock(input, 0, input.Length, input, 0);
                             }
                         }, bytes, TaskCreationOptions.AttachedToParent);
                    }
                    else
                    {
                        previousTask = previousTask.ContinueWith((entry) =>
                               {
                                   byte[] input = bytes;
                                   if (last)
                                   {
                                       crc32Hasher.TransformFinalBlock(input, 0, input.Length);
                                   }
                                   else
                                   {
                                       crc32Hasher.TransformBlock(input, 0, input.Length, input, 0);
                                   }
                               }, TaskContinuationOptions.AttachedToParent);

                        if (last)
                            previousTask.Wait();
                    }
                }
            }
        }

        

        private static bool ToBeCleaned(TaskStatus taskStatus)
        {
 	        switch (taskStatus)
            {
                case TaskStatus.RanToCompletion:
                case TaskStatus.Faulted:
                case TaskStatus.Canceled:
                    return true;
            }

            return false;
        } 
        
        static void ComputeFile(string filename)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int nbToRead = (int)Math.Min((1048576), br.BaseStream.Length - br.BaseStream.Position);

                    byte[] bytes = br.ReadBytes(nbToRead);

                    bool last = nbToRead < 1048576;

                    Task.Factory.StartNew((entry) =>
                    {
                        //resultsCRC32.Add(crc32Hasher.ComputeHash((byte[])entry));
                        byte[] input = (byte[])entry;
                        if (last)
                        {
                            Console.WriteLine("last block");
                            crc32Hasher.TransformFinalBlock(input, 0, input.Length);
                        }
                        else
                        {
                            crc32Hasher.TransformBlock(input, 0, input.Length, input, 0);
                        }
                    }, bytes, TaskCreationOptions.AttachedToParent).Wait();

                    //Task.Factory.StartNew((entry) =>
                    //{
                        

                    //    resultsMD5.Add(md5Hasher.ComputeHash((byte[])entry));

                    //}, bytes, TaskCreationOptions.AttachedToParent);
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
                    ComputeFileChained((string)file);
                }, filename, TaskCreationOptions.LongRunning).Wait();

            //resultsCRC32.CompleteAdding();
            //resultsMD5.CompleteAdding();

            Console.WriteLine(CRC32.ToHex(crc32Hasher.Hash));

            //using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            //{
            //    CRC32 crc32Hasher = new CRC32();

            //    byte[] output = crc32Hasher.ComputeHash(fs);

            //    Console.WriteLine(CRC32.ToHex(output));
            //}

            TimeSpan duration = DateTime.Now - startTime;

            Console.WriteLine("time : {0} m {1} s {2} ms", duration.Minutes, duration.Seconds, duration.Milliseconds);
        }
    }
}
