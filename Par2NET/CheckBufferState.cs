using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Par2NET.Packets;
using Par2NET.Interfaces;

namespace Par2NET
{
    public class CheckBufferState
    {
        private byte[] buffer;
        private DiskFile diskFile;
        private string fileName;
        private int blockSize;
        private Dictionary<uint, FileVerificationEntry> hashFull;
        private MatchType matchType;
        private int offset;
        private int overlap;

        public CheckBufferState(byte[] buffer, DiskFile diskFile, string fileName, int blockSize, Dictionary<uint, FileVerificationEntry> hashFull, MatchType matchType, int offset, int overlap)
        {
            this.buffer = buffer;
            this.diskFile = diskFile;
            this.fileName = fileName;
            this.blockSize = blockSize;
            this.hashFull = hashFull;
            this.matchType = matchType;
            this.offset = offset;
            this.overlap = overlap;
        }

        public byte[] Buffer
        {
            get { return buffer; }
        }

        public DiskFile DiskFile
        {
            get { return diskFile; }
        }

        public string FileName
        {
            get { return fileName; }
        }

        public int BlockSize
        {
            get { return blockSize; }
        }

        public  Dictionary<uint, FileVerificationEntry> HashFull
        {
            get { return hashFull; }
        }

        public MatchType MatchType
        {
            get { return matchType; }
            set { matchType = value; }
        }

        public int Offset
        {
            get { return offset; }
        }

        public int Overlap
        {
            get { return overlap; }
        }
    }
}
