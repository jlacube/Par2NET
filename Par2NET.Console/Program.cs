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
        /*
         Repair commandline /mt- /rf:"C:\Users\Jerome\Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\Tests\EntLib50.par2" /action:ParRepair
         
         * 
         * Create commandline /if:"C:\Users\Jerome\Documents\Visual Studio 2010\Projects\Par2NET\Par2NET\CreateTests\EntLib50.chm" /action:ParCreate /rbc:10 /mtcpu- /mtio-
         * 
         * 
         * /mtio- /mtcpu- /a:ParVerify /rf:\\VBOXSVR\SharedFolders\Tests\5150.Rue.Des.Ormes.LIMITED.FRENCHDVDRIP.XVID.AC3-TBoss\5150.par2
         * 
         * 
         */
        static void Main(string[] args)
        {
            Par2LibraryArguments par2args = new Par2LibraryArguments();

            if (!Parser.ParseArgumentsWithUsage(args, par2args))
                return;

            switch (par2args.action)
            {
                case ParAction.ParCreate:
                    if (par2args.inputFiles.Length == 0 || (par2args.redundancy == -1 && par2args.recoveryblockcount == -1))
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

            Par2Library library = new Par2Library(par2args.multithreadCPU, par2args.multithreadIO);

            List<string> inputFiles = new List<string>(par2args.inputFiles);
            List<string> recoveryFiles = new List<string>(par2args.recoveryFiles);

            if (string.IsNullOrEmpty(par2args.targetPath))
            {
                if (par2args.action == ParAction.ParCreate)
                    par2args.targetPath = Path.GetDirectoryName(par2args.inputFiles[0]);
                else
                    par2args.targetPath = Path.GetDirectoryName(par2args.recoveryFiles[0]);
            }

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
    }
}
