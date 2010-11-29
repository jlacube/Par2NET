using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Packets;
using System.IO;

namespace Par2NET
{
    public class Par2RecoverySet
    {
        public CreatorPacket CreatorPacket = null;
        public MainPacket MainPacket = null;
        public List<RecoveryPacket> RecoveryPackets = new List<RecoveryPacket>();
        public Dictionary<string, FileVerification> FileSets = new Dictionary<string, FileVerification>();
        public List<FileVerification> SourceFiles = new List<FileVerification>();

        //private Dictionary<ulong, DataBlock> sourceblocks = new Dictionary<ulong, DataBlock>();
        //private Dictionary<ulong, DataBlock> targetblocks = new Dictionary<ulong, DataBlock>();

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
            ulong sourceblockcount = 0;

            foreach (FileVerification fileVer in SourceFiles)
            {
                sourceblockcount += fileVer.FileVerificationPacket.blockcount;
            }

            // Why return true if there is no sourceblock available ?
            if (sourceblockcount <= 0)
                return true;

            //sourceblocks = new Dictionary<ulong, DataBlock>();
            //targetblocks = new Dictionary<ulong, DataBlock>();

            //for (ulong index = 0; index < sourceblockcount; index++)
            //{
            //    sourceblocks.Add(index, new DataBlock());
            //    targetblocks.Add(index, new DataBlock());
            //}

            ulong totalsize = 0;
            //ulong blocknumber = 0;

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
                        //DataBlock dataBlock = sourceblocks[blocknumber];
                        DataBlock dataBlock = new DataBlock();
                        dataBlock.Offset = i * MainPacket.blocksize;
                        dataBlock.Length = Math.Min(MainPacket.blocksize, filesize - (i * MainPacket.blocksize));
                        fileVer.SourceBlocks.Add(dataBlock);
                        fileVer.TargetBlocks.Add(new DataBlock());
                        //blocknumber++;
                    }
                }
            }

            return true;
        }

        internal bool PrepareVerificationHashTable()
        {
            throw new NotImplementedException();
        }

        internal bool ComputeWindowTable()
        {
            throw new NotImplementedException();
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

        private bool? VerifyFile(Par2NET.FileVerification fileVer)
        {
            FileChecker.CheckFile(fileVer.TargetFileName, (int)this.MainPacket.blocksize, fileVer.FileVerificationPacket.entries, fileVer.FileDescriptionPacket.hash16k, fileVer.FileDescriptionPacket.hashfull);

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
                    //TODO : add member to class
                    //completefilecount++;
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
                        tb.Offset = offset;
                        tb.Length = Math.Min(MainPacket.blocksize, filesize - offset);

                        offset += MainPacket.blocksize;
                    }

                    // Add the file to the list of those that will need to be verified
                    // once the repair has completed.
                    //verifylist.push_back(sourcefile);
                }
            }

            return true;
        }
    }
}
