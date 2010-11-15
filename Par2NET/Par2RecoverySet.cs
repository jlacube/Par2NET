using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Packets;
using System.IO;

namespace Par2NET
{
    public class FileVerification
    {
        public FileDescriptionPacket FileDescriptionPacket = null;
        public FileVerificationPacket FileVerificationPacket = null;
        public string TargetFileName = string.Empty;
    }

    public class Par2RecoverySet
    {
        public CreatorPacket CreatorPacket = null;
        public MainPacket MainPacket = null;
        public List<RecoveryPacket> RecoveryPackets = new List<RecoveryPacket>();
        public Dictionary<string, FileVerification> FileSets = new Dictionary<string, FileVerification>();
        public List<FileVerification> SourceFiles = new List<FileVerification>();

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

            Dictionary<ulong, DataBlock> sourceblocks = new Dictionary<ulong, DataBlock>();
            Dictionary<ulong, DataBlock> targetblocks = new Dictionary<ulong, DataBlock>();

            for (ulong index = 0; index < sourceblockcount; index++)
            {
                sourceblocks.Add(index, new DataBlock());
                targetblocks.Add(index, new DataBlock());
            }

            ulong totalsize = 0;
            ulong blocknumber = 0;

            foreach (FileVerification fileVer in SourceFiles)
            {
                totalsize += fileVer.FileDescriptionPacket.length;
                ulong blockcount = fileVer.FileVerificationPacket.blockcount;

                if (blockcount > 0)
                {
                    ulong filesize = fileVer.FileDescriptionPacket.length;

                    for (ulong i = 0; i < blockcount; i++)
                    {
                        DataBlock dataBlock = sourceblocks[blocknumber];
                        dataBlock.Offset = 0;
                        dataBlock.Length = Math.Min(MainPacket.blocksize, filesize - (i * MainPacket.blocksize));
                        blocknumber++;
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
    }
}
