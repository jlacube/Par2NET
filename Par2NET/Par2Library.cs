using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Par2NET.Tasks;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using Par2NET.Interfaces;

namespace Par2NET
{
    [Flags]
    public enum ParResult {
        Success = 0,

        RepairPossible = 1,  // Data files are damaged and there is
        // enough recovery data available to
        // repair them.

        RepairNotPossible = 2,  // Data files are damaged and there is
        // insufficient recovery data available
        // to be able to repair them.

        InvalidCommandLineArguments = 3,  // There was something wrong with the
        // command line arguments

        InsufficientCriticalData = 4,  // The PAR2 files did not contain sufficient
        // information about the data files to be able
        // to verify them.

        RepairFailed = 5,  // Repair completed but the data files
        // still appear to be damaged.


        FileIOError = 6,  // An error occured when accessing files
        LogicError = 7,  // An internal error occurred
        MemoryError = 8,  // Out of memory
    };

    public enum ParVersion
    {
        Par1,       // PAR1 version
        Par2        // PAR2 version
    }

    public enum ParAction
    {
        ParCreate,      // PAR creation
        ParVerify,      // PAR verify
        ParRepair       // PAR repair
    }

    public enum PacketType
    {
        Unknown = 0,
        MagicPacket,
        CreatorPacket,
        MainPacket,
        DescriptionPacket,
        VerificationPacket,
        RecoveryPacket
    }

    public enum MatchType
    {
        NoMatch = 0,
        PartialMatch,
        FullMatch
    }

    public enum NoiseLevel
    {
        Unknown = 0,
        Silent,       // Absolutely no output (other than errors)
        Quiet,        // Bare minimum of output
        Normal,       // Normal level of output
        Noisy,        // Lots of output
        Debug         // Extra debugging information
    }

    public class ToolKit
    {
        public static byte[] InitByteArrayFromChars(params char[] chars)
        {
            byte[] result = new byte[chars.Length];

            for (uint i = 0; i < chars.Length; i++)
            {
                result[i] = (byte)chars[i];
            }

            return result;
        }

        public static int IndexOf(byte[] ByteArrayToSearch, byte[] ByteArrayToFind)
        {
            return IndexOf(ByteArrayToSearch, ByteArrayToFind, 0);
        }

        public static int IndexOf(byte[] ByteArrayToSearch, byte[] ByteArrayToFind, int start_index)
        {
            // Any encoding will do, as long as all bytes represent a unique character.
            Encoding encoding = Encoding.GetEncoding(1252);

            string toSearch = encoding.GetString(ByteArrayToSearch, 0, ByteArrayToSearch.Length);
            string toFind = encoding.GetString(ByteArrayToFind, 0, ByteArrayToFind.Length);
            int result = toSearch.IndexOf(toFind, start_index, StringComparison.Ordinal);
            return result;
        }

        public static T ReadStruct<T>(byte[] array, int start, int length)
        {
            byte[] buffer = new byte[length];

            Buffer.BlockCopy(array, start, buffer, 0, length);

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            T temp = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return temp;
        }

        public static unsafe bool UnsafeCompare(byte[] a1, byte[] a2)
        {
            if (a1 == null || a2 == null || a1.Length != a2.Length)
                return false;
            fixed (byte* p1 = a1, p2 = a2)
            {
                byte* x1 = p1, x2 = p2;
                int l = a1.Length;
                for (int i = 0; i < l / 8; i++, x1 += 8, x2 += 8)
                    if (*((long*)x1) != *((long*)x2)) return false;
                if ((l & 4) != 0) { if (*((int*)x1) != *((int*)x2)) return false; x1 += 4; x2 += 4; }
                if ((l & 2) != 0) { if (*((short*)x1) != *((short*)x2)) return false; x1 += 2; x2 += 2; }
                if ((l & 1) != 0) if (*((byte*)x1) != *((byte*)x2)) return false;
                return true;
            }
        }

        public static string ToHex(byte[] data)
        {
            StringBuilder builder = new StringBuilder();
            foreach (byte item in data) builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static string ByteArrayToString(byte[] array)
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(array);
        }
    }

    public class Par2Library
    {
        private static string targetPath = string.Empty;
        private static NoiseLevel noiseLevel = NoiseLevel.Normal;
        private static bool searchAllExtMisNamedFiles = false;

        public static void SetNoiseLevel(NoiseLevel newNoiseLevel)
        {
            noiseLevel = newNoiseLevel;
        }

