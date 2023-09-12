using Newtonsoft.Json;


namespace gvm_win
{
    internal sealed class GvmConfig
    {
        private const string SaveFile = ".gvm-win";
        private const string DataDirectory = "gvm-win-data";

        private static readonly string SaveFileLocation;

        private static readonly GvmConfig? Instance;

        static GvmConfig() 
        {
            dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DataDirectory);
            installations = new List<GoInstallation>();

            string userSaveFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), SaveFile);
            string localSaveFile = Path.Combine(AppContext.BaseDirectory, SaveFile);

            if (File.Exists(localSaveFile)) { SaveFileLocation = localSaveFile; }
            else if (File.Exists(userSaveFile)) { SaveFileLocation = userSaveFile; }
            else { SaveFileLocation = userSaveFile; Save(); }

            try
            {
                string config = File.ReadAllText(SaveFileLocation);
                Instance = JsonConvert.DeserializeObject<GvmConfig>(config);
            }
            catch { throw new Exception($"Unable to read file {SaveFileLocation}!"); }

            if (!Directory.Exists(dataDirectory))
            {
                try { Directory.CreateDirectory(dataDirectory); }
                catch { throw new Exception($"Unable to create data directory {dataDirectory}!"); }
            }
        }

        [JsonProperty] public static string dataDirectory { get; set; }

        [JsonProperty] public static List<GoInstallation> installations { get; set; }

        [JsonProperty] public static string current { get; set; } = String.Empty;

        [JsonProperty] public static string currentBinPath { get; set; } = String.Empty;

        public static void Save()
        {
            try { File.WriteAllText(SaveFileLocation, JsonConvert.SerializeObject(Instance)); }
            catch { throw new Exception("Unable to save file!"); }
        }
    }

    internal class GoInstallation
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool Local { get; set; }
        public bool Stable { get; set; }
    }
}
