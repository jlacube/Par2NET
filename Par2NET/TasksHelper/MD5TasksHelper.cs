using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Par2NET.Tasks
{
    public static partial class TasksHelper
    {
        public static Task<byte[]> ComputeMD5Hash(string filename)
        {
            return Task<byte[]>.Factory.StartNew((str) =>
                {
                    MD5 md5Hasher = MD5.Create();

                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return md5Hasher.ComputeHash(fs);
                    }
                }, filename);
        }

        public static Task<string> ComputeMD5HashStr(string filename)
        {
            return ComputeMD5Hash(filename).ContinueWith((MD5HashTask) => 
                {
                    return ByteArrayToString(MD5HashTask.Result);
                });
        }

        public static Task<bool> VerifyMD5HashStr(string filename, string verifyHash)
        {
            return ComputeMD5HashStr(filename).ContinueWith((MD5HashStrTask) =>
                {
                    return CompareHashes(MD5HashStrTask.Result, verifyHash);
                });
        }

        public static Task<bool> VerifyMD5Hash(string filename, string verifyFilename)
        {
            return VerifyMD5HashStr(filename, ComputeMD5HashStr(verifyFilename).Result);
        }
    }
}
