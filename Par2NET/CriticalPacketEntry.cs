using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Par2NET.Packets;
using System.Diagnostics;

namespace Par2NET
{
    // Class used to record the fact that a copy of a particular critical packet
    // will be written to a particular file at a specific offset.
    class CriticalPacketEntry
    {
        private DiskFile diskfile;
        private ulong offset;
        private CriticalPacket packet;

        public CriticalPacketEntry()
        {
            diskfile = null;
            offset = 0;
            packet = null;
        }

        public CriticalPacketEntry(CriticalPacketEntry other)
        {
            diskfile = other.diskfile;
            offset = other.offset;
            packet = other.packet;
        }

        public CriticalPacketEntry(DiskFile _diskFile, ulong _offset, CriticalPacket _packet)
        {
            diskfile = _diskFile;
            offset = _offset;
            packet = _packet;
        }

        public bool WritePacket()
        {
            Debug.Assert(packet != null && diskfile != null);

            // Tell the packet to write itself to disk
            return packet.WritePacket(diskfile, offset);
        }

        public ulong PacketLength()
        {
            Debug.Assert(packet != null);

            // Ask the packet how big it is.
            return packet.PacketLength();
        }
    }
}
