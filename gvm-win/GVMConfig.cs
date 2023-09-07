using Newtonsoft.Json;


namespace gvm_win
{
    internal sealed class GVMConfig
    {
        private const string SAVE_FILE = ".gvm-win";
        private const string DATA_DIRECTORY = "gvm-win-data";

        private static string saveFileLocation;

        private static GVMConfig? instance = new();

        public static GVMConfig? Instance { get {  return instance; } }

        static GVMConfig() 
        {
            dataDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DATA_DIRECTORY);
            installations = new List<GoInstallation>();

            string userSaveFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), SAVE_FILE);
            string localSaveFile = System.IO.Path.Combine(AppContext.BaseDirectory, SAVE_FILE);

            if (File.Exists(localSaveFile)) { saveFileLocation = localSaveFile; }
            else if (File.Exists(userSaveFile)) { saveFileLocation = userSaveFile; }
            else { saveFileLocation = userSaveFile; Save(); }

            try
            {
                string config = File.ReadAllText(saveFileLocation);
                instance = JsonConvert.DeserializeObject<GVMConfig>(config);
            }
            catch { throw new Exception($"Unable to read file {saveFileLocation}!"); }

            if (!Directory.Exists(dataDirectory))
            {
                try { Directory.CreateDirectory(dataDirectory); }
                catch { throw new Exception($"Unable to create data directory {dataDirectory}!"); }
            }
        }

        [JsonProperty]
        public static string dataDirectory { get; set; }

        [JsonProperty]
        public static List<GoInstallation> installations { get; set; }

        [JsonProperty]
        public static string current { get; set; }

        [JsonProperty]
        public static string currentBinPath { get; set; }

        public static void Save()
        {
            try { File.WriteAllText(saveFileLocation, JsonConvert.SerializeObject(instance)); }
            catch { throw new Exception("Unable to savefile!"); }
        }
    }

    internal class GoInstallation
    {
        public string Id { get; set; } = "";
        public string Version { get; set; } = "";
        public string Path { get; set; } = "";
        public bool Local { get; set; }
        public bool Stable { get; set; }
    }
}
