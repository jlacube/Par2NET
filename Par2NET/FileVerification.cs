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
                contextfull = MD5.Create();
                //contextfull = new MD5Context;
            }
            else
            {
                // Compute 16k MD5 hash
                using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    MD5 md5Hasher = MD5.Create();
                    byte[] buffer16k = br.ReadBytes(16 * 1024);
                    FileDescriptionPacket.hash16k = md5Hasher.ComputeHash(buffer16k);
                }

                // Compute the fileid and store it in the verification packet.
                FileDescriptionPacket.ComputeFileId();
                FileVerificationPacket.fileid = (byte[])FileDescriptionPacket.fileid.Clone();

                // Compute full file MD5 hash & block CRC32 and MD5 hashes
                long readSize = 5 * (long)blocksize;
                long fileSize = new FileInfo(filename).Length;
                long nbSteps =  fileSize / readSize;
                long remaining = fileSize % readSize;
                uint blocknumber = 0;

                using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    MD5 md5FullHasher = MD5.Create();
                    MD5 md5Hasher = MD5.Create();
                    FastCRC32.FastCRC32 crc32 = new FastCRC32.FastCRC32(blocksize);

                    byte[] blockHash = new byte[16];
                    uint blockCRC32 = 0;
                    for (int i = 0; i < nbSteps + 1; ++i)
                    {
                        byte[] buffer = br.ReadBytes((int)(i == nbSteps ? remaining : readSize));

                        // Global MD5 hash
                        if (i == nbSteps)
                            md5FullHasher.TransformFinalBlock(buffer, 0, buffer.Length);
                        else
                            md5FullHasher.TransformBlock(buffer, 0, buffer.Length, null, 0);

                        for (uint j = 0; j < 5; ++j)
                        {
                            // Block MD5 hash & CRC32
                            uint length = (uint)blocksize;

                            if (i == nbSteps && j == 4)
                            {
                                if (remaining % (long)blocksize != 0)
                                {
                                    // We need arry padding since calculation **MUST** always be done on blocksize-length buffers
                                    byte[] smallBuffer = buffer;
                                    buffer = new byte[5*blocksize];
                                    Buffer.BlockCopy(smallBuffer, 0, buffer, 0, smallBuffer.Length);
                                }
                            }
                            
                            blockCRC32 = crc32.CRCUpdateBlock(0xFFFFFFFF, (uint)blocksize, buffer, (uint)(j * blocksize)) ^ 0xFFFFFFFF;
                            blockHash = md5Hasher.ComputeHash(buffer, (int)(j * blocksize), (int)blocksize);

                            FileVerificationPacket.SetBlockHashAndCRC((uint)(i*j), blockHash, blockCRC32);
                        }
                    }

                    FileDescriptionPacket.hashfull = md5FullHasher.Hash;
                }


                using (BinaryReader br = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    MD5 md5Hasher = MD5.Create();
                    byte[] buffer16k = br.ReadBytes(16 * 1024);
                    FileDescriptionPacket.hash16k = md5Hasher.ComputeHash(buffer16k);
                }
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
            //contextfull = MD5.Create();

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
            // Store it in the description
            this.FileDescriptionPacket.hashfull = contextfull.TransformFinalBlock(new byte[] {}, 0, 0);
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

                contextfull.TransformBlock(buffer, 0, buffer.Length, null, 0);
                //contextfull.ComputeHash(buffer, 0, (int)length);
            }
        }
    }
}
