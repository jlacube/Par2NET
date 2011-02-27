using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommandLine;

namespace Par2NET.Interfaces
{
    public class Par2LibraryArguments
    {
        [Argument(ArgumentType.AtMostOnce, HelpText = "MultiThread CPU switch", ShortName = "mtcpu", LongName = "multithreadCPU", DefaultValue = true)]
        public bool multithreadCPU;
        [Argument(ArgumentType.AtMostOnce, HelpText = "MultiThread IO switch", ShortName = "mtio", LongName = "multithreadIO", DefaultValue = false)]
        public bool multithreadIO;
        [Argument(ArgumentType.AtMostOnce, HelpText = "PAR library version", ShortName = "v", LongName = "version", DefaultValue = ParVersion.Par2)]
        public ParVersion version;
        [Argument(ArgumentType.AtMostOnce, HelpText = "PAR library action to perform", ShortName = "a", LongName = "action", DefaultValue = ParAction.ParVerify)]
        public ParAction action;
        [Argument(ArgumentType.AtMostOnce, HelpText = "Output target directory", ShortName = "o", LongName = "outputPath", DefaultValue = "")]
        public string targetPath;
        [Argument(ArgumentType.MultipleUnique, HelpText = "Specific input files", ShortName = "if", LongName = "inputFile", DefaultValue = new string[] { })]
        public string[] inputFiles; // ArgumentType.Required for create
        [Argument(ArgumentType.MultipleUnique, HelpText = "Specific recorvery files", ShortName = "rf", LongName = "recoveryFile", DefaultValue = new string[] { })]
        public string[] recoveryFiles; // ArgumentType.Required for repair / verify
        [Argument(ArgumentType.AtMostOnce, HelpText = "Par2 base filename", ShortName = "pbf", LongName = "par2BaseFileName", DefaultValue = "")]
        public string par2filename;
        [Argument(ArgumentType.AtMostOnce, HelpText = "Blocksize for recovery", ShortName = "bs", LongName = "blockSize", DefaultValue = 384000)]
        public int blocksize;
        [Argument(ArgumentType.AtMostOnce, HelpText = "RecoveryFile count", ShortName = "rfc", LongName = "recoveryFileCount", DefaultValue = 0)]
        public int recoveryfilecount;
        [Argument(ArgumentType.AtMostOnce, HelpText = "Redundancy in percent", ShortName = "r", LongName = "redundancy", DefaultValue = -1)]
        public int redundancy; // ArgumentType.Required for create (paired with recoverycountblock)
        [Argument(ArgumentType.AtMostOnce, HelpText = "RecoveryBlock count", ShortName = "rbc", LongName = "recovreyBlockCount", DefaultValue = -1)]
        public int recoveryblockcount; // ArgumentType.Required for create (paired with redundancy)
        [Argument(ArgumentType.AtMostOnce, HelpText = "RecoveryFile scheme", ShortName = "rfs", LongName = "recoveryFileScheme", DefaultValue = ParScheme.Variable)]
        public ParScheme recoveryfilescheme;
    }
}
