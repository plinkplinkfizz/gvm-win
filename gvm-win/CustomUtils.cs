using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace gvm_win
{
    internal static class CustomUtils
    {
        public static string RunCommand(string fileName, string args)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = fileName;
            start.Arguments = string.Format("{0}", args);
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;

            var sb = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = start;
                process.OutputDataReceived += (sender, eventArgs) =>
                {
                    sb.AppendLine(eventArgs.Data); //allow other stuff as well
                };
                process.ErrorDataReceived += (sender, eventArgs) => {
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

        public static T? GetJSONData<T>(string url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
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
        }

        public static string GetChecksum(string filename, HashingAlgoTypes hashingAlgoType)
        {
            using (var hasher = System.Security.Cryptography.HashAlgorithm.Create(hashingAlgoType.ToString()))
            {
                using (var stream = System.IO.File.OpenRead(filename))
                {
                    var hash = hasher.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "");
                }
            }
        }
    }

    public enum HashingAlgoTypes
    {
        MD5,
        SHA1,
        SHA256,
        SHA384,
        SHA512
    }
}
