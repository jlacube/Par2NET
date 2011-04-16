using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace FastCRC32
{
    public class FastCRC32
    {
        private static uint[] crcTable = null;
        private static uint[] windowTable = null;

        public uint windowMask = 0;

        //static FastCRC32()
        //{
        //    unchecked
        //    {
        //        if (crcTable == null)
        //        {
        //            crcTable = new uint[256];

        //            uint polynomial = 0xEDB88320;

        //            for (uint i = 0; i <= 255; i++)
        //            {
        //                uint crc = i;

        //                for (uint j = 0; j < 8; j++)
        //                {
        //                    crc = (crc >> 1) ^ ((crc & 1) != 0 ? polynomial : 0);
        //                }

        //                crcTable[i] = crc;
        //            }

        //            //Parallel.For(0, 255, i =>
        //            //    {
        //            //        uint crc = (uint)i;

        //            //        for (uint j = 0; j < 8; j++)
        //            //        {
        //            //            crc = (crc >> 1) ^ ((crc & 1) != 0 ? polynomial : 0);
        //            //        }

        //            //        crcTable[i] = crc;
        //            //    });
        //        }
        //    }
        //}

        private static object _syncCRC32 = new object();

        private static FastCRC32 _crc32 = null;

        private static ManualResetEvent _mre = new ManualResetEvent(false);

        private static bool initializing = false;

        public static FastCRC32 GetCRC32Instance(ulong window)
        {
            if (_crc32 == null)
            {
                lock (_syncCRC32)
                {
                    if (initializing)
                        _mre.WaitOne();

                    initializing = true;

                    if (_crc32 == null)
                    {
                        _crc32 = new FastCRC32(window);
                    }
                }
            }

            return _crc32;
        }

        protected FastCRC32(ulong window)
        {
            Task.Factory.StartNew(() =>
            {
                GenerateCRC32Table();
                GenerateWindowTable(window);
                windowMask = ComputeWindowMask(window);
                _mre.Set();
            });
        }

        private void GenerateCRC32Table()
        {
            unchecked
            {
                if (crcTable == null)
                {
                    crcTable = new uint[256];

                    uint polynomial = 0xEDB88320;

                    for (uint i = 0; i <= 255; i++)
                    {
                        uint crc = i;

                        for (uint j = 0; j < 8; j++)
                        {
                            crc = (crc >> 1) ^ ((crc & 1) != 0 ? polynomial : 0);
                        }

                        crcTable[i] = crc;
                    }

                    //Parallel.For(0, 255, i =>
                    //    {
                    //        uint crc = (uint)i;

                    //        for (uint j = 0; j < 8; j++)
                    //        {
                    //            crc = (crc >> 1) ^ ((crc & 1) != 0 ? polynomial : 0);
                    //        }

                    //        crcTable[i] = crc;
                    //    });
                }
            }
        }

        private void GenerateWindowTable(ulong window)
        {
            unchecked
            {
                windowTable = new uint[256];

                //for (uint i = 0; i <= 255; i++)
                //{
                //    uint crc = crcTable[i];

                //    for (uint j = 0; j < window; j++)
                //    {
                //        crc = ((crc >> 8) & 0x00ffffff) ^ crcTable[(byte)crc];
                //    }

                //    windowTable[i] = crc;
                //}

                Parallel.For(0, 255, i =>
                    {
                        uint crc = crcTable[i];

                        for (uint j = 0; j < window; j++)
                        {
                            crc = ((crc >> 8) & 0x00ffffff) ^ crcTable[(byte)crc];
                        }

                        windowTable[i] = crc;
                    });
            }
        }

        uint ComputeWindowMask(ulong window)
        {
            unchecked
            {
                uint result = 0xFFFFFFFF;

                while (window > 0)
                {
                    result = CRCUpdateChar(result, (char)0);

                    window--;
                }

                //result ^= ~0;
                result ^= 0xFFFFFFFF;

                windowMask = result;

                return windowMask;
            }
        }

        private uint CRCUpdateChar(uint crc, char ch)
        {
            unchecked
            {
                return ((crc >> 8) & 0x00ffffff) ^ crcTable[(byte)crc ^ ch];
            }
        }

        public uint CRCUpdateBlock(uint crc, uint length, byte[] buffer, uint start)
        {
            _mre.WaitOne();
            unchecked
            {
                while (length-- > 0)
                {
                    crc = crcTable[(crc ^ buffer[start++]) & 0xFF] ^ (crc >> 8);
                }

                return crc;
            }
        }

        public uint CRCUpdateBlock(uint crc, uint length)
        {
            _mre.WaitOne();
            unchecked
            {
                while (length-- > 0)
                {
                    crc = ((crc >> 8) & 0x00ffffff) ^ crcTable[(byte)crc];
                }

                return crc;
            }
        }

        // Slide the CRC along a buffer by one character (removing the old and adding the new).
        // The new character is added using the main CCITT CRC32 table, and the old character
        // is removed using the windowtable.
        public uint CRCSlideChar(uint crc, byte chNew, byte chOld)
        {
            _mre.WaitOne();
            unchecked
            {
                return ((crc >> 8) & 0x00ffffff) ^ crcTable[(byte)crc ^ chNew] ^ windowTable[chOld];
            }
        }


        // Usage
        /*

          char *buffer;
          u64 window;

          //...

          u32 windowtable[256];
          GenerateWindowTable(window, windowtable);
          u32 windowmask = ComputeWindowMask(window);

          u32 crc = ~0 ^ CRCUpdateBlock(~0, window, buffer);
          crc = windowmask ^ CRCSlideChar(windowmask ^ crc, buffer[window], buffer[0], windowtable);

          assert(crc == ~0 ^ CRCUpdateBlock(~0, window, buffer+1));

        */
    }
}
