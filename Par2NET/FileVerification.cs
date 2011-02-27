using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Packets;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

namespace Par2NET
{
    public class FileVerification : IComparable
    {
        public FileDescriptionPacket FileDescriptionPacket = null;
        public FileVerificationPacket FileVerificationPacket = null;

        public List<DataBlock> SourceBlocks = new List<DataBlock>();
        public List<DataBlock> TargetBlocks = new List<DataBlock>();

        private bool targetexists = false;        // Whether the target file exists
        private DiskFile targetfile = null;          // The final version of the file
        private DiskFile completefile = null;        // A complete version of the file
        private ulong filesize = 0;
        private ulong blockcount = 0;

        private string targetfilename = string.Empty;      // The filename of the target file
        private string parfilename = string.Empty;          // The filename of the par2 target file

        private MD5 contextfull = null;
        private FastCRC32.FastCRC32 crc32 = null;

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

        internal uint BlockCount()
        {
            return (uint)FileVerificationPacket.blockcount;
        }

        internal FileVerificationPacket GetVerificationPacket()
        {
            return FileVerificationPacket;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;

            FileVerification other = (FileVerification)obj;

            return this.TargetFileName.CompareTo(other.TargetFileName);
        }

        // Open the source file, compute the MD5 Hash of the whole file and the first
        // 16k of the file, and then compute the FileId and store the results
        // in a file description packet and a file verification packet.
        public bool Open(string filename, ulong blocksize, bool deferhashcomputation)
        {
            // Get the filename and filesize
            targetfilename = filename;
            filesize = (ulong)new FileInfo(filename).Length;

            // Work out how many blocks the file will be sliced into
            blockcount = (uint)((filesize + blocksize - 1) / blocksize);

            // Determine what filename to record in the PAR2 files
            parfilename = Path.GetFileName(filename);

            // Create the Description and Verification packets
            FileDescriptionPacket = Packets.FileDescriptionPacket.Create(parfilename, filesize);
            FileVerificationPacket = Packets.FileVerificationPacket.Create(blockcount);

            // Create the diskfile object
            targetfile = new DiskFile();

            // Open the source file
            if (!targetfile.Open(targetfilename, filesize))
                return false;

            // Do we want to defer the computation of the full file hash, and 
            // the block crc and hashes. This is only permitted if there
            // is sufficient memory available to create all recovery blocks
            // in one pass of the source files (i.e. chunksize == blocksize)
            if (deferhashcomputation)
            {
                // Initialise a buffer to read the first 16k of the source file
                uint buffersize = Math.Min((uint)filesize, 16 * 1024);

                byte[] buffer = new byte[buffersize];

                // Read the data from the file
                if (!targetfile.Read(0, buffer, buffersize))
                {
                    targetfile.Close();
                    return false;
                }

                // Compute the hash of the data read from the file
                // Store the hash in the descriptionpacket and compute the file id
                MD5 md5Hasher = MD5.Create();
                FileDescriptionPacket.hash16k = md5Hasher.ComputeHash(buffer);


                // Compute the fileid and store it in the verification packet.
                FileDescriptionPacket.ComputeFileId();
                FileVerificationPacket.fileid = (byte[])FileDescriptionPacket.fileid.Clone();

                //// Allocate an MD5 context for computing the file hash
                //// during the recovery data generation phase
                //contextfull = new MD5Context;
            }
            else
            {
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    MD5 md5Hasher = MD5.Create();
                    FileDescriptionPacket.hashfull = md5Hasher.ComputeHash(fs);
                }

                using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    MD5 md5Hasher = MD5.Create();
                    byte[] buffer16k = br.ReadBytes(16 * 1024);
                    FileDescriptionPacket.hash16k = md5Hasher.ComputeHash(buffer16k);
                }


                // Compute the fileid and store it in the verification packet.
                FileDescriptionPacket.ComputeFileId();
                FileVerificationPacket.fileid = (byte[])FileDescriptionPacket.fileid.Clone();
            }

            return true;
        }

        internal void RecordCriticalPackets(List<IPar2Packet> criticalpackets)
        {
            // Add the file description packet and file verification packet to
            // the critical packet list.
            criticalpackets.Add(FileDescriptionPacket);
            criticalpackets.Add(FileVerificationPacket);
        }

        internal void Close()
        {
            targetfile.Close();
        }

        internal void InitialiseSourceBlocks(ref List<DataBlock> sourceblocks, ulong blocksize)
        {
            crc32 = new FastCRC32.FastCRC32(blocksize);
            contextfull = MD5.Create();

            for (uint blocknum = 0; blocknum < blockcount; blocknum++)
            {
                // Configure each source block to an appropriate offset and length within the source file.
                DataBlock sourceblock = new DataBlock();
                sourceblock.SetLocation(targetfile, blocknum * blocksize);
                sourceblock.SetLength(Math.Min(blocksize, filesize - (blocknum * blocksize)));
                sourceblocks.Add(sourceblock);
            }
        }

        internal void FinishHashes()
        {
            Debug.Assert(contextfull != null);

            // Finish computation of the full file hash
            // Store it in the description packet
            this.FileDescriptionPacket.hashfull = contextfull.Hash;
        }

        internal void UpdateHashes(uint blocknumber, byte[] buffer, ulong length)
        {
            // Compute the crc and hash of the data
            unchecked
            {
                uint blockcrc = (uint)~0 ^ crc32.CRCUpdateBlock((uint)~0, (uint)length, buffer, 0);

                MD5 blockcontext = MD5.Create();
                byte[] blockhash = blockcontext.ComputeHash(buffer, 0, (int)length);

                // Store the results in the verification packet
                this.FileVerificationPacket.SetBlockHashAndCRC(blocknumber, blockhash, blockcrc);

                // Update the full file hash, but don't go beyond the end of the file
                if ((ulong)length > filesize - blocknumber * (ulong)length)
                {
                    length = (ulong)(filesize - blocknumber * (ulong)length);
                }

                Debug.Assert(contextfull != null);

                contextfull.ComputeHash(buffer, 0, (int)length);
            }
        }
    }
}
