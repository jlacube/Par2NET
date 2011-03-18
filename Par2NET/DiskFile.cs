using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Par2NET
{
    public class DiskFile
    {
        /*private*/public string filename = string.Empty;
        private ulong filesize = 0;
        private FileStream hFile = null;
        private ulong offset = 0;
        //private bool exists = false;

        public DiskFile()
        {
        }

        public DiskFile(string _filename)
        {
            filename = _filename;
        }

        internal bool Rename(string _filename)
        {

            try
            {
                File.Move(filename, _filename);
                filename = _filename;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal bool Rename()
        {
            uint index = 1;
            string newname = filename + "." + index;

            while (File.Exists(newname))
            {
                ++index;
                newname = filename + "." + index;
            }

            return Rename(newname);
        }

        // Create new file on disk and make sure that there is enough
        // space on disk for it.
        internal bool Create(string _filename, ulong _filesize)
        {
            return CreateOrOpen(_filename, _filesize, true);
        }

        private bool CreateOrOpen(string _filename, ulong _filesize, bool create)
        {
            filename = _filename;
            filesize = _filesize;

            try
            {
                if (create)
                {
                    hFile = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    hFile.SetLength((long)filesize);
                    offset = filesize;
                }
                else
                {
                    hFile = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
            }
            catch (Exception)
            {
                if (!hFile.SafeFileHandle.IsClosed)
                    hFile.Close();

                if (File.Exists(filename))
                    File.Delete(filename);

                return false;
            }
            
            //exists = true;

            return true;
        }

        private bool IsFileHandleAccessible()
        {
            return hFile != null && hFile.SafeFileHandle != null && (!hFile.SafeFileHandle.IsClosed && !hFile.SafeFileHandle.IsInvalid);
        }

        internal bool IsOpen()
        {
            return IsFileHandleAccessible();
        }

        internal void Close()
        {
            if (IsOpen())
            {
                hFile.Close();
                hFile = null;
            }
        }

        internal void Delete()
        {
            Close();

            File.Delete(filename);
        }

        internal bool Open()
        {
            string _filename = filename;

            return Open(_filename);
        }

        internal bool Open(string _filename)
        {
            return CreateOrOpen(_filename, GetFileSize(_filename), false);
        }

        internal bool Open(string _filename, ulong _filesize)
        {
            return CreateOrOpen(_filename, _filesize, false);
        }

        private ulong GetFileSize(string _filename)
        {
            return (ulong)new FileInfo(_filename).Length;
        }

        // Read some data from disk
        internal bool Read(ulong _offset, byte[] buffer, uint length)
        {
            if (!IsFileHandleAccessible())
                return false;

            if (offset != _offset)
            {
                hFile.Position = (long)_offset;

                if (hFile.Position != (long)_offset)
                    return false;

                offset = _offset;
            }

            int want = (int)length;
            int got = 0;

            // Read the data
            got = hFile.Read(buffer, 0, want);

            if (got != length)
                return false;

            offset += length;

            return true;
        }

        // Write some data to disk
        internal bool Write(ulong _offset, byte[] buffer, uint length)
        {
            return Write(_offset, buffer, 0, length);
        }

        // Write some data to disk
        internal bool Write(ulong _offset, byte[] buffer, ulong start, uint length)
        {
            if (!IsFileHandleAccessible())
                return false;

            if (offset != _offset)
            {
                hFile.Position = (long)_offset;

                if (hFile.Position != (long)_offset)
                    return false;

                offset = _offset;
            }

            int write = (int)length;

            try
            {
                // Write the data
                hFile.Write(buffer, (int)start, write);
                hFile.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }

            ToolKit.LogToFile(@"diskfile.log", string.Format("filename={0},offset={1},length={2}", filename, _offset, length));

            if (filename.Contains("EntLib50.chm.par2"))
            {
                ToolKit.LogArrayToFile<byte>("dump.log", buffer);
            }

            offset += length;

            if (filesize < offset)
            {
                filesize = offset;
            }

            return true;
        }
    }
}
