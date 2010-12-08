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

            // Are we still missing any files
            if (setids[setid].completefilecount < setids[setid].MainPacket.recoverablefilecount)
            {
                // Work out which files are being repaired, create them, and allocate
                // target DataBlocks to them, and remember them for later verification.
                if (!setids[setid].CreateTargetFiles())
                    return ParResult.FileIOError;

                // Work out which data blocks are available, which need to be copied
                // directly to the output, and which need to be recreated, and compute
                // the appropriate Reed Solomon matrix.
                if (!setids[setid].ComputeRSmatrix())
                {
                    // Delete all of the partly reconstructed files
                    setids[setid].DeleteIncompleteTargetFiles();
                    return ParResult.FileIOError;
                }

                //  if (noiselevel > CommandLine::nlSilent)
                //    cout << endl;

                // Allocate memory buffers for reading and writing data to disk.
                if (!setids[setid].AllocateBuffers(GetMemoryLimit()))
                {
                    // Delete all of the partly reconstructed files
                    setids[setid].DeleteIncompleteTargetFiles();
                    return ParResult.MemoryError;
                }

                // Set the total amount of data to be processed.
                setids[setid].totaldata = setids[setid].MainPacket.blocksize * setids[setid].sourceblockcount * (setids[setid].missingblockcount > 0 ? setids[setid].missingblockcount : 1);

                // Start at an offset of 0 within a block.
                ulong blockoffset = 0;
                while (blockoffset < setids[setid].MainPacket.blocksize) // Continue until the end of the block.
                {
                    // Work out how much data to process this time.
                    uint blocklength = (uint)Math.Min((ulong)setids[setid].chunksize, setids[setid].MainPacket.blocksize - blockoffset);

                    // Read source data, process it through the RS matrix and write it to disk.
                    if (!setids[setid].ProcessData(blockoffset, blocklength))
                    {
                        // Delete all of the partly reconstructed files
                        setids[setid].DeleteIncompleteTargetFiles();
                        return ParResult.FileIOError;
                    }

                    // Advance to the need offset within each block
                    blockoffset += blocklength;
                }

                //  if (noiselevel > CommandLine::nlSilent)
                //    cout << endl << "Verifying repaired files:" << endl << endl;

                // Verify that all of the reconstructed target files are now correct
                if (!setids[setid].VerifyTargetFiles())
                {
                    // Delete all of the partly reconstructed files
                    setids[setid].DeleteIncompleteTargetFiles();
                    return ParResult.FileIOError;
                }
            }

            // Are all of the target files now complete?
            if (setids[setid].completefilecount < setids[setid].MainPacket.recoverablefilecount)
            {
                //cerr << "Repair Failed." << endl;
                return ParResult.RepairFailed;
            }
            else
            {
                //if (noiselevel > CommandLine::nlSilent)
                //  cout << endl << "Repair complete." << endl;

                return ParResult.Success;
            }
        }

        private ulong GetMemoryLimit()
        {
            //TODO : to rewrite
            // Assume 128 MB

            return 128 * 1048576;
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

            if (!setids[setid].AllocateSourceBlocks())
                return ParResult.LogicError;

            if (!setids[setid].PrepareVerificationHashTable())
                return ParResult.LogicError;

            //if (!setids[setid].ComputeWindowTable())
            //    return ParResult.LogicError;

            // 1st a quick verify to extract basic information from files like md5 hashes to be able to match files with wrong names
            //if (!setids[setid].QuickVerifySourceFiles())
            //    return ParResult.LogicError;

            // Attempt to verify all of the source files
            if (!setids[setid].VerifySourceFiles())
                return ParResult.LogicError;

            // Find out how much data we have found
            setids[setid].UpdateVerificationResults();

            // Check the verification results and report the results
            if (!setids[setid].CheckVerificationResults())
                return ParResult.RepairNotPossible;
            
            // TODO: Send return with
            // ParResult.Success || ParResult.RepairPossible || ParResult.RepairNotPossible

            return ParResult.Success;
        }
    }
}
