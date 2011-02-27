using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Interfaces
{
    [Flags]
    public enum ParResult
    {
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

    public enum ParScheme
    {
        Unknown = 0,
        Variable,      // Each PAR2 file will have 2x as many blocks as previous
        Limited,       // Limit PAR2 file size
        Uniform        // All PAR2 files the same size
    } ;

    public enum ParVersion
    {
        Par1,       // PAR1 version
        Par2        // PAR2 version
    };

    public enum ParAction
    {
        ParCreate,      // PAR creation
        ParVerify,      // PAR verify
        ParRepair       // PAR repair
    };

    public enum PacketType
    {
        Unknown = 0,
        MagicPacket,
        CreatorPacket,
        MainPacket,
        DescriptionPacket,
        VerificationPacket,
        RecoveryPacket
    };

    public enum MatchType
    {
        NoMatch = 0,
        PartialMatch,
        FullMatch
    };

    public enum NoiseLevel
    {
        Unknown = 0,
        Silent,       // Absolutely no output (other than errors)
        Quiet,        // Bare minimum of output
        Normal,       // Normal level of output
        Noisy,        // Lots of output
        Debug         // Extra debugging information
    };
}
