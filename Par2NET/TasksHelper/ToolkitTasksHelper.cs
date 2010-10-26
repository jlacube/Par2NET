using System;
using System.Text;

namespace Par2NET.Tasks
{
    public static partial class TasksHelper
    {
        public static string ByteArrayToString(byte[] input)
        {
            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                sBuilder.Append(input[i].ToString("x2"));
            }

            return sBuilder.ToString();
        }

        public static bool CompareHashes(string hash1, string hash2)
        {
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            if (0 == comparer.Compare(hash1, hash2))
                return true;
            else
                return false;
        }
    }
}
