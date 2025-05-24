using System.Security.Cryptography;
using System.Text;

namespace ClusterManager.Common.Utils;

public static class HashGenerator
{
    public static string GetMd5HashString(string input)
    {
        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
