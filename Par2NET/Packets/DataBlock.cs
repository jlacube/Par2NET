using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    public class DataBlock
    {
        public ulong offset = 0;
        public ulong length = 0;
        public DiskFile diskfile = null;

        // Set the length of the block
        internal void SetLength(ulong _length)
        {
            length = _length;
        }

        // Set the location of the block
        internal void SetLocation(DiskFile _diskfile, ulong _offset)
        {
            diskfile = _diskfile;
            offset = _offset;
        }

        // Clear the location of the block
        internal void ClearLocation()
        {
            diskfile = null;
            offset = 0;
        }

        // Check to see of the location is known
        internal bool IsSet()
        {
            return (diskfile != null);
        }

        // Open the file associated with the data block if is not already open
        internal bool Open()
        {
            if (diskfile == null)
                return false;

            if (diskfile.IsOpen())
                return true;

            return diskfile.Open();
        }

        // Which disk file is this data block in
        internal DiskFile GetDiskFile()
        {
            return diskfile;
        }

        // What offset is the block located at
        internal ulong GetOffset()
        {
            return offset;
        }

        // What is the length of this block
        internal ulong GetLength()
        {
            return length;
        }

        // Read some data at a specified position within a data block
        // into a buffer in memory
        internal bool ReadData(ulong position, // Position within the block
                                 uint size,     // Size of the memory buffer
                                 byte[] buffer)   // Pointer to memory buffer
        {

            // Check to see if the position from which data is to be read
            // is within the bounds of the data block
            if (length > position)
            {
                // Compute the file offset and how much data to physically read from disk
                ulong fileoffset = offset + position;
                uint want = (uint)Math.Min((ulong)size, length - position);

                if (want < buffer.Length)
                    Array.Clear(buffer, 0, buffer.Length);

                // Read the data from the file into the buffer
                if (!diskfile.Read(fileoffset, buffer, want))
                    return false;

                // If the read extends beyond the end of the data block,
                // then the rest of the buffer is zeroed.
            }
            else
            {
                // Zero the whole buffer
                buffer = new byte[size];
            }

            return true;
        }

        // Write some data at a specified position within a datablock
        // from memory to disk
        internal bool WriteData(ulong position, // Position within the block
                                  uint size,     // Size of the memory buffer
                                  byte[] buffer,   // Pointer to memory buffer
                                  out uint wrote)    // Amount actually written
        {
            wrote = 0;

            // Check to see if the position from which data is to be written
            // is within the bounds of the data block
            if (length > position)
            {
                // Compute the file offset and how much data to physically write to disk
                ulong fileoffset = offset + position;
                uint have = (uint)Math.Min((ulong)size, length - position);

                // Write the data from the buffer to disk
                if (!diskfile.Write(fileoffset, buffer, have))
                    return false;

                wrote = have;
            }

            return true;
        }

        internal bool WriteData(ulong position, // Position within the block
                                  uint size,     // Size of the memory buffer
                                  byte[] buffer,   // Pointer to memory buffer
                                  ulong start,    // Start index for the buffer
                                  out uint wrote)    // Amount actually written
        {
            wrote = 0;

            // Check to see if the position from which data is to be written
            // is within the bounds of the data block
            if (length > position)
            {
                // Compute the file offset and how much data to physically write to disk
                ulong fileoffset = offset + position;
                uint have = (uint)Math.Min((ulong)size, length - position);

                // Write the data from the buffer to disk
                if (!diskfile.Write(fileoffset, buffer, start, have))
                    return false;

                wrote = have;
            }

            return true;
        }
    }
}
