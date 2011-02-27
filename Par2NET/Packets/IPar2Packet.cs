using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    public interface IPar2Packet
    {
        //bool WritePacket(DiskFile diskfile, ulong offset);
        ulong PacketLength();

        void FinishPacket(byte[] setid);
    }
}
