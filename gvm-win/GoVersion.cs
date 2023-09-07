using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gvm_win
{
    internal class GoVersion
    {
        public string version {  get; set; }
        public bool stable { get; set; }
        public List<GoVersionFile> files { get; set; }
    }

    public class GoVersionFile
    {
        public string filename { get; set; }
        public string os {  get; set; }
        public string arch { get; set; }
        public string version { get; set; }
        public string sha256 { get; set; }
        public ulong size { get; set; }
        public string kind { get; set; }
    }
}
