using System.IO;
using System.Threading.Tasks;
using CRC32NET;

namespace Par2NET.Tasks
{
    public static partial class TasksHelper
    {
        public static Task<byte[]> ComputeCRC32Hash(string filename)
        {
            return Task<byte[]>.Factory.StartNew((str) =>
            {
                CRC32 crc32Hasher = new CRC32();

                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return crc32Hasher.ComputeHash(fs);
                }
            }, filename);
        }

        public static Task<string> ComputeCRC32HashStr(string filename)
        {
            return ComputeCRC32Hash(filename).ContinueWith((CRC32HashTask) =>
            {
                return ByteArrayToString(CRC32HashTask.Result);
            });
        }

        public static Task<bool> VerifyCRC32HashStr(string filename, string verifyHash)
        {
            return ComputeCRC32HashStr(filename).ContinueWith((CRC32HashStrTask) =>
            {
                return CompareHashes(CRC32HashStrTask.Result, verifyHash);
            });
        }

        public static Task<bool> VerifyCRC32Hash(string filename, string verifyFilename)
        {
            return VerifyCRC32HashStr(filename, ComputeCRC32HashStr(verifyFilename).Result);
        }
    }
}
