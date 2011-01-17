using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Tasks;
using Par2NET.Interfaces;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

using CommandLine;

namespace Par2NET.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            Par2LibraryArguments par2args = new Par2LibraryArguments();

            if (!Parser.ParseArgumentsWithUsage(args, par2args))
                return;

            switch (par2args.action)
            {
                case ParAction.ParCreate:
                    if (par2args.inputFiles.Length == 0)
                    {
                        Parser.ArgumentsUsage(par2args.GetType());
                        return;
                    }
                    break;
                case ParAction.ParVerify:
                case ParAction.ParRepair:
                    if (par2args.recoveryFiles.Length == 0)
                    {
                        Parser.ArgumentsUsage(par2args.GetType());
                        return;
                    }
                    break;
            }

            Par2Library library = new Par2Library(par2args.multithread);

            List<string> inputFiles = new List<string>(par2args.inputFiles);
            List<string> recoveryFiles = new List<string>(par2args.recoveryFiles);

            if (string.IsNullOrEmpty(par2args.targetPath))
                par2args.targetPath = Path.GetDirectoryName(par2args.recoveryFiles[0]);

#if TimeTrack
            DateTime startTime = DateTime.Now;
#endif
            ParResult result = library.Process(par2args);
#if TimeTrack
            DateTime endTime = DateTime.Now;
            TimeSpan duration = endTime - startTime;

            System.Console.WriteLine("Duration : {0}h{1}m{2}s{3}ms", duration.Hours, duration.Minutes, duration.Seconds, duration.Milliseconds);
#endif

            System.Console.WriteLine("Par2NET result : {0}", result);
            //recoveryFiles.Add(@"C:\USERS\Projects\__Perso\Par2NET\Par2NET\Tests\EntLib50.vol10+10.PAR2");
            //recoveryFiles.Add(@"C:\Documents and Settings\Jerome\My Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests\EntLib50.vol10+10.PAR2");
            //recoveryFiles.Add(@"C:\Users\Jerome\Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests\EntLib50.vol10+10.PAR2");

            //ParResult result = library.Process(ParVersion.Par2, inputFiles, recoveryFiles, ParAction.ParVerify, @"C:\Documents and Settings\Jerome\My Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests");
            //ParResult result = library.Process(ParVersion.Par2, inputFiles, recoveryFiles, ParAction.ParRepair, @"C:\Documents and Settings\Jerome\My Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests");
        }

        //static void Main(string[] args)
        //{
        //    Par2Library library = new Par2Library();

        //    byte[] md5hash16k;
        //    byte[] md5hash;

        //    DateTime start = DateTime.Now;
        //    //FileChecker.CheckFile(@"C:\Documents and Settings\Jerome\My Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests\EntLib50.chm", 384000, null, out md5hash16k, out md5hash);
        //    //FileChecker.CheckFile(@"Z:\en_visual_studio_2010_ultimate_x86_dvd_509116.iso", 384000, null, out md5hash16k, out md5hash);
        //    DateTime end = DateTime.Now;
        //    TimeSpan diff = end - start;
        //    System.Console.WriteLine("md5_16k:{0},md5:{1}", ToolKit.ToHex(md5hash16k), ToolKit.ToHex(md5hash));

        //    //FileChecker.CheckFile(@"C:\Users\Jerome\Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests\EntLib50_copy.chm", null, out md5hash16k, out md5hash);
        //}

        static void MainOld2(string[] args)
        {
            //using (BinaryReader br = new BinaryReader(new FileStream(@"C:\Users\Jerome\Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests\vlc-1.0.3-win32.exe.par2", FileMode.Open, FileAccess.Read, FileShare.Read)))
            //{
            //    int bytesToRead = Math.Min(1048576, (int)new FileInfo(@"C:\Users\Jerome\Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests\vlc-1.0.3-win32.exe.par2").Length);

            //    byte[] bytes = br.ReadBytes(bytesToRead);

            //    int index = ToolKit.IndexOf(bytes, Par2Library.packet_magic.magic);

            //    PACKET_HEADER header = ToolKit.ReadStruct<PACKET_HEADER>(bytes, index, Marshal.SizeOf(typeof(PACKET_HEADER)));
            //    bool equal = ToolKit.UnsafeCompare(header.type.type, Par2Library.filedescriptionpacket_type.type);

            //    if (ToolKit.UnsafeCompare(header.type.type, Par2Library.filedescriptionpacket_type.type))
            //    {
            //        FILEDESCRIPTIONPACKET packet = ToolKit.ReadPacket(bytes, index, (int)header.length);
            //    }
            //}
        }


        static void MainOld(string[] args)
        {
            int nbTasks = 30;

            //MD5

            DateTime startTime = DateTime.Now;

            List<Task> tasks = new List<Task>();

            for (int i = 0; i < nbTasks; i++)
            {
                tasks.Add(TasksHelper.VerifyMD5HashStr(@"C:\Users\Jerome\Documents\GRMWDK_EN_7600_1.iso", "8fe981a1706d43ad34bda496e6558f94"));
            }

            Task.WaitAll(tasks.ToArray());

            TimeSpan duration = DateTime.Now - startTime;
            System.Console.WriteLine("{3} MD5 Hash : {0}m{1}s{2}ms", duration.Minutes, duration.Seconds, duration.Milliseconds, nbTasks);


            //CRC32

            tasks.Clear();

            startTime = DateTime.Now;

            for (int i = 0; i < nbTasks; i++)
            {
                tasks.Add(TasksHelper.VerifyCRC32HashStr(@"C:\Users\Jerome\Documents\GRMWDK_EN_7600_1.iso", "8fe981a1706d43ad34bda496e6558f94"));
            }

            Task.WaitAll(tasks.ToArray());

            duration = DateTime.Now - startTime;
            System.Console.WriteLine("{3} CRC32 Hash : {0}m{1}s{2}ms", duration.Minutes, duration.Seconds, duration.Milliseconds, nbTasks);
        }
    }
}
