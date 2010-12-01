using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET
{
    public class DiskFile
    {
        internal bool Rename(string p)
        {
            throw new NotImplementedException();
        }

        internal bool Rename()
        {
            throw new NotImplementedException();
        }

        internal bool Create(string filename, ulong filesize)
        {
            throw new NotImplementedException();
        }

        internal bool IsOpen()
        {
            throw new NotImplementedException();
        }

        internal void Close()
        {
            throw new NotImplementedException();
        }

        internal void Delete()
        {
            throw new NotImplementedException();
        }

        internal bool Open()
        {
            throw new NotImplementedException();
        }

        internal bool Read(ulong fileoffset, byte[] buffer, uint want)
        {
            throw new NotImplementedException();
        }

        internal bool Write(ulong fileoffset, byte[] buffer, uint have)
        {
            throw new NotImplementedException();
        }
    }
}
