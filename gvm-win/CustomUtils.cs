using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace gvm_win
{
    internal static class CustomUtils
    {
        public static string RunCommand(string fileName, string args)
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = $"{args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var sb = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = start;
                process.OutputDataReceived += (_, eventArgs) =>
                {
                    sb.AppendLine(eventArgs.Data); //allow other stuff as well
                };
                process.ErrorDataReceived += (_, _) => {
                };

                if (process.Start())
                {
                    process.EnableRaisingEvents = true;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();
                    //allow std out to be flushed
                    Thread.Sleep(100);
                }
            }
            return sb.ToString();
        }

        public static T? GetJsonData<T>(string url)
        {
            using HttpClient httpClient = new HttpClient();
            try
            {
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                HttpResponseMessage httpResponseMessage = httpClient.Send(httpRequestMessage);
                httpResponseMessage.EnsureSuccessStatusCode();
                Stream stream = httpResponseMessage.Content.ReadAsStream();
                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                T? result = JsonSerializer.Deserialize<T>(stream, jsonSerializerOptions);
                return result;
            }
            catch
            {
                throw new Exception("Unable to get or deserialize data!");
            }
        }

        public static string? GetChecksum(string filename, HashingAlgoTypes hashingAlgoType)
        {
            using var hasher = System.Security.Cryptography.HashAlgorithm.Create(hashingAlgoType.ToString());
            using var stream = File.OpenRead(filename);
            var hash = hasher?.ComputeHash(stream);
            if (hash != null) return BitConverter.ToString(hash).Replace("-", "");
            return null;
        }
    }

    public enum HashingAlgoTypes
    {
        Md5,
        Sha1,
        Sha256,
        Sha384,
        Sha512
    }
}
