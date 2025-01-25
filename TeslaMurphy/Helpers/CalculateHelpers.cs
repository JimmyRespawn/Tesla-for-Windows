using System;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace TeslaMurphy.Helpers
{
    public class CalculateHelpers
    {
        public static async Task<string> GetHashAlgorithmNamesFromFile(StorageFile file)
        {
            //Currently is using for OneDrive update n download
            //NB: "file" is a "StorageFile" previously opened
            //in this example I use HashAlgorithmNames.Md5, you can replace it with HashAlgorithmName.Sha1, etc...
            try
            {
                HashAlgorithmProvider alg = Windows.Security.Cryptography.Core.HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
                var stream = await file.OpenStreamForReadAsync();
                var inputStream = stream.AsInputStream();
                uint capacity = 100000000;
                Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(capacity);
                var hash = alg.CreateHash();

                while (true)
                {
                    await inputStream.ReadAsync(buffer, capacity, InputStreamOptions.None);
                    if (buffer.Length > 0)
                        hash.Append(buffer);
                    else
                        break;
                }

                string hashText = CryptographicBuffer.EncodeToHexString(hash.GetValueAndReset()).ToUpper();

                inputStream.Dispose();
                stream.Dispose();
                return hashText;
            }
            catch
            {
                return null;
            }
        }

        public static Task<double> MilesToKM(double miles)
        {
            double km = miles * 1.61;
            return Task.FromResult(km);
        }
    }
}
