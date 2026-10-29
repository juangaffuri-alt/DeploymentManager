using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace PasadorScript.Helpers
{
    public static class RolesDecompressor
    {
        public static List<string> DecodeAndDecompress(string compressedBase64)
        {
            var compressedBytes = Convert.FromBase64String(compressedBase64);

            using var input = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();

            gzip.CopyTo(output);
            var json = Encoding.UTF8.GetString(output.ToArray());

            return JsonSerializer.Deserialize<List<string>>(json)!;
        }
    }
}
