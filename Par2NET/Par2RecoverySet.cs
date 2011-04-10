using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Packets;
using Par2NET.Interfaces;
using System.IO;
using FastGaloisFields;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Par2NET
{
    public class Par2RecoverySet
    {
        private bool multithreadCPU = false;
        private bool multithreadIO = false;

        public CreatorPacket CreatorPacket = null;
        public MainPacket MainPacket = null;
        public List<RecoveryPacket> RecoveryPackets = new List<RecoveryPacket>();
        public Dictionary<string, FileVerification> FileSets = new Dictionary<string, FileVerification>();
        public List<FileVerification> SourceFiles = new List<FileVerification>();

        public uint completefilecount = 0;       // How many files are fully verified
        public uint renamedfilecount = 0;        // How many files are verified but have the wrong name
        public uint damagedfilecount = 0;        // How many files exist but are damaged
        public uint missingfilecount = 0;        // How many files are completely missing

        public ulong chunksize = 0;              // How much of a block can be processed.
        public uint sourceblockcount = 0;        // The total number of blocks
        public uint availableblockcount = 0;     // How many undamaged blocks have been found
        public uint missingblockcount = 0;       // How many blocks are missing

        public ulong totaldata = 0;              // Total amount of data to be processed.

        public byte[] inputbuffer = null;             // Buffer for reading DataBlocks (chunksize)
        public byte[] outputbuffer = null;            // Buffer for writing DataBlocks (chunksize * missingblockcount)

        private List<DataBlock> inputblocks = new List<DataBlock>();             // Which DataBlocks will be read from disk
        private List<DataBlock> copyblocks = new List<DataBlock>();              // Which DataBlocks will copied back to disk
        private List<DataBlock> outputblocks = new List<DataBlock>();            // Which DataBlocks have to calculated using RS

        private List<FileVerification> verifylist = new List<FileVerification>();

        private Dictionary<uint, FileVerificationEntry> verificationhashtable = new Dictionary<uint, FileVerificationEntry>();
        private Dictionary<uint, FileVerificationEntry> verificationhashtablefull = new Dictionary<uint, FileVerificationEntry>();
        private List<FileVerificationEntry> expectedblocklist = new List<FileVerificationEntry>();
        private int expectedblockindex = 0;

        static ReedSolomonGalois16     _rs = new ReedSolomonGalois16();                      // The Reed Solomon matrix.
        ReedSolomonGalois16 rs = Par2RecoverySet._rs;

        private static object _syncObject = new object();
        public int recoveryfilecount = 0;
        public int redundancy = 0;
        public int recoveryblockcount;
        public ParScheme recoveryfilescheme;
        private bool deferhashcomputation = false;
        private ulong largestfilesize = 0;
        private DiskFile[] recoveryfiles = null;

        public List<IPar2Packet> criticalpackets = new List<IPar2Packet>();

        private List<CriticalPacketEntry> criticalpacketentries = new List<CriticalPacketEntry>();

        MainPacket mainpacket = null;
        CreatorPacket creatorpacket = null;

        public Par2RecoverySet(bool _multithreadCPU, bool _multithreadIO)
            : this (_multithreadCPU, _multithreadIO, null)
        {
        }

        public Par2RecoverySet(bool _multithreadCPU, bool _multithreadIO, Par2LibraryArguments args)
        {
            multithreadCPU = _multithreadCPU;
            multithreadIO = _multithreadIO;

            if (args != null)
            {
                /* Par2Creator section */
                CreateMainPacket(args);
                AddMainPacket(mainpacket);
                redundancy = args.redundancy;
                recoveryfilecount = args.recoveryfilecount;
                recoveryblockcount = args.recoveryblockcount;
                recoveryfilescheme = args.recoveryfilescheme;
            }
        }

        private FileVerification FileVerification(string fileid)
        {
            if (!FileSets.Keys.Contains(fileid))
                FileSets.Add(fileid, new FileVerification());

            return FileSets[fileid];
        }

        #region Par2Creator methods

        private long FileSize(string file)
        {
            if (!File.Exists(file))
                return 0;

            return (new FileInfo(file)).Length;
        }

        // Compute block size from block count or vice versa depending on which was
        // specified on the command line
        public bool ComputeBlockSizeAndBlockCount(ref List<string> inputFiles)
        {
            // Determine blocksize from sourceblockcount or vice-versa
            int blocksize = (int)MainPacket.blocksize;

            foreach (string file in inputFiles)
            {
                if (!File.Exists(file))
                    continue;

                ulong filesize = (ulong)FileSize(file);
                if (filesize > largestfilesize)
                    largestfilesize = filesize;
            }

            if (blocksize > 0)
            {
                long count = 0;

                foreach (string file in inputFiles)
                {
                    count += (FileSize(file) + blocksize - 1) / blocksize;
                }

                if (count > 32768)
                {
                    Console.WriteLine("Block size is too small. It would require {0} blocks.", count);
                    return false;
                }

                sourceblockcount = (uint)count;
            }
            else if (sourceblockcount > 0)
            {
                if (sourceblockcount < inputFiles.Count)
                {
                    // The block count cannot be less that the number of files.

                    Console.WriteLine("Block count is too small.");
                    return false;
                }
                else if (sourceblockcount == inputFiles.Count)
                {
                    // If the block count is the same as the number of files, then the block
                    // size is the size of the largest file (rounded up to a multiple of 4).

                    long largestsourcesize = 0;

                    foreach (string file in inputFiles)
                    {
                        if (largestsourcesize < FileSize(file))
                        {
                            largestsourcesize = FileSize(file);
                        }
                    }

                    blocksize = (int)(largestsourcesize + 3) & ~3;
                }
                else
                {
                    long totalsize = 0;

                    foreach (string file in inputFiles)
                    {
                        totalsize += (FileSize(file) + 3) / 4;
                    }

                    if (sourceblockcount > totalsize)
                    {
                        sourceblockcount = (uint)totalsize;
                        blocksize = 4;
                    }
                    else
                    {
                        // Absolute lower bound and upper bound on the source block size that will
                        // result in the requested source block count.
                        long lowerBound = totalsize / sourceblockcount;
                        long upperBound = (totalsize + sourceblockcount - inputFiles.Count - 1) / (sourceblockcount - inputFiles.Count);

                        long bestsize = lowerBound;
                        long bestdistance = 1000000;
                        long bestcount = 0;

                        long count;
                        long size;

                        // Work out how many blocks you get for the lower bound block size
                        {
                            size = lowerBound;

                            count = 0;
                            foreach (string file in inputFiles)
                            {
                                count += ((FileSize(file) + 3) / 4 + size - 1) / size;
                            }

                            if (bestdistance > (count > sourceblockcount ? count - sourceblockcount : sourceblockcount - count))
                            {
                                bestdistance = (count > sourceblockcount ? count - sourceblockcount : sourceblockcount - count);
                                bestcount = count;
                                bestsize = size;
                            }
                        }

                        // Work out how many blocks you get for the upper bound block size
                        {
                            size = upperBound;

                            count = 0;
                            foreach (string file in inputFiles)
                            {
                                count += ((FileSize(file) + 3) / 4 + size - 1) / size;
                            }

                            if (bestdistance > (count > sourceblockcount ? count - sourceblockcount : sourceblockcount - count))
                            {
                                bestdistance = (count > sourceblockcount ? count - sourceblockcount : sourceblockcount - count);
                                bestcount = count;
                                bestsize = size;
                            }
                        }

                        // Use binary search to find best block size
                        while (lowerBound + 1 < upperBound)
                        {
                            size = (lowerBound + upperBound) / 2;

                            count = 0;
                            foreach (string file in inputFiles)
                            {
                                count += ((FileSize(file) + 3) / 4 + size - 1) / size;
                            }

                            if (bestdistance > (count > sourceblockcount ? count - sourceblockcount : sourceblockcount - count))
                            {
                                bestdistance = (count > sourceblockcount ? count - sourceblockcount : sourceblockcount - count);
                                bestcount = count;
                                bestsize = size;
                            }

                            if (count < sourceblockcount)
                            {
                                upperBound = size;
                            }
                            else if (count > sourceblockcount)
                            {
                                lowerBound = size;
                            }
                            else
                            {
                                upperBound = size;
                            }
                        }

                        size = bestsize;
                        count = bestcount;

                        if (count > 32768)
                        {
                            Console.WriteLine("Error calculating block size.");
                            return false;
                        }

                        sourceblockcount = (uint)count;
                        blocksize = (int)size * 4;
                    }
                }
            }

            return true;
        }

        // Determine how many recovery blocks to create based on the source block
        // count and the requested level of redundancy.
        public bool ComputeRecoveryBlockCount(int redundancy)
        {
            // Determine recoveryblockcount
            recoveryblockcount = (int)(sourceblockcount * redundancy + 50) / 100;

            // Force valid values if necessary
            if (recoveryblockcount == 0 && redundancy > 0)
                recoveryblockcount = 1;

            if (recoveryblockcount > 65536)
            {
                Console.WriteLine("Too many recovery blocks requested.");
                return false;
            }

            //// Check that the last recovery block number would not be too large
            //if (firstrecoveryblock + recoveryblockcount >= 65536)
            //{
            //    cerr << "First recovery block number is too high." << endl;
            //    return false;
            //}

            return true;
        }

        // Determine how much recovery data can be computed on one pass
        public bool CalculateProcessBlockSize(ulong memorylimit)
        {
            // Are we computing any recovery blocks
            if (recoveryblockcount == 0)
            {
                deferhashcomputation = false;
            }
            else
            {
                // Would single pass processing use too much memory
                if (MainPacket.blocksize * (ulong)recoveryblockcount > memorylimit && this.largestfilesize > MainPacket.blocksize)
                {
                    unchecked
                    {
                        // Pick a size that is small enough
                        chunksize = (ulong)~3 & (memorylimit / (ulong)recoveryblockcount);
                    }

                    deferhashcomputation = true;
                }
                else
                {
                    chunksize = MainPacket.blocksize;

                    deferhashcomputation = true;
                }
            }

            return true;
        }

        // Determine how many recovery files to create.
        public bool ComputeRecoveryFileCount()
        {
            // Are we computing any recovery blocks
            if (recoveryblockcount == 0)
            {
                recoveryfilecount = 0;
                return true;
            }

            switch (recoveryfilescheme)
            {
                case ParScheme.Unknown:
                    {
                        Debug.Assert(false);
                        return false;
                    }
                case ParScheme.Variable:
                case ParScheme.Uniform:
                    {
                        if (recoveryfilecount == 0)
                        {
                            // If none specified then then filecount is roughly log2(blockcount)
                            // This prevents you getting excessively large numbers of files
                            // when the block count is high and also allows the files to have
                            // sizes which vary exponentially.

                            for (int blocks = recoveryblockcount; blocks > 0; blocks >>= 1)
                            {
                                recoveryfilecount++;
                            }
                        }

                        if (recoveryfilecount > recoveryblockcount)
                        {
                            // You cannot have move recovery files that there are recovery blocks
                            // to put in them.
                            Console.WriteLine("Too many recovery files specified.");
                            return false;
                        }
                    }
                    break;

                case ParScheme.Limited:
                    {
                        // No recovery file will contain more recovery blocks than would
                        // be required to reconstruct the largest source file if it
                        // were missing. Other recovery files will have recovery blocks
                        // distributed in an exponential scheme.

                        uint largest = (uint)((largestfilesize + MainPacket.blocksize - 1) / MainPacket.blocksize);
                        uint whole = (uint)recoveryblockcount / largest;
                        whole = (whole >= 1) ? whole - 1 : 0;

                        uint extra = (uint)recoveryblockcount - whole * largest;
                        recoveryfilecount = (int)whole;
                        for (uint blocks = extra; blocks > 0; blocks >>= 1)
                        {
                            recoveryfilecount++;
                        }
                    }
                    break;
            }

            return true;
        }

        // Open all of the source files, compute the Hashes and CRC values, and store
        // the results in the file verification and file description packets.
        public bool OpenSourceFiles(ref List<string> inputFiles)
        {
            foreach (string file in inputFiles)
            {
                FileVerification fileVer = new FileVerification();

                if (!fileVer.Open(file, MainPacket.blocksize, deferhashcomputation))
                {
                    fileVer = null;
                    return false;
                }

                fileVer.RecordCriticalPackets(criticalpackets);

                SourceFiles.Add(fileVer);

                if (!mainpacket.fileids.Contains(fileVer.FileDescriptionPacket.fileid))
                    mainpacket.fileids.Add(fileVer.FileDescriptionPacket.fileid);

                fileVer.Close();
            }

            // Compute setid MD5 hash
            MD5 setidcontext = MD5.Create();
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(mainpacket.blocksize);
                    bw.Write(mainpacket.recoverablefilecount);

                    foreach (byte[] fileid in mainpacket.fileids)
                    {
                        bw.Write(fileid);
                    }
                    
                    byte[] buffer = ms.ToArray();

                    mainpacket.header.setid = setidcontext.ComputeHash(buffer);
                }
            }

            // Compute mainpacket hash MD5 hash
            MD5 hashcontext = MD5.Create();
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(mainpacket.header.setid);
                    bw.Write(mainpacket.header.type);
                    bw.Write(mainpacket.blocksize);
                    bw.Write(mainpacket.recoverablefilecount);

                    foreach (byte[] fileid in mainpacket.fileids)
                    {
                        bw.Write(fileid);
                    }

                    byte[] buffer = ms.ToArray();

                    mainpacket.header.hash = setidcontext.ComputeHash(buffer);
                }
            }

            return true;
        }

        // Create the main packet and determine the setid to use with all packets
        private bool CreateMainPacket(Par2LibraryArguments args)
        {
            // Construct the main packet from the list of source files and the block size.
            // Create the packet (sourcefiles will get sorted into FileId order).
            mainpacket = MainPacket.Create(args);

            // Add the main packet to the list of critical packets.
            //criticalpackets.Add(mainpacket);
            
            return true;
        }

        // Create the creator packet.
        public bool CreateCreatorPacket()
        {
            // Construct & Create the creator packet
            creatorpacket = CreatorPacket.Create(MainPacket.header.setid);

            // Create the packet
            return true;
        }

        // Initialise all of the source blocks ready to start reading data from the source files.
        internal bool CreateSourceBlocks()
        {
            // Allocate the array of source blocks
            this.inputblocks = new List<DataBlock>();

            foreach (FileVerification fileVer in SourceFiles)
            {
                // Allocate the appopriate number of source blocks to each source file.
                // sourceblock will be advanced.
                fileVer.InitialiseSourceBlocks(ref inputblocks, MainPacket.blocksize);
            }

            return true;
        }

        // Create all of the output files and allocate all packets to appropriate file offets.
        internal bool InitialiseOutputFiles(string par2filename)
        {
            // Choose filenames and decide which recovery blocks to place in each file          
            Par2FileAllocation[] fileallocations = new Par2FileAllocation[recoveryfilecount + 1];

            uint exponent = 0; //firstrecoveryblock originally
            uint filenumber = 0;

            if (recoveryfilecount > 0)
            {
                switch (recoveryfilescheme)
                {
                    case ParScheme.Unknown:
                        // Should never happened
                        Debug.Assert(false);
                        return false;

                    case ParScheme.Uniform:
                        // Files will have roughly the same number of recovery blocks each.

                        uint uibase = (uint)(recoveryblockcount / recoveryfilecount);
                        uint uiremainder = (uint)(recoveryblockcount % recoveryfilecount);

                        for (filenumber = 0; filenumber < recoveryfilecount; filenumber++)
                        {
                            fileallocations[filenumber] = new Par2FileAllocation();
                            fileallocations[filenumber].exponent = exponent;
                            fileallocations[filenumber].count = filenumber < uiremainder ? uibase + 1 : uibase;
                            exponent += fileallocations[filenumber].count;
                        }
                        break;

                    case ParScheme.Variable:
                        // Files will have recovery blocks allocated in an exponential fashion.

                        // Work out how many blocks to place in the smallest file
                        uint lowblockcount = 1;
                        uint maxrecoveryblocks = (uint)((1 << recoveryfilecount) - 1);
                        while (maxrecoveryblocks < recoveryblockcount)
                        {
                            lowblockcount <<= 1;
                            maxrecoveryblocks <<= 1;
                        }

                        // Allocate the blocks.
                        uint blocks = (uint)recoveryblockcount;
                        for (filenumber = 0; filenumber < recoveryfilecount; filenumber++)
                        {
                            uint number = Math.Min(lowblockcount, blocks);
                            fileallocations[filenumber] = new Par2FileAllocation();
                            fileallocations[filenumber].exponent = exponent;
                            fileallocations[filenumber].count = number;
                            exponent += number;
                            blocks -= number;
                            lowblockcount <<= 1;
                        }
                        break;

                    case ParScheme.Limited:
                        // Files will be allocated in an exponential fashion but the
                        // Maximum file size will be limited.

                        uint largest = (uint)((largestfilesize + MainPacket.blocksize - 1) / MainPacket.blocksize);
                        filenumber = (uint)recoveryfilecount;
                        blocks = (uint)recoveryblockcount;

                        //exponent = firstrecoveryblock + recoveryblockcount;
                        exponent = (uint)(0 + recoveryblockcount);

                        // Allocate uniformly at the top
                        while (blocks >= 2 * largest && filenumber > 0)
                        {
                            filenumber--;
                            exponent -= largest;
                            blocks -= largest;

                            fileallocations[filenumber] = new Par2FileAllocation();
                            fileallocations[filenumber].exponent = exponent;
                            fileallocations[filenumber].count = largest;
                        }
                        Debug.Assert(blocks > 0 && filenumber > 0);

                        //exponent = firstrecoveryblock;
                        exponent = 0;
                        uint count = 1;
                        uint files = filenumber;

                        // Allocate exponentially at the bottom
                        for (filenumber = 0; filenumber < files; filenumber++)
                        {
                            uint number = Math.Min(count, blocks);
                            fileallocations[filenumber] = new Par2FileAllocation();
                            fileallocations[filenumber].exponent = exponent;
                            fileallocations[filenumber].count = number;

                            exponent += number;
                            blocks -= number;
                            count <<= 1;
                        }
                        break;
                }
            }


            // There will be an extra file with no recovery blocks.
            fileallocations[recoveryfilecount] = new Par2FileAllocation();
            fileallocations[recoveryfilecount].exponent = exponent;
            fileallocations[recoveryfilecount].count = 0;

            // Determine the format to use for filenames of recovery files

            uint limitLow = 0;
            uint limitCount = 0;
            for (filenumber = 0; filenumber <= recoveryfilecount; filenumber++)
            {
                if (limitLow < fileallocations[filenumber].exponent)
                {
                    limitLow = fileallocations[filenumber].exponent;
                }
                if (limitCount < fileallocations[filenumber].count)
                {
                    limitCount = fileallocations[filenumber].count;
                }
            }

            uint digitsLow = 1;
            for (uint t = limitLow; t >= 10; t /= 10)
            {
                digitsLow++;
            }

            uint digitsCount = 1;
            for (uint t = limitCount; t >= 10; t /= 10)
            {
                digitsCount++;
            }

            //string filenameformat = string.Format("%%s.vol%%0%dd+%%0%dd.par2", digitsLow, digitsCount);
            //string filenameformat = string.Format("{{0}}.vol{{1:{0}}}+{{2:{1}}}.par2", digitsLow, digitsCount);
            string filenameformat = string.Format("{{0}}.vol{{1:00}}+{{2:00}}.par2", digitsLow, digitsCount);

            // Set the filenames
            for (filenumber = 0; filenumber < recoveryfilecount; filenumber++)
            {
                fileallocations[filenumber].filename = string.Format(filenameformat, par2filename, fileallocations[filenumber].exponent, fileallocations[filenumber].count);
            }
            fileallocations[recoveryfilecount].filename = par2filename + ".par2";


            // Allocate the recovery files
            this.recoveryfiles = new DiskFile[recoveryfilecount + 1];

            // Allocate packets to the output files
            {
                byte[] setid = mainpacket.header.setid;

                int fai = 0;

                // For each recovery file:
                for (int i = 0; i < recoveryfilecount + 1; ++i)
                {
                    recoveryfiles[i] = new DiskFile();

                    // How many recovery blocks in this file
                    uint count = fileallocations[fai].count;

                    // start at the beginning of the recovery file
                    ulong offset = 0;

                    if (count == 0)
                    {
                        // Write one set of critical packets
                        foreach (CriticalPacket criticalPacket in criticalpackets)
                        {
                            criticalpacketentries.Add(new CriticalPacketEntry(recoveryfiles[i], offset, criticalPacket));
                            offset += criticalPacket.PacketLength();
                        }
                    }
                    else
                    {
                        // How many copies of each critical packet
                        uint copies = 0;
                        for (uint t = count; t > 0; t >>= 1)
                        {
                            copies++;
                        }

                        // Get ready to iterate through the critical packets
                        uint packetCount = 0;
                        //list<CriticalPacket*>::const_iterator nextCriticalPacket = criticalpackets.end();

                        // What is the first exponent
                        exponent = fileallocations[fai].exponent;

                        // Start allocating the recovery packets
                        uint limit = exponent + count;

                        while (exponent < limit)
                        {
                            // Add the next recovery packet
                            RecoveryPacket recoverypacket = RecoveryPacket.Create(recoveryfiles[i], offset, MainPacket.blocksize, exponent, setid);

                            offset += recoverypacket.PacketLength();
                            //++recoverypacket;
                            RecoveryPackets.Add(recoverypacket);
                            ++exponent;

                            // Add some critical packets
                            packetCount += (uint)(copies * criticalpackets.Count);
                            int cpi = 0;
                            while (packetCount >= count)
                            {
                                criticalpacketentries.Add(new CriticalPacketEntry(recoveryfiles[i], offset, (CriticalPacket)criticalpackets[cpi]));
                                //if (nextCriticalPacket == criticalpackets.end()) nextCriticalPacket = criticalpackets.begin();

                                offset += criticalpackets[cpi].PacketLength();
                                //++nextCriticalPacket;
                                ++cpi;

                                packetCount -= count;
                            }
                        }
                    }

                    // Add one copy of the creator packet
                    criticalpacketentries.Add(new CriticalPacketEntry(recoveryfiles[i], offset, creatorpacket));

                    offset += creatorpacket.PacketLength();

                    // Create the file on disk and make it the required size
                    if (!recoveryfiles[i].Create(fileallocations[fai].filename, offset))
                        return false;

                    ++fai;
                }
            }

            return true;
        }

        // Allocate memory buffers for reading and writing data to disk.
        internal bool AllocateBuffers()
        {
            inputbuffer = new byte[chunksize];
            outputbuffer = new byte[chunksize * (ulong)recoveryblockcount];

            if (inputbuffer == null || outputbuffer == null)
            {
                Console.Error.WriteLine("Could not allocate buffer memory.");
                return false;
            }

            return true;
        }

        // Compute the Reed Solomon matrix
        internal bool ComputeRSMatrix()
        {
            // Set the number of input blocks
            if (!rs.SetInput(sourceblockcount))
                return false;

            // Set the number of output blocks to be created
            //if (!rs.SetOutput(false, (ushort)firstrecoveryblock, (ushort)firstrecoveryblock + (ushort)(recoveryblockcount - 1)))
            if (!rs.SetOutput(false, (ushort)0, (ushort)(recoveryblockcount - 1)))
                return false;

            // Compute the RS matrix
            if (!rs.Compute())
                return false;

            return true;
        }

        // Read source data, process it through the RS matrix and write it to disk.
        internal bool ProcessData(ulong blockoffset, ulong blocklength)
        {
            // Clear the output buffer
            outputbuffer = new byte[chunksize * (ulong)recoveryblockcount];

            // If we have defered computation of the file hash and block crc and hashes
            // sourcefile and sourceindex will be used to update them during
            // the main recovery block computation
            uint sourceindex = 0;
            int sourcefileindex = 0;

            //DataBlock sourceblock;
            uint inputblock = 0;

            DiskFile lastopenfile = null;

            // For each input block
            foreach (DataBlock sourceblock in inputblocks)
            {
                FileVerification sourcefile = SourceFiles[sourcefileindex];

                // Are we reading from a new file?
                if (lastopenfile != sourceblock.GetDiskFile())
                {
                    // Close the last file
                    if (lastopenfile != null)
                    {
                        lastopenfile.Close();
                    }

                    // Open the new file
                    lastopenfile = sourceblock.GetDiskFile();
                    if (!lastopenfile.Open())
                    {
                        return false;
                    }
                }
                // Read data from the current input block
                if (!sourceblock.ReadData(blockoffset, (uint)blocklength, inputbuffer))
                    return false;

                if (deferhashcomputation)
                {
                    Debug.Assert(blockoffset == 0 && blocklength == MainPacket.blocksize);
                    //Debug.Assert(sourcefileindex < SourceFiles.Count - 1); //TODO: Check Assert against c++ lib

                    sourcefile.UpdateHashes(sourceindex, inputbuffer, blocklength);
                }

                //LogArrayToFile<byte>(@"outputbuffer.before.createparityblocks.log", outputbuffer);

                // Function that does the subtask in multiple threads if appropriate.
                if (!CreateParityBlocks(blocklength, inputblock))
                    return false;

                //LogArrayToFile<byte>(@"outputbuffer.after.createparityblocks.log", outputbuffer);

                // Work out which source file the next block belongs to
                if (++sourceindex >= sourcefile.BlockCount())
                {
                    sourceindex = 0;
                    ++sourcefileindex;
                }

                inputblock++;
            }

            // Close the last file
            if (lastopenfile != null)
            {
                lastopenfile.Close();
            }

            //if (noiselevel > CommandLine::nlQuiet)
            //  cout << "Writing recovery packets\r";

            // For each output block
            for (uint outputblock = 0; outputblock < recoveryblockcount; outputblock++)
            {
                // Select the appropriate part of the output buffer
                // Write the data to the recovery packet
                if (!RecoveryPackets[(int)outputblock].WriteData(blockoffset, blocklength, outputbuffer, chunksize * outputblock))
                    return false;
            }

            //if (noiselevel > CommandLine::nlQuiet)
            //  cout << "Wrote " << recoveryblockcount * blocklength << " bytes to disk" << endl;

            return true;
        }

        //public static void LogArrayToFile<T>(string filename, T[] array)
        //{
        //    if (array == null)
        //        return;

        //    using (StreamWriter sw = new StreamWriter(new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)))
        //    {
        //        for (int i = 0; i < array.Length; ++i)
        //        {
        //            sw.WriteLine("index={0},data={1}", i, array[i].ToString());
        //        }
        //    }
        //}

        private bool CreateParityBlocks(ulong blocklength, uint inputindex)
        {
            // Used from within ProcessData.

            if (recoveryblockcount == 0)
                return true;		// Nothing to do, actually

            bool rv = true;		// Optimistic default

            int lNumThreads = Environment.ProcessorCount;

            // First, establish the number of blocks to be processed by each thread. Of course the last
            // one started might get some less...
            int lNumBlocksPerThread = (recoveryblockcount - 1) / lNumThreads + 1;		// Round up
            //int lNumBlocksPerThread = 1;
            uint lCurrentStartBlockNo = 0;

            List<Task> tasks = new List<Task>();

            while (lCurrentStartBlockNo < recoveryblockcount)
            {
                uint lNextStartBlockNo = (uint)(lCurrentStartBlockNo + lNumBlocksPerThread);
                if (lNextStartBlockNo > recoveryblockcount)
                    lNextStartBlockNo = (uint)recoveryblockcount;		// Constraint

                if (multithreadCPU)
                {
                    //MT : OK
                    object[] args = new object[] { blocklength, inputindex, lCurrentStartBlockNo, lNextStartBlockNo };
                    tasks.Add(Task.Factory.StartNew((a) =>
                    {
                        object[] list = (object[])a;
                        ulong bl = (ulong)list[0];
                        uint ii = (uint)list[1];
                        uint csb = (uint)list[2];
                        uint nsb = (uint)list[3];
                        CreateParityBlockRange((uint)bl, ii, csb, nsb);
                    }, args/*, TaskCreationOptions.LongRunning*/));
                }
                else
                {
                    //ST
                    CreateParityBlockRange((uint)blocklength, inputindex, lCurrentStartBlockNo, lNextStartBlockNo);
                }

                lCurrentStartBlockNo = lNextStartBlockNo;
            }

            Task.WaitAll(tasks.ToArray());

            return rv;
        }

        //-----------------------------------------------------------------------------
        private void CreateParityBlockRange(uint blocklength, uint inputindex, uint aStartBlockNo, uint aEndBlockNo)
        {
            // This function runs in multiple threads.
            // For each output block
            for (uint outputindex = aStartBlockNo; outputindex < aEndBlockNo; outputindex++)
            {
                // Select the appropriate part of the output buffer
                //byte[] outbuf = new byte[blocklength];

                //Buffer.BlockCopy(outputbuffer, (int)(chunksize * outputindex), outbuf, 0, outbuf.Length);

                // Process the data
                rs.Process(blocklength, inputindex, inputbuffer, outputindex, outputbuffer, (int)(chunksize * outputindex), blocklength);

                //Buffer.BlockCopy(outbuf, 0, outputbuffer, (int)(chunksize * outputindex), outbuf.Length);
            }
        }

