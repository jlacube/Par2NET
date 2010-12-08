using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Packets;
using System.IO;
using FastGaloisFields;

namespace Par2NET
{
    public class Par2RecoverySet
    {
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

        private Dictionary<uint, FileVerificationEntry> verificationhashtable = new Dictionary<uint, FileVerificationEntry>();
        private Dictionary<uint, FileVerificationEntry> verificationhashtablefull = new Dictionary<uint, FileVerificationEntry>();

        ReedSolomonGalois16     rs = new ReedSolomonGalois16();                      // The Reed Solomon matrix.

        private List<FileVerification> verifylist = new List<FileVerification>();

        private FileVerification FileVerification(string fileid)
        {
            if (!FileSets.Keys.Contains(fileid))
                FileSets.Add(fileid, new FileVerification());

            return FileSets[fileid];
        }

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

                return result;
            }
            catch
            {
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

            FileChecker.CheckFile(fileVer.TargetFileName, (int)this.MainPacket.blocksize, fileVer.FileVerificationPacket.entries, fileVer.FileDescriptionPacket.hash16k, fileVer.FileDescriptionPacket.hashfull, ref matchType, verificationhashtablefull, verificationhashtable);

            if (matchType == MatchType.FullMatch)
            {
                fileVer.SetCompleteFile(fileVer.GetTargetFile());
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
            uint filenumber = 0;

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
                        tb.offset = offset;
                        tb.length = Math.Min(MainPacket.blocksize, filesize - offset);

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
                        inputblocks[index] = sourceblock;
                        copyblocks[index] = targetblock;

                        ++inputindex;
                        ++copyindex;
                    }
                    else
                    {
                        // Record that the block was missing
                        present[index] = false;

                        // Add the block to the list of those to be written
                        outputblocks[index] = targetblock;
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

	        foreach( FileVerification fileVer in verifylist)
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
                if (!VerifyDataFile(fileVer))
                    finalresult &= false;

                // Close the file again
                targetfile.Close();

                // Find out how much data we have found
                UpdateVerificationResults();
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
                    DataBlock copyblock = copyblocks[(int)copyblockindex];

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
                    if (copyblockindex != copyblocks.Count)
                    {
                        // Does this block need to be copied to the target file
                        if (copyblock.IsSet())
                        {
                            uint wrote = 0;

                            // Write the block back to disk in the new target file
                            if (!copyblock.WriteData(blockoffset, blocklength, inputbuffer, out wrote))
                                return false;

                            totalwritten += wrote;
                        }
                        ++copyblockindex;
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
                while (copyblockindex != copyblocks.Count)
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

            // First, establish the number of blocks to be processed by each thread. Of course the last
            // one started might get some less...
            int lNumBlocksPerThread = (int)(missingblockcount - 1) / lNumThreads + 1;		// Round up
            uint lCurrentStartBlockNo = 0;

            while (lCurrentStartBlockNo < missingblockcount)
            {
                uint lNextStartBlockNo = (uint)(lCurrentStartBlockNo + lNumBlocksPerThread);
                if (lNextStartBlockNo > missingblockcount)
                    lNextStartBlockNo = missingblockcount;		// Constrain

                RepairMissingBlockRange(blocklength, inputindex, lCurrentStartBlockNo, lNextStartBlockNo);
            }

            return rv;
        }

        private void RepairMissingBlockRange(uint blocklength, uint inputindex, uint aStartBlockNo, uint aEndBlockNo)
        {
            // This function runs in multiple threads.
            // For each output block
            for (uint outputindex = aStartBlockNo; outputindex < aEndBlockNo; outputindex++)
            {
                // Select the appropriate part of the output buffer
                byte[] outbuf = new byte[blocklength];
                Buffer.BlockCopy(outputbuffer, (int)(chunksize * outputindex), outbuf, 0, outbuf.Length);

                // Process the data
                rs.Process(blocklength, inputindex, inputbuffer, outputindex, outbuf);

                //if (noiselevel > CommandLine::nlQuiet)
                //{
                //    // Update a progress indicator. This is thread-safe with a simple mutex
                //    pthread_mutex_lock (&progressMutex);
                //    progress += blocklength;
                //    u32 newfraction = (u32)(1000 * progress / totaldata);

                //    // Only report "Repairing" when a certain amount of progress has been made
                //    // since last time, or when the progress is 100%
                //    if ((newfraction - previouslyReportedProgress >= 10) || (newfraction == 1000))
                //    {
                //        cout << "Repairing: " << newfraction/10 << '.' << newfraction%10 << "%\r" << flush;
                //        previouslyReportedProgress = newfraction;
                //    }
                //    pthread_mutex_unlock (&progressMutex);
                //}
            }
        }

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

          cout << "You have " << availableblockcount 
               << " out of " << sourceblockcount 
               << " data blocks available." << endl;
          if (recoverypacketmap.size() > 0)
            cout << "You have " << (u32)recoverypacketmap.size() 
                 << " recovery blocks available." << endl;
        }
    }

    // Is repair possible
    if (RecoveryPackets.Count  >= missingblockcount)
    {
      if (!aSilent)
      {
          if (noiselevel > CommandLine::nlSilent)
            cout << "Repair is possible." << endl;

          if (noiselevel > CommandLine::nlQuiet)
          {
            if (recoverypacketmap.size() > missingblockcount)
              cout << "You have an excess of " 
                   << (u32)recoverypacketmap.size() - missingblockcount
                   << " recovery blocks." << endl;

            if (missingblockcount > 0)
              cout << missingblockcount
                   << " recovery blocks will be used to repair." << endl;
            else if (recoverypacketmap.size())
              cout << "None of the recovery blocks will be used for the repair." << endl;
          }
      }
      return true;
    }
    else
    {
      if (!aSilent)
      {
          if (noiselevel > CommandLine::nlSilent)
          {
            cout << "Repair is not possible." << endl;
            cout << "You need " << missingblockcount - recoverypacketmap.size()
                 << " more recovery blocks to be able to repair." << endl;
          }
      }
      return false;
    }
  }
  else
  {
    if (!aSilent)
    {
        if (noiselevel > CommandLine::nlSilent)
          cout << "All files are correct, repair is not required." << endl;
    }
    return true;
  }

  return true;
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

                uint key = (uint)(entry.crc ^ (17 * Path.GetFileName(sourcefile.TargetFileName).GetHashCode()));

                if (!verificationhashtablefull.ContainsKey(key))
                    verificationhashtablefull.Add(key, entry);

                ++blocknumber;
                ++sourceblockindex;
            }
        }
    }
}
