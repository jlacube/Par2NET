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
using System.Threading;
using Par2NET.Packets;
using System.Reflection;

namespace Par2NET
{
    public class Par2Library : IParLibrary
    {
        public static readonly string PACKAGE = "Par2NET.Library";
        public static readonly string VERSION = Assembly.GetExecutingAssembly().ImageRuntimeVersion;

        public bool multithreadCPU = false;
        public bool multithreadIO = false;

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

        public Par2Library(bool _multithreadCPU, bool _multithreadIO)
        {
            multithreadCPU = _multithreadCPU;
            multithreadIO = _multithreadIO;
        }

        public Par2Library()
            : this(true, false)
        {
        }

        private Dictionary<string, Par2RecoverySet> setids = new Dictionary<string, Par2RecoverySet>();

        private static object syncObject = new object();

        private void UpdateOrAddRecoverySet(Par2RecoverySet recoverySet)
        {
            lock (syncObject)
            {
                string setid = ToolKit.ToHex(recoverySet.MainPacket.header.setid);

                if (!setids.Keys.Contains(setid))
                    setids.Add(setid, recoverySet);
                else
                {
                    UpdateRecoverySet(setids[setid], recoverySet);
                }
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

        public ParResult Process(Par2LibraryArguments args)
        {
            Par2Library.targetPath = args.targetPath;

            List<string> inputFiles = new List<string>(args.inputFiles);
            List<string> recoveryFiles = new List<string>(args.recoveryFiles);

            if (args.version == ParVersion.Par1)
                return Par1Library().Process(inputFiles, recoveryFiles, args.action);

            if (args.action != ParAction.ParCreate)
                GetRecoveryFiles(recoveryFiles);

            switch (args.action)
            {
                case ParAction.ParCreate:
                    return Create(ref inputFiles, ref recoveryFiles, args);
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

        private ParResult Create(ref List<string> inputFiles, ref List<string> recoveryFiles, Par2LibraryArguments args)
        {
            // Initialize the base par2 filename if not set in the command line
            if (args.par2filename == string.Empty)
            {
                //args.par2filename = args.inputFiles[0] + ".par2";
                args.par2filename = args.inputFiles[0];
            }

            Par2RecoverySet recoverySet = new Par2RecoverySet(args.multithreadCPU, args.multithreadIO, args);

            // Compute block size from block count or vice versa depending on which was
            // specified on the command line
            if (!recoverySet.ComputeBlockSizeAndBlockCount(ref inputFiles))
                return ParResult.InvalidCommandLineArguments;

            // Determine how many recovery blocks to create based on the source block
            // count and the requested level of redundancy.
            if (recoverySet.redundancy > 0 && !recoverySet.ComputeRecoveryBlockCount(recoverySet.redundancy))
                return ParResult.InvalidCommandLineArguments;

            // Determine how much recovery data can be computed on one pass
            if (!recoverySet.CalculateProcessBlockSize(GetMemoryLimit()))
                return ParResult.LogicError;

            // Determine how many recovery files to create.
            if (!recoverySet.ComputeRecoveryFileCount())
                return ParResult.InvalidCommandLineArguments;

            //if (noiselevel > CommandLine::nlQuiet)
            //{
            // Display information.
            Console.WriteLine("Block size: " + recoverySet.MainPacket.blocksize);
            Console.WriteLine("Source file count: " + recoverySet.SourceFiles.Count);
            Console.WriteLine("Source block count: " + recoverySet.sourceblockcount);
            if (recoverySet.redundancy > 0 || recoverySet.recoveryblockcount == 0)
                Console.WriteLine("Redundancy: " + recoverySet.redundancy + '%');
            Console.WriteLine("Recovery block count: " + recoverySet.recoveryblockcount);
            Console.WriteLine("Recovery file count: " + recoverySet.recoveryfilecount);
            //}

            // Open all of the source files, compute the Hashes and CRC values, and store
            // the results in the file verification and file description packets.
            if (!recoverySet.OpenSourceFiles(ref inputFiles))
                return ParResult.FileIOError;

            // Create the main packet and determine the setid to use with all packets
            //if (!recoverySet.CreateMainPacket(Par2LibraryArguments args))
            //  return ParResult.LogicError;

            // Create the creator packet.
            if (!recoverySet.CreateCreatorPacket())
                return ParResult.LogicError;

            // Initialise all of the source blocks ready to start reading data from the source files.
            if (!recoverySet.CreateSourceBlocks())
                return ParResult.LogicError;

            // Create all of the output files and allocate all packets to appropriate file offets.
            if (!recoverySet.InitialiseOutputFiles(args.par2filename))
                return ParResult.FileIOError;

            if (recoverySet.recoveryblockcount > 0)
            {
                // Allocate memory buffers for reading and writing data to disk.
                if (!recoverySet.AllocateBuffers())
                    return ParResult.MemoryError;

                // Compute the Reed Solomon matrix
                if (!recoverySet.ComputeRSMatrix())  //TODO: Unify and switch for Create and Verify/Repair
                    return ParResult.LogicError;

                // Set the total amount of data to be processed.
                /*progress = 0;
                totaldata = blocksize * sourceblockcount * recoveryblockcount;
                previouslyReportedFraction = -10000000;	// Big negative*/

                // Start at an offset of 0 within a block.
                ulong blockoffset = 0;
                while (blockoffset < recoverySet.MainPacket.blocksize) // Continue until the end of the block.
                {
                    // Work out how much data to process this time.
                    ulong blocklength = (ulong)Math.Min(recoverySet.chunksize, recoverySet.MainPacket.blocksize - blockoffset);

                    // Read source data, process it through the RS matrix and write it to disk.
                    if (!recoverySet.ProcessData(blockoffset, blocklength))
                        return ParResult.FileIOError;

                    blockoffset += blocklength;
                }

                //if (noiselevel > CommandLine::nlQuiet)
                //  cout << "Writing recovery packets" << endl;

                // Finish computation of the recovery packets and write the headers to disk.
                if (!recoverySet.WriteRecoveryPacketHeaders())
                    return ParResult.FileIOError;

                // Finish computing the full file hash values of the source files
                if (!recoverySet.FinishFileHashComputation())
                    return ParResult.LogicError;
            }

            // Fill in all remaining details in the critical packets.
            if (!recoverySet.FinishCriticalPackets())
                return ParResult.LogicError;

            //if (noiselevel > CommandLine::nlQuiet)
            //  cout << "Writing verification packets" << endl;

            // Write all other critical packets to disk.
            if (!recoverySet.WriteCriticalPackets())
                return ParResult.FileIOError;

            // Close all files.
            if (!recoverySet.CloseFiles())
                return ParResult.FileIOError;

            //if (noiselevel > CommandLine::nlSilent)
            //  cout << "Done" << endl;

            return ParResult.Success;
        }

        private ParResult Repair(ref List<string> inputFiles, ref List<string> recoveryFiles)
        {
            ParResult verifyResult = Verify(ref inputFiles, ref recoveryFiles);

            if (verifyResult != ParResult.Success && verifyResult != ParResult.RepairPossible)
                return verifyResult;

            // Add code to find setid propelry
            string setid = setids.Keys.First();

            // Rename any damaged or missnamed target files.
            if (!setids[setid].RenameTargetFiles())
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
                    //setids[setid].DeleteIncompleteTargetFiles();
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
            try
            {
                if (!multithreadIO)
                {
                    //ST
                    foreach (string recoveryFile in recoveryFiles)
                    {
                        Par2FileReader reader = new Par2FileReader(recoveryFile, multithreadCPU, multithreadIO);
                        UpdateOrAddRecoverySet(reader.ReadRecoverySet());
                    }
                }
                else
                {
                    //MT : OK
                    using (Semaphore concurrencySemaphore = new Semaphore(0,4))
                    {
                        List<Task> tasks = new List<Task>();
                        foreach (string recoveryFile in recoveryFiles)
                        {
                            concurrencySemaphore.WaitOne();
                            tasks.Add(Task.Factory.StartNew((f) =>
                            {
                                try
                                {
                                    string file = (string)f;
                                    Par2FileReader reader = new Par2FileReader(file, multithreadCPU, multithreadIO);
                                    UpdateOrAddRecoverySet(reader.ReadRecoverySet());
                                }
                                finally
                                {
                                    concurrencySemaphore.Release();
                                }
                            }, recoveryFile));
                        }

                        Task.WaitAll(tasks.ToArray());
                    }                   
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
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
            if (!setids[setid].CheckVerificationResults(false))
                return ParResult.RepairNotPossible;
            
            // TODO: Send return with
            // ParResult.Success || ParResult.RepairPossible || ParResult.RepairNotPossible

            return ParResult.Success;
        }
    }
}
