using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace Par2NET
{
    // Maintain state information to be passed to EndReadCallback.
    public class FileCheckerState
    {
        // fStream is used to read and write to the file.
        FileStream fStream;

        // readArray stores data that is read from the file.
        byte[] readArray;

        CheckBufferState state;

        // manualEvent signals the main thread 
        // when verification is complete.
        ManualResetEvent manualEvent;

        public FileCheckerState(FileStream fStream, byte[] readArray, ManualResetEvent manualEvent, CheckBufferState state)
        {
            this.fStream   = fStream;
            this.readArray = readArray;
            this.manualEvent = manualEvent;
            this.state = state;
        }

        public FileStream FStream
        { get { return fStream; } }

        public byte[] ReadArray
        { get { return readArray; } }

        public ManualResetEvent ManualEvent
        { get { return manualEvent; } }

        public CheckBufferState State
        { get { return state; } }
    }
}
