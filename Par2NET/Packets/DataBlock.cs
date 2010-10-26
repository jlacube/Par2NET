using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Packets
{
    public class DataBlock
    {
        public ulong Offset = 0;
        public ulong Length = 0;
        public string FileName = string.Empty;
    }
}