//        private void CreateParityBlockRange_orig(uint blocklength, uint inputindex, uint aStartBlockNo, uint aEndBlockNo)
//        {
//            // This function runs in multiple threads.
//            // For each output block
//            for (uint outputindex = aStartBlockNo; outputindex < aEndBlockNo; outputindex++)
//            {
//                // Select the appropriate part of the output buffer
//                byte[] outbuf = new byte[blocklength];

//                Buffer.BlockCopy(outputbuffer, (int)(chunksize * outputindex), outbuf, 0, outbuf.Length);

//                // Process the data
//                rs.Process_orig(blocklength, inputindex, inputbuffer, outputindex, outbuf);

//#if TRACE
//                ToolKit.LogArrayToFile<byte>("outbuf." + inputindex + ".log", outbuf);
//#endif
//                Buffer.BlockCopy(outbuf, 0, outputbuffer, (int)(chunksize * outputindex), outbuf.Length);
//            }
//        }

        // Finish computation of the recovery packets and write the headers to disk.
        internal bool WriteRecoveryPacketHeaders()
        {
            foreach (RecoveryPacket recoverypacket in this.RecoveryPackets)
            {
                if (!recoverypacket.WriteHeader())
                    return false;
            }

            return true;
        }

        internal bool FinishFileHashComputation()
        {
            // If we defered the computation of the full file hash, then we finish it now
            if (deferhashcomputation)
            {
                // For each source file
                foreach (FileVerification fileVer in SourceFiles)
                {
                    fileVer.FinishHashes();
                }
            }

            return true;
        }

        // Fill in all remaining details in the critical packets.
        internal bool FinishCriticalPackets()
        {
            // Get the setid from the main packet
            byte[] setid = MainPacket.header.setid;

            foreach (IPar2Packet criticalpacket in criticalpackets)
            {
                // Store the setid in each of the critical packets
                // and compute the packet_hash of each one.
                criticalpacket.FinishPacket(setid);
            }

            return true;
        }

        // Write all other critical packets to disk.
        internal bool WriteCriticalPackets()
        {
            foreach (CriticalPacketEntry packetentry in criticalpacketentries)
            {
                if (!packetentry.WritePacket())
                    return false;
            }

            return true;
        }

        // Close all files.
        internal bool CloseFiles()
        {
            try
            {
                foreach (DiskFile file in recoveryfiles)
                {
                    file.Close();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        public bool CheckPacketsConsistency()
        {
            if (MainPacket == null)
                return false;

            // Remove bad recovery packets
            foreach (RecoveryPacket badRecoveryPacket in (from packet in RecoveryPackets
                                                          where (packet.header.length - (ulong)packet.GetSize()) != MainPacket.blocksize
                                                          select packet))
            {
                RecoveryPackets.Remove(badRecoveryPacket);
            }

            ulong block_size = MainPacket.blocksize;
            List<string> keysToBeRemoved = new List<string>();

            foreach (string key in FileSets.Keys)
            {
                FileVerification fileVer = FileSets[key];

                if (fileVer.FileDescriptionPacket == null)
                    keysToBeRemoved.Add(key);

                if (fileVer.FileVerificationPacket == null)
                    continue;

                ulong file_size = fileVer.FileDescriptionPacket.length;
                ulong block_count = fileVer.FileVerificationPacket.blockcount;

                if ((file_size + block_size - 1) / block_size != block_count)
                    keysToBeRemoved.Add(key);
            }

            return true;
        }

        internal void AddCreatorPacket(CreatorPacket createPacket)
        {
            if (CreatorPacket == null)
                CreatorPacket = createPacket;
        }

        internal void AddDescriptionPacket(FileDescriptionPacket descPacket)
        {
            string fileid = ToolKit.ToHex(descPacket.fileid);

            if (FileVerification(fileid).FileDescriptionPacket == null)
                FileVerification(fileid).FileDescriptionPacket = descPacket;
        }

        internal void AddMainPacket(MainPacket mainPacket)
        {
            if (MainPacket == null)
                MainPacket = mainPacket;
        }

        internal void AddRecoveryPacket(RecoveryPacket recoveryPacket)
        {
            RecoveryPackets.Add(recoveryPacket);
        }

        internal void AddVerificationPacket(FileVerificationPacket verPacket)
        {
            string fileid = ToolKit.ToHex(verPacket.fileid);

            if (FileVerification(fileid).FileVerificationPacket == null)
                FileVerification(fileid).FileVerificationPacket = verPacket;
        }

        internal bool CreateSourceFileList()
        {
            foreach (byte[] fileidBytes in MainPacket.fileids)
            {
                string fileid = ToolKit.ToHex(fileidBytes);

                if (!this.FileSets.Keys.Contains(fileid))
                    continue;

                FileVerification fileVer = FileSets[fileid];
                fileVer.TargetFileName = Par2Library.ComputeTargetFileName(fileVer.FileDescriptionPacket.name);

                if (!SourceFiles.Contains(fileVer))
                    SourceFiles.Add(fileVer);
            }

            SourceFiles.Sort();

            return true;
        }

        internal bool AllocateSourceBlocks()
        {
            //ulong sourceblockcount = 0;

            foreach (FileVerification fileVer in SourceFiles)
            {
                sourceblockcount += (uint)fileVer.FileVerificationPacket.blockcount;
            }

            // Why return true if there is no sourceblock available ?
            if (sourceblockcount <= 0)
                return true;

            ulong totalsize = 0;

            foreach (FileVerification fileVer in SourceFiles)
            {
                totalsize += fileVer.FileDescriptionPacket.length;
                ulong blockcount = fileVer.FileVerificationPacket.blockcount;
                fileVer.SourceBlocks = new List<DataBlock>();
                fileVer.TargetBlocks = new List<DataBlock>();

                if (blockcount > 0)
                {
                    ulong filesize = fileVer.FileDescriptionPacket.length;

                    for (ulong i = 0; i < blockcount; i++)
                    {
                        DataBlock dataBlock = new DataBlock();
                        dataBlock.offset = i * MainPacket.blocksize;
                        dataBlock.length = Math.Min(MainPacket.blocksize, filesize - (i * MainPacket.blocksize));
                        fileVer.SourceBlocks.Add(dataBlock);
                        fileVer.TargetBlocks.Add(new DataBlock());
                    }
                }
            }

            return true;
        }

        internal bool VerifySourceFiles()
        {
            try
            {
                bool result = true;

                if (!multithreadIO)
                {
                    //ST
                    foreach (FileVerification fileVer in SourceFiles)
                    {
                        if (!File.Exists(fileVer.TargetFileName))
                            continue;

                        // Yes. Record that fact.
                        fileVer.SetTargetExists(true);

                        // Remember that the DiskFile is the target file
                        fileVer.SetTargetFile(new DiskFile(fileVer.TargetFileName));

                        result &= (VerifyFile(fileVer) != null);

                    }
                }
                else
                {
                    //MT : OK
                    using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(2))
                    {
                        List<Task> tasks = new List<Task>();
                        foreach (FileVerification fileVer in SourceFiles)
                        {
                            if (!File.Exists(fileVer.TargetFileName))
                                continue;

                            concurrencySemaphore.Wait();

                            tasks.Add(Task.Factory.StartNew(
                                (f) =>
                                {
                                    try
                                    {
                                        FileVerification file = (FileVerification)f;
                                        // Yes. Record that fact.
                                        file.SetTargetExists(true);

                                        // Remember that the DiskFile is the target file
                                        file.SetTargetFile(new DiskFile(file.TargetFileName));

                                        VerifyFile(file);
                                    }
                                    finally
                                    {
                                        concurrencySemaphore.Release();
                                    }
                                }, fileVer));
                        }

                        Task.WaitAll(tasks.ToArray());
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        internal bool QuickVerifySourceFiles()
        {
            try
            {
                bool result = true;

                foreach (FileVerification fileVer in SourceFiles)
                {
                    if (!File.Exists(fileVer.TargetFileName))
                        continue;

                    result &= (QuickVerifyFile(fileVer) != null);

                }

                return result;
            }
            catch
            {
                return false;
            }
        }

        private bool? QuickVerifyFile(Par2NET.FileVerification fileVer)
        {
            long filesize = 0;
            uint nb_blocks = 0;
            byte[] md5hash = null;
            byte[] md5hash16k = null;

            FileChecker.QuickCheckFile(fileVer.TargetFileName, (int)this.MainPacket.blocksize, out filesize, out nb_blocks, out md5hash16k, out md5hash);

            return false;
        }

        private bool VerifyDataFile(FileVerification fileVer)
        {
            return (bool)VerifyFile(fileVer);
        }

        private bool? VerifyFile(Par2NET.FileVerification fileVer)
        {
            MatchType matchType = MatchType.NoMatch;

            //FileChecker.CheckFile(fileVer.GetTargetFile(), fileVer.TargetFileName, (int)this.MainPacket.blocksize, fileVer.FileVerificationPacket.entries, fileVer.FileDescriptionPacket.hash16k, fileVer.FileDescriptionPacket.hashfull, ref matchType, verificationhashtablefull, verificationhashtable, expectedblocklist, ref expectedblockindex, this.multithreadCPU);
            FileChecker.CheckFile_async(fileVer.GetTargetFile(), fileVer.TargetFileName, (int)this.MainPacket.blocksize, fileVer.FileVerificationPacket.entries, fileVer.FileDescriptionPacket.hash16k, fileVer.FileDescriptionPacket.hashfull, ref matchType, verificationhashtablefull, verificationhashtable, expectedblocklist, ref expectedblockindex, this.multithreadCPU);

            if (matchType == MatchType.FullMatch)
            {
                fileVer.SetCompleteFile(fileVer.GetTargetFile());
                return true;
            }

            return false;
        }

        // Rename any damaged or missnamed target files.
        public bool RenameTargetFiles()
        {
            // Rename any damaged target files
            foreach (FileVerification fileVer in SourceFiles)
            {
                // If the target file exists but is not a complete version of the file
                if (fileVer.GetTargetExists() && fileVer.GetTargetFile() != fileVer.GetCompleteFile())
                {
                    if (!fileVer.GetTargetFile().Rename())
                        return false;

                    // We no longer have a target file
                    fileVer.SetTargetExists(false);
                    fileVer.SetTargetFile(null);
                }
            }

            // Rename any missnamed but complete versions of the files
            foreach (FileVerification fileVer in SourceFiles)
            {
                // If there is no targetfile and there is a complete version
                if (fileVer.GetTargetFile() == null && fileVer.GetCompleteFile() != null)
                {
                    if (!fileVer.GetCompleteFile().Rename(fileVer.TargetFileName))
                        return false;

                    // This file is now the target file
                    fileVer.SetTargetExists(true);
                    fileVer.SetTargetFile(fileVer.GetCompleteFile());

                    // We have one more complete file
                    completefilecount++;
                }
            }

            return true;
        }

        public bool CreateTargetFiles()
        {
            // Create any missing target files
            foreach (FileVerification fileVer in SourceFiles)
            {

                // If the file does not exist
                if (!fileVer.GetTargetExists())
                {
                    DiskFile targetfile = new DiskFile();
                    string filename = fileVer.TargetFileName;
                    ulong filesize = fileVer.FileDescriptionPacket.length;

                    // Create the target file
                    if (!targetfile.Create(filename, filesize))
                    {
                        return false;
                    }

                    // This file is now the target file
                    fileVer.SetTargetExists(true);
                    fileVer.SetTargetFile(targetfile);

                    ulong offset = 0;

                    // Allocate all of the target data blocks
                    for (int index = 0; index < fileVer.TargetBlocks.Count; index++)
                    {
                        DataBlock tb = fileVer.TargetBlocks[index];
                        tb.SetLocation(targetfile, offset);
                        tb.SetLength((ulong) Math.Min(MainPacket.blocksize, filesize - offset));
                       
                        offset += MainPacket.blocksize;
                    }

                    // Add the file to the list of those that will need to be verified
                    // once the repair has completed.
                    verifylist.Add(fileVer);
                }
            }

            return true;
        }

        private void Resize(ref List<DataBlock> list, uint length)
        {
            list = new List<DataBlock>((int)length);

            for (int i = 0; i < length; ++i)
            {
                list.Add((DataBlock)null);
            }
        }

        private void Resize(ref List<bool> list, uint length)
        {
            list = new List<bool>((int)length);

            for (int i = 0; i < length; ++i)
            {
                list.Add(false);
            }
        }

        // Work out which data blocks are available, which need to be copied
        // directly to the output, and which need to be recreated, and compute
        // the appropriate Reed Solomon matrix.
        internal bool ComputeRSmatrix()
        {
            Resize(ref inputblocks, sourceblockcount);       // The DataBlocks that will read from disk
            Resize(ref copyblocks, availableblockcount);     // Those DataBlocks which need to be copied
            Resize(ref outputblocks, missingblockcount);     // Those DataBlocks that will re recalculated

            // Build an array listing which source data blocks are present and which are missing
            List<bool> present = new List<bool>();
            Resize(ref present, sourceblockcount);

            int index = 0;

            int inputindex = 0;
            int copyindex = 0;
            int outputindex = 0;

            // Iterate through all source blocks for all files
            foreach (FileVerification fileVer in SourceFiles)
            {
                for (int i = 0; i < fileVer.SourceBlocks.Count; i++)
                {
                    DataBlock sourceblock = fileVer.SourceBlocks[i];
                    DataBlock targetblock = fileVer.TargetBlocks[i];

                    // Was this block found
                    if (sourceblock.IsSet())
                    {
                        // Record that the block was found
                        present[index] = true;

                        // Add the block to the list of those which will be read 
                        // as input (and which might also need to be copied
                        inputblocks[inputindex] = sourceblock;
                        copyblocks[copyindex] = targetblock;

                        ++inputindex;
                        ++copyindex;
                    }
                    else
                    {
                        // Record that the block was missing
                        present[index] = false;

                        // Add the block to the list of those to be written
                        outputblocks[outputindex] = targetblock;
                        ++outputindex;
                    }

                    ++index;
                }
            }

            // Set the number of source blocks and which of them are present
            if (!rs.SetInput(present.ToArray()))
                return false;

            // Start iterating through the available recovery packets
            int recoverypacketindex = 0;

            // Continue to fill the remaining list of data blocks to be read
            while (inputindex < inputblocks.Count)
            {
                // Get the next available recovery packet
                RecoveryPacket rp = RecoveryPackets[recoverypacketindex];

                // Get the DataBlock from the recovery packet
                DataBlock recoveryblock = rp.GetDataBlock();

                // Add the recovery block to the list of blocks that will be read
                inputblocks[inputindex] = recoveryblock;

                // Record that the corresponding exponent value is the next one
                // to use in the RS matrix
                if (!rs.SetOutput(true, (ushort)rp.exponent))
                    return false;

                ++inputindex;
                ++recoverypacketindex;
            }

            // If we need to, compute and solve the RS matrix
            if (missingblockcount == 0)
                return true;

            return rs.Compute();
        }

        internal void DeleteIncompleteTargetFiles()
        {
            foreach (FileVerification fileVer in verifylist)
            {
                if (fileVer.GetTargetExists())
                {
                    DiskFile targetFile = fileVer.GetTargetFile();

                    if (targetFile.IsOpen())
                        targetFile.Close();

                    targetFile.Delete();

                    fileVer.SetTargetExists(false);
                    fileVer.SetTargetFile(null);
                }
            }
        }

        // Verify that all of the reconstructed target files are now correct.
        // Do this in multiple threads if appropriate (1 thread per processor).
        internal bool VerifyTargetFiles()
        {
            bool finalresult = true;

            // Verify the target files in alphabetical order
            verifylist.Sort();

            if (!multithreadIO)
            {
                //ST
                foreach (FileVerification fileVer in verifylist)
                {
                    DiskFile targetfile = fileVer.GetTargetFile();

                    // Close the file
                    if (targetfile.IsOpen())
                        targetfile.Close();

                    // Mark all data blocks for the file as unknown
                    foreach (DataBlock db in fileVer.SourceBlocks)
                    {
                        db.ClearLocation();
                    }

                    // Say we don't have a complete version of the file
                    fileVer.SetCompleteFile(null);

                    // Re-open the target file
                    if (!targetfile.Open())
                    {
                        finalresult &= false;
                        continue;
                    }

                    // Verify the file again
                    //if (!VerifyDataFile(targetfile, fileVer))
                    expectedblockindex = 0;
                    if (!VerifyDataFile(fileVer))
                        finalresult &= false;

                    // Close the file again
                    targetfile.Close();

                    // Find out how much data we have found
                    UpdateVerificationResults();
                }
            }
            else
            {
                //MT : OK
                List<Task<bool>> tasks = new List<Task<bool>>();
                foreach (FileVerification fileVer in verifylist)
                {
                    tasks.Add(Task.Factory.StartNew<bool>((f) =>
                    {
                        bool result = true;

                        FileVerification file = (FileVerification)f;

                        DiskFile targetfile = file.GetTargetFile();

                        // Close the file
                        if (targetfile.IsOpen())
                            targetfile.Close();

                        // Mark all data blocks for the file as unknown
                        foreach (DataBlock db in file.SourceBlocks)
                        {
                            db.ClearLocation();
                        }

                        // Say we don't have a complete version of the file
                        file.SetCompleteFile(null);

                        // Re-open the target file
                        if (!targetfile.Open())
                        {
                            result &= false;
                            return result;
                        }

                        // Verify the file again
                        //if (!VerifyDataFile(targetfile, fileVer))
                        expectedblockindex = 0;
                        if (!VerifyDataFile(file))
                            result &= false;

                        // Close the file again
                        targetfile.Close();

                        // Find out how much data we have found
                        //UpdateVerificationResults();
                        return result;
                    }, fileVer));
                }

                Task.WaitAll(tasks.ToArray());

                UpdateVerificationResults();

                foreach (Task<bool> t in tasks)
                {
                    finalresult &= t.Result;
                }
            }

            return finalresult;
        }

        // Find out how much data we have found
        internal void UpdateVerificationResults()
        {
            availableblockcount = 0;
            missingblockcount = 0;

            completefilecount = 0;
            renamedfilecount = 0;
            damagedfilecount = 0;
            missingfilecount = 0;

            foreach (FileVerification sourcefile in SourceFiles)
            {
                // Was a perfect match for the file found
                if (sourcefile.GetCompleteFile() != null)
                {
                    // Is it the target file or a different one
                    if (sourcefile.GetCompleteFile() == sourcefile.GetTargetFile())
                    {
                        completefilecount++;
                    }
                    else
                    {
                        renamedfilecount++;
                    }

                    availableblockcount += (uint)sourcefile.FileVerificationPacket.blockcount;
                }
                else
                {
                    // Count the number of blocks that have been found
                    foreach (DataBlock sb in sourcefile.SourceBlocks)
                    {
                        if (sb.IsSet())
                            availableblockcount++;
                    }

                    // Does the target file exist
                    if (sourcefile.GetTargetExists())
                    {
                        damagedfilecount++;
                    }
                    else
                    {
                        missingfilecount++;
                    }
                }
            }

            missingblockcount = sourceblockcount - availableblockcount;
        }

        // Allocate memory buffers for reading and writing data to disk.
        internal bool AllocateBuffers(ulong memoryLimit)
        {
            // Would single pass processing use too much memory
            if (MainPacket.blocksize * missingblockcount > memoryLimit)
            {
                // Pick a size that is small enough
                chunksize = (ulong) (~3 & (int)(memoryLimit / missingblockcount));
            }
            else
            {
                chunksize = MainPacket.blocksize;
            }

            try
            {
                // Allocate the two buffers
                inputbuffer = new byte[chunksize];
                outputbuffer = new byte[chunksize * missingblockcount];
            }
            catch (OutOfMemoryException oome)
            {
                //cerr << "Could not allocate buffer memory." << endl;
                Debug.WriteLine(oome);
                return false;
            }

            return true;
        }

        // Read source data, process it through the RS matrix and write it to disk.
        internal bool ProcessData(ulong blockoffset, uint blocklength)
        {
            ulong totalwritten = 0;

            // Clear the output buffer
            outputbuffer = new byte[chunksize * missingblockcount];

            uint inputblockindex = 0;
            uint copyblockindex = 0;
            uint inputindex = 0;

            DiskFile lastopenfile = null;

            // Are there any blocks which need to be reconstructed
            if (missingblockcount > 0)
            {
                // For each input block
                //while (inputblock != inputblocks.end())       
                while (inputblockindex < inputblocks.Count)
                {
                    DataBlock inputblock = inputblocks[(int)inputblockindex];

                    // Are we reading from a new file?
                    if (lastopenfile != inputblock.GetDiskFile())
                    {
                        // Close the last file
                        if (lastopenfile != null)
                        {
                            lastopenfile.Close();
                        }

                        // Open the new file
                        lastopenfile = inputblock.GetDiskFile();
                        if (!lastopenfile.Open())
                        {
                            return false;
                        }
                    }

                    // Read data from the current input block
                    if (!inputblock.ReadData(blockoffset, blocklength, inputbuffer))
                        return false;

                    // Have we reached the last source data block
                    if (copyblockindex < copyblocks.Count)
                    {
                        DataBlock copyblock = copyblocks[(int)copyblockindex];

                        // Does this block need to be copied to the target file
                        if (copyblock.IsSet())
                        {
                            uint wrote = 0;

                            // Write the block back to disk in the new target file
                            if (!copyblock.WriteData(blockoffset, blocklength, inputbuffer, out wrote))
                                return false;

                            totalwritten += wrote;
                            ++copyblockindex;
                        }
                    }

                    // Function to process things in multiple threads if appropariate
                    if (!RepairMissingBlocks(blocklength, inputindex))
                        return false;

                    ++inputblockindex;
                    ++inputindex;
                }
            }
            else
            {
                // Reconstruction is not required, we are just copying blocks between files

                // For each block that might need to be copied
                while (copyblockindex < copyblocks.Count)
                {
                    DataBlock inputblock = inputblocks[(int)inputblockindex];
                    DataBlock copyblock = copyblocks[(int)copyblockindex];

                    // Does this block need to be copied
                    if (copyblock.IsSet())
                    {
                        // Are we reading from a new file?
                        if (lastopenfile != inputblock.GetDiskFile())
                        {
                            // Close the last file
                            if (lastopenfile != null)
                            {
                                lastopenfile.Close();
                            }

                            // Open the new file
                            lastopenfile = inputblock.GetDiskFile();
                            if (!lastopenfile.Open())
                            {
                                return false;
                            }
                        }

                        // Read data from the current input block
                        if (!inputblock.ReadData(blockoffset, blocklength, inputbuffer))
                            return false;

                        uint wrote = 0;
                        if (!copyblock.WriteData(blockoffset, blocklength, inputbuffer, out wrote))
                            return false;
                        totalwritten += wrote;
                    }

                    //if (noiselevel > CommandLine::nlQuiet)
                    //{
                    //  // Update a progress indicator
                    //  u32 oldfraction = (u32)(1000 * progress / totaldata);
                    //  progress += blocklength;
                    //  u32 newfraction = (u32)(1000 * progress / totaldata);

                    //  if (oldfraction != newfraction)
                    //  {
                    //    cout << "Processing: " << newfraction/10 << '.' << newfraction%10 << "%\r" << flush;
                    //  }
                    //}

                    ++copyblockindex;
                    ++inputblockindex;
                }
            }

            // Close the last file
            if (lastopenfile != null)
            {
                lastopenfile.Close();
            }

            //if (noiselevel > CommandLine::nlQuiet)
            //  cout << "Writing recovered data\r";

#if TRACE
            ToolKit.LogArrayToFile<byte>("outputbuffer.log", outputbuffer);
#endif

            // For each output block that has been recomputed
            for (uint outputindex = 0; outputindex < missingblockcount; outputindex++)
            {
                DataBlock outputblock = outputblocks[(int)outputindex];

                // Select the appropriate part of the output buffer
                ulong startIndex = chunksize * outputindex;


                // Write the data to the target file
                uint wrote = 0;
                if (!outputblock.WriteData(blockoffset, blocklength, outputbuffer, startIndex, out wrote))
                    return false;
                totalwritten += wrote;

                //++outputblock;
            }

            //if (noiselevel > CommandLine::nlQuiet)
            //  cout << "Wrote " << totalwritten << " bytes to disk" << endl;

            return true;
        }

        private bool RepairMissingBlocks(uint blocklength, uint inputindex)
        {
            // Used from within ProcessData.

            if (missingblockcount == 0)
                return true;		// Nothing to do, actually

            bool rv = true;		// Optimistic default

            int lNumThreads = Environment.ProcessorCount;
            //int lNumThreads = 2;

            // First, establish the number of blocks to be processed by each thread. Of course the last
            // one started might get some less...
            int lNumBlocksPerThread = (int)(missingblockcount - 1) / lNumThreads + 1;		// Round up
            uint lCurrentStartBlockNo = 0;

            List<Task> tasks = new List<Task>();

            while (lCurrentStartBlockNo < missingblockcount)
            {
                uint lNextStartBlockNo = (uint)(lCurrentStartBlockNo + lNumBlocksPerThread);
                if (lNextStartBlockNo > missingblockcount)
                    lNextStartBlockNo = missingblockcount;		// Constraint

                //MT : OK
                object[] args = new object[] { blocklength, inputindex, lCurrentStartBlockNo, lNextStartBlockNo };
                tasks.Add(Task.Factory.StartNew((a) => 
                {
                    object[] list = (object[])a;
                    uint bl = (uint)list[0];
                    uint ii = (uint)list[1];
                    uint csb = (uint)list[2];
                    uint nsb = (uint)list[3];
                    RepairMissingBlockRange(bl, ii, csb, nsb); 
                }, args, TaskCreationOptions.LongRunning));
                
                //ST
                //RepairMissingBlockRange(blocklength, inputindex, lCurrentStartBlockNo, lNextStartBlockNo);

                lCurrentStartBlockNo = lNextStartBlockNo;
            }

            Task.WaitAll(tasks.ToArray());

            return rv;
        }

        private void RepairMissingBlockRange(uint blocklength, uint inputindex, uint aStartBlockNo, uint aEndBlockNo)
        {
            // This function runs in multiple threads.
            // For each output block
            for (uint outputindex = aStartBlockNo; outputindex < aEndBlockNo; outputindex++)
            {
                // Select the appropriate part of the output buffer
                //byte[] outbuf = new byte[blocklength];

                //Buffer.BlockCopy(outputbuffer, (int)(chunksize * outputindex), outbuf, 0, outbuf.Length);

                // Process the data
                rs.Process(blocklength, inputindex, inputbuffer, outputindex, outputbuffer, (int)(chunksize * outputindex), blocklength);

#if TRACE
                //ToolKit.LogArrayToFile<byte>("outbuf." + inputindex + ".log", outbuf);
#endif
                //Buffer.BlockCopy(outbuf, 0, outputbuffer, (int)(chunksize * outputindex), outbuf.Length);              
            }
        }

//        private void RepairMissingBlockRange_orig(uint blocklength, uint inputindex, uint aStartBlockNo, uint aEndBlockNo)
//        {
//            // This function runs in multiple threads.
//            // For each output block
//            for (uint outputindex = aStartBlockNo; outputindex < aEndBlockNo; outputindex++)
//            {
//                // Select the appropriate part of the output buffer
//                byte[] outbuf = new byte[blocklength];

//                Buffer.BlockCopy(outputbuffer, (int)(chunksize * outputindex), outbuf, 0, outbuf.Length);

//                // Process the data
//                rs.Process_orig(blocklength, inputindex, inputbuffer, outputindex, outbuf);

//#if TRACE
//                ToolKit.LogArrayToFile<byte>("outbuf." + inputindex + ".log", outbuf);
//#endif
//                Buffer.BlockCopy(outbuf, 0, outputbuffer, (int)(chunksize * outputindex), outbuf.Length);
//            }
//        }

        // Check the verification results and report the results 
        internal bool CheckVerificationResults(bool aSilent)
        {
            // Is repair needed
            if (completefilecount < MainPacket.recoverablefilecount || renamedfilecount > 0 || damagedfilecount > 0 || missingfilecount > 0)
            {
                if (!aSilent)
                {
                    Console.WriteLine("Repair is required.");
                    Console.WriteLine();


                    if (renamedfilecount > 0) Console.WriteLine("{0} file(s) have the wrong name.", renamedfilecount);
                    if (missingfilecount > 0) Console.WriteLine("{0} file(s) are missing.", missingfilecount);
                    if (damagedfilecount > 0) Console.WriteLine("{0} file(s) exist but are damaged.", damagedfilecount);
                    if (completefilecount > 0) Console.WriteLine("{0} file(s) are ok.", completefilecount);

                    Console.WriteLine("You have {0} out of {1} datablocks available", availableblockcount, sourceblockcount);

                    if (RecoveryPackets.Count > 0)
                        Console.WriteLine("You have {0} recovery blocks available.", RecoveryPackets.Count);
                }

                // Is repair possible
                if (RecoveryPackets.Count >= missingblockcount)
                {
                    if (!aSilent)
                    {
                        Console.WriteLine("Repair is possible.");

                        if (RecoveryPackets.Count > missingblockcount)
                            Console.WriteLine("You have an excess of {0} recovery blocks", RecoveryPackets.Count - missingblockcount);

                        if (missingblockcount > 0)
                            Console.WriteLine("{0} recovery blocks will be used to repair.", missingblockcount);
                        else if (RecoveryPackets.Count > 0)
                            Console.WriteLine("None of the recovery blocks will be used for the repair.");
                    }
                    return true;
                }
                else
                {
                    if (!aSilent)
                    {
                        Console.WriteLine("Repair is not possible.");
                        Console.WriteLine("You need {0} more recovery blocks to be able to repair.", missingblockcount - RecoveryPackets.Count);
                    }
                    return false;
                }
            }
            else
            {
                if (!aSilent)
                {
                    Console.WriteLine("All files are correct, repair is not required.");
                }
                return true;
            }
        }

        // Create a verification hash table for all files for which we have not
        // found a complete version of the file and for which we have
        // a verification packet
        internal bool PrepareVerificationHashTable()
        {
            foreach (FileVerification sourcefile in SourceFiles)
            {
                if (sourcefile == null)
                    continue;

                // Do we have a verification packet
                if (sourcefile.GetVerificationPacket() == null)
                    continue;

                // Yes. Load the verification entries into the hash table
                Load(sourcefile, MainPacket.blocksize);
            }

            return true;
        }

        private void Load(Par2NET.FileVerification sourcefile, ulong blocksize)
        {
            // Get information from the sourcefile
            FileVerificationPacket verificationPacket = sourcefile.GetVerificationPacket();
            uint blockcount = (uint)verificationPacket.blockcount;

            // Iterate throught the data blocks for the source file and the verification
            // entries in the verification packet.
            uint blocknumber = 0;
            uint sourceblockindex = 0;

            while (blocknumber < blockcount)
            {
                DataBlock datablock = sourcefile.SourceBlocks[(int)sourceblockindex];

                // Create a new VerificationHashEntry with the details for the current
                // data block and verification entry.
                FileVerificationEntry entry = verificationPacket.entries[(int)blocknumber];
                entry.datablock = datablock;

                // Create a new VerificationHashEntry with the details for the current
                // data block and verification entry.
                if (!verificationhashtable.ContainsKey(entry.crc))
                    verificationhashtable.Add(entry.crc, entry);

                uint key = (uint)(entry.crc ^ (Path.GetFileName(sourcefile.TargetFileName).GetHashCode()));

                if (!verificationhashtablefull.ContainsKey(key))
                {
                    verificationhashtablefull.Add(key, entry);
                    expectedblocklist.Add(entry);
                }

                ++blocknumber;
                ++sourceblockindex;
            }
        }
    }
}
