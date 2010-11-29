using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Packets;

namespace Par2NET
{
    public class FileVerification
    {
        public FileDescriptionPacket FileDescriptionPacket = null;
        public FileVerificationPacket FileVerificationPacket = null;
        //public string TargetFileName = string.Empty;

        public List<DataBlock> SourceBlocks = new List<DataBlock>();
        public List<DataBlock> TargetBlocks = new List<DataBlock>();

        private bool targetexists = false;        // Whether the target file exists
        private DiskFile targetfile = null;          // The final version of the file
        private DiskFile completefile = null;        // A complete version of the file

        private string targetfilename = string.Empty;      // The filename of the target file

        public string TargetFileName
        {
            get { return targetfilename; }
            set { targetfilename = value; }
        }

        public void SetTargetFile(DiskFile diskfile)
        {
            targetfile = diskfile;
        }

        public DiskFile GetTargetFile()
        {
            return targetfile;
        }

        public void SetTargetExists(bool exists)
        {
            targetexists = exists;
        }

        public bool GetTargetExists()
        {
            return targetexists;
        }

        public void SetCompleteFile(DiskFile diskfile)
        {
            completefile = diskfile;
        }

        public DiskFile GetCompleteFile()
        {
            return completefile;
        }
    }
}
