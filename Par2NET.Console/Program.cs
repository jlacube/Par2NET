using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Tasks;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

using CommandLine;

namespace Par2NET.Console
{
    //        [Argument(ArgumentType.AtMostOnce, HelpText = "Count number of lines in the input text.")]
    //        public bool lines;
    //        [Argument(ArgumentType.AtMostOnce, HelpText="Count number of words in the input text.")]
    //        public bool words;
    //        [Argument(ArgumentType.AtMostOnce, HelpText="Count number of chars in the input text.")]
    //        public bool chars;
    //        [DefaultArgument(ArgumentType.MultipleUnique, HelpText="Input files to count.")]
    //        public string[] files;

    public class ProgramArguments
    {
        [Argument(ArgumentType.AtMostOnce, HelpText = "MultiThread switch", ShortName = "mt", LongName = "multithread", DefaultValue = true)]
        public bool multithread;
        [Argument(ArgumentType.AtMostOnce, HelpText = "PAR library version", ShortName = "v", LongName = "version", DefaultValue = ParVersion.Par2)]
        public ParVersion version;
        [Argument(ArgumentType.AtMostOnce, HelpText = "PAR library action to perform", ShortName = "a", LongName = "action", DefaultValue = ParAction.ParVerify)]
        public ParAction action;
        [Argument(ArgumentType.AtMostOnce, HelpText = "Output target directory", ShortName = "o", LongName = "outputPath", DefaultValue = "")]
        public string targetPath;
        [Argument(ArgumentType.MultipleUnique, HelpText = "Specific input files", ShortName = "if", LongName = "inputFile", DefaultValue = new string[] { })]
        public string[] inputFiles;
        [Argument(ArgumentType.MultipleUnique | ArgumentType.Required, HelpText = "Specific recorvery files", ShortName = "rf", LongName = "recoveryFile", DefaultValue = new string[] { })]
        public string[] recoveryFiles;
    }

    class Program
    {
        static void Main(string[] args)
        {
            ProgramArguments _args = new ProgramArguments();

            if (!Parser.ParseArgumentsWithUsage(args, _args))
                return;

            Par2Library library = new Par2Library(_args.multithread);

            List<string> inputFiles = new List<string>(_args.inputFiles);
            List<string> recoveryFiles = new List<string>(_args.recoveryFiles);

            if (string.IsNullOrEmpty(_args.targetPath))
                _args.targetPath = Path.GetDirectoryName(_args.recoveryFiles[0]);

            DateTime startTime = DateTime.Now;
            ParResult result = library.Process(_args.version, inputFiles, recoveryFiles, _args.action, _args.targetPath);
            DateTime endTime = DateTime.Now;
            TimeSpan duration = endTime - startTime;

            System.Console.WriteLine("Duration : {0}h{1}m{2}s{3}ms", duration.Hours, duration.Minutes, duration.Seconds, duration.Milliseconds);

            System.Console.WriteLine("Par2NET result : {0}", result.ToString());
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
