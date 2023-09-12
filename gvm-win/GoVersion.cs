namespace gvm_win
{
    internal class GoVersion
    {
        public string version {  get; set; } = string.Empty;
        public bool stable { get; set; }
        public List<GoVersionFile> files { get; set; } = new List<GoVersionFile>();
    }

    public class GoVersionFile
    {
        public string filename { get; set; } = string.Empty;
        public string os { get; set; } = string.Empty;
        public string arch { get; set; } = string.Empty;
        public string version { get; set; } = string.Empty;
        public string sha256 { get; set; } = string.Empty;
        public ulong size { get; set; }
        public string kind { get; set; } = string.Empty;
    }
}