        public static void SetSearchMisNamedFiles(bool value)
        {
            searchAllExtMisNamedFiles = value;
        }

        public static string ComputeTargetFileName(string filename)
        {
            return Path.Combine(targetPath,Path.GetFileName(filename));
        }

        private Par1Library par1Library = null;

        private Par1Library Par1Library()
        {
            if (par1Library == null)
                par1Library = new Par1Library();

            return par1Library;
        }

        private Dictionary<string, Par2RecoverySet> setids = new Dictionary<string, Par2RecoverySet>();

        private void UpdateOrAddRecoverySet(Par2RecoverySet recoverySet)
        {
            string setid = ToolKit.ToHex(recoverySet.MainPacket.header.setid);

            if (!setids.Keys.Contains(setid))
                setids.Add(setid, recoverySet);
            else
            {
                UpdateRecoverySet(setids[setid], recoverySet);
            }
        }

        private void UpdateRecoverySet(Par2RecoverySet oldRecoverySet, Par2RecoverySet newRecoverySet)
        {
            if (oldRecoverySet.CreatorPacket == null && newRecoverySet.CreatorPacket != null)
                oldRecoverySet.CreatorPacket = newRecoverySet.CreatorPacket;

            if (oldRecoverySet.MainPacket == null && newRecoverySet.MainPacket != null)
                oldRecoverySet.MainPacket = newRecoverySet.MainPacket;

            foreach (string key in newRecoverySet.FileSets.Keys)
            {
                if (!oldRecoverySet.FileSets.Keys.Contains(key))
                    oldRecoverySet.FileSets.Add(key, newRecoverySet.FileSets[key]);
            }

            oldRecoverySet.RecoveryPackets.AddRange(newRecoverySet.RecoveryPackets);

            // Cleaning
            newRecoverySet.RecoveryPackets.Clear();
            newRecoverySet.FileSets.Clear();
        }

        public ParResult Process(ParVersion version, List<string> inputFiles, List<string> recoveryFiles, ParAction action, string targetPath)
        {
            Par2Library.targetPath = targetPath;

            if (version == ParVersion.Par1)
                return Par1Library().Process(inputFiles, recoveryFiles, action);

            if (action != ParAction.ParCreate)
                GetRecoveryFiles(recoveryFiles);

            switch (action)
            {
                case ParAction.ParCreate:
                    return Create(ref inputFiles, ref recoveryFiles);
                case ParAction.ParRepair:
                    return Repair(ref inputFiles, ref recoveryFiles);
                case ParAction.ParVerify:
                    return Verify(ref inputFiles, ref recoveryFiles);
            }

            return ParResult.LogicError;
        }

        private void GetRecoveryFiles(List<string> recoveryFiles)
        {
            List<string> paths = new List<string>();
            List<string> radixes = new List<string>();

            foreach (string file in recoveryFiles)
            {
                string path = Path.GetDirectoryName(file);
                if (Directory.Exists(path))
                    paths.Add(path);

                string shortname = Path.GetFileNameWithoutExtension(file);
                if (shortname.Contains(".vol"))
                    radixes.Add(shortname.Substring(0, shortname.IndexOf(".vol")));
                else
                    radixes.Add(shortname);
            }

            foreach (string path in paths)
            {
                foreach (string radix in radixes)
                {
                    foreach (string file in Directory.GetFiles(path, radix + "*", SearchOption.TopDirectoryOnly))
                    {
                        if ((Path.GetExtension(file).ToLower() == ".par2") && (!recoveryFiles.Contains(file)))
                            recoveryFiles.Add(file);
                    }
                }
            }

            recoveryFiles.Sort();
        }

        private ParResult Create(ref List<string> inputFiles, ref List<string> recoveryFiles)
        {
            throw new NotImplementedException();
        }

