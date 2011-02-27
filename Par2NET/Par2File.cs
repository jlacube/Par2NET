using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Par2NET.Packets;

namespace Par2NET
{
    public interface IParFile
    {
    }

    public class Par2File : IParFile
    {
        public string FileName;
        public string TargetFileName;
        public bool IsComplete;
        public uint TotalBlocks;
        public uint PresentBlocks;
        public uint MissingBlocks;
        public Dictionary<uint, DataBlock> DataBlocks;

        public Par2File()
            : this(string.Empty)
        {
            //System.IO.BinaryWriter br;
        }

        public Par2File(string filename)
        {
            FileName = filename;
            TargetFileName = filename;
            IsComplete = false;
            TotalBlocks = 0;
            PresentBlocks = 0;
            MissingBlocks = 0;
            DataBlocks = new Dictionary<uint, DataBlock>();
        }
    }
}
