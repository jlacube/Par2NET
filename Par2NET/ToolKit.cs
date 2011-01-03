using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Globalization;

namespace Par2NET
{
    public class ToolKit
    {
        public static byte[] InitByteArrayFromChars(params char[] chars)
        {
            byte[] result = new byte[chars.Length];

            for (uint i = 0; i < chars.Length; i++)
            {
                result[i] = (byte)chars[i];
            }

            return result;
        }

        public static int IndexOf(byte[] ByteArrayToSearch, byte[] ByteArrayToFind)
        {
            return IndexOf(ByteArrayToSearch, ByteArrayToFind, 0);
        }

        public static int OldIndexOf(byte[] ByteArrayToSearch, byte[] ByteArrayToFind, int start_index)
        {
            // Any encoding will do, as long as all bytes represent a unique character.
            Encoding encoding = Encoding.GetEncoding(1252);

            string toSearch = encoding.GetString(ByteArrayToSearch, 0, ByteArrayToSearch.Length);
            string toFind = encoding.GetString(ByteArrayToFind, 0, ByteArrayToFind.Length);
            int result = toSearch.IndexOf(toFind, start_index, StringComparison.Ordinal);

            return result;
        }

        public static int IndexOf(byte[] ByteArrayToSearch, byte[] ByteArrayToFind, int startIndex)
        {
            int found = 0;
            for (int i = startIndex; i < ByteArrayToSearch.Length; i++)
            {
                if (ByteArrayToSearch[i] == ByteArrayToFind[found])
                {
                    if (++found == ByteArrayToFind.Length)
                    {
                        return i - found + 1;
                    }
                }
                else
                {
                    found = 0;
                }
            }
            return -1;
        }

        public static T ReadStruct<T>(byte[] array, int start, int length)
        {
            byte[] buffer = new byte[length];

            Buffer.BlockCopy(array, start, buffer, 0, length);

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            T temp = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return temp;
        }

        public static unsafe bool UnsafeCompare(byte[] a1, byte[] a2)
        {
            if (a1 == null || a2 == null || a1.Length != a2.Length)
                return false;
            fixed (byte* p1 = a1, p2 = a2)
            {
                byte* x1 = p1, x2 = p2;
                int l = a1.Length;
                for (int i = 0; i < l / 8; i++, x1 += 8, x2 += 8)
                    if (*((long*)x1) != *((long*)x2)) return false;
                if ((l & 4) != 0) { if (*((int*)x1) != *((int*)x2)) return false; x1 += 4; x2 += 4; }
                if ((l & 2) != 0) { if (*((short*)x1) != *((short*)x2)) return false; x1 += 2; x2 += 2; }
                if ((l & 1) != 0) if (*((byte*)x1) != *((byte*)x2)) return false;
                return true;
            }
        }

        public static string ToHex(byte[] data)
        {
            StringBuilder builder = new StringBuilder();
            foreach (byte item in data) builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static string ByteArrayToString(byte[] array)
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(array);
        }
    }
}