        private ParResult Repair(ref List<string> inputFiles, ref List<string> recoveryFiles)
        {
            ParResult verifyResult = Verify(ref inputFiles, ref recoveryFiles);

            if (verifyResult != ParResult.Success && verifyResult != ParResult.RepairPossible)
                return verifyResult;

            // Add code to find setid propelry
            string setid = setids.Keys.First();

            // Rename any damaged or missnamed target files.
            if (!!setids[setid].RenameTargetFiles())
                return ParResult.FileIOError;

      //// Are we still missing any files
      //if (completefilecount<mainpacket->RecoverableFileCount())
      //{
            // Work out which files are being repaired, create them, and allocate
            // target DataBlocks to them, and remember them for later verification.
            if (!setids[setid].CreateTargetFiles())
                return ParResult.FileIOError;

      //  // Work out which data blocks are available, which need to be copied
      //  // directly to the output, and which need to be recreated, and compute
      //  // the appropriate Reed Solomon matrix.
      //  if (!ComputeRSmatrix())
      //  {
      //    // Delete all of the partly reconstructed files
      //    DeleteIncompleteTargetFiles();
      //    return eFileIOError;
      //  }

      //  if (noiselevel > CommandLine::nlSilent)
      //    cout << endl;

      //  // Allocate memory buffers for reading and writing data to disk.
      //  if (!AllocateBuffers(commandline.GetMemoryLimit()))
      //  {
      //    // Delete all of the partly reconstructed files
      //    DeleteIncompleteTargetFiles();
      //    return eMemoryError;
      //  }

      //  // Set the total amount of data to be processed.
      //  progress = 0;
      //  this->previouslyReportedProgress = -10000000;	// Big negative
      //  totaldata = blocksize * sourceblockcount * (missingblockcount > 0 ? missingblockcount : 1);

      //  // Start at an offset of 0 within a block.
      //  u64 blockoffset = 0;
      //  while (blockoffset < blocksize) // Continue until the end of the block.
      //  {
      //    // Work out how much data to process this time.
      //    size_t blocklength = (size_t)min((u64)chunksize, blocksize-blockoffset);

      //    // Read source data, process it through the RS matrix and write it to disk.
      //    if (!ProcessData(blockoffset, blocklength))
      //    {
      //      // Delete all of the partly reconstructed files
      //      DeleteIncompleteTargetFiles();
      //      return eFileIOError;
      //    }

      //    // Advance to the need offset within each block
      //    blockoffset += blocklength;
      //  }

      //  if (noiselevel > CommandLine::nlSilent)
      //    cout << endl << "Verifying repaired files:" << endl << endl;

      //  // Verify that all of the reconstructed target files are now correct
      //  if (!VerifyTargetFiles())
      //  {
      //    // Delete all of the partly reconstructed files
      //    DeleteIncompleteTargetFiles();
      //    return eFileIOError;
      //  }
      //}

      //// Are all of the target files now complete?
      //if (completefilecount<mainpacket->RecoverableFileCount())
      //{
      //  cerr << "Repair Failed." << endl;
      //  return eRepairFailed;
      //}
      //else
      //{
      //  if (noiselevel > CommandLine::nlSilent)
      //    cout << endl << "Repair complete." << endl;
      //}

            return ParResult.Success;
        }

        private ParResult Verify(ref List<string> inputFiles, ref List<string> recoveryFiles)
        {
            //FastGaloisFields.GaloisTables.GaloisTable16 GT16 = new FastGaloisFields.GaloisTables.GaloisTable16(16, 0x1100B);

            try
            {
                foreach (string recoveryFile in recoveryFiles)
                {
                    Par2FileReader reader = new Par2FileReader(recoveryFile);
                    UpdateOrAddRecoverySet(reader.ReadRecoverySet());
                }
            }
            catch (Exception)
            {
                return ParResult.LogicError;
            }

            // Add code to find setid propelry
            string setid = setids.Keys.First();

            if (!setids[setid].CheckPacketsConsistency())
                return ParResult.InsufficientCriticalData;

            if (!setids[setid].CreateSourceFileList())
                return ParResult.LogicError;

            //foreach (FileVerification fileVer in setids[setid].FileSets.Values)
            //{
            //    foreach (Packets.FileVerificationEntry entry in fileVer.FileVerificationPacket.entries)
            //    {
            //        Console.WriteLine("crc32:{0},md5:{1}", entry.crc, ToolKit.ToHex(entry.hash));
            //    }
            //}

            if (!setids[setid].AllocateSourceBlocks())
                return ParResult.LogicError;

            //if (!setids[setid].PrepareVerificationHashTable())
            //    return ParResult.LogicError;

            //if (!setids[setid].ComputeWindowTable())
            //    return ParResult.LogicError;

            // 1st a quick verify to extract basic information from files like md5 hashes to be able to match files with wrong names
            if (!setids[setid].QuickVerifySourceFiles())
                return ParResult.LogicError;

            // Attempt to verify all of the source files
            if (!setids[setid].VerifySourceFiles())
                return ParResult.LogicError;
            
            // TODO: Send return with
            // ParResult.Success || ParResult.RepairPossible || ParResult.RepairNotPossible

            return ParResult.Success;
        }
    }
}
