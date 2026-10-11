using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQEmu_Patcher
{
    public class PatchVersion
    {
        public string ServerName;
        public string ClientVersion;
        public int ServerDateLastUpdate;
        public Dictionary<string, string> RootFiles = new Dictionary<string, string>();
        public Dictionary<string, string> ResourceFiles = new Dictionary<string, string>();
        public Dictionary<string, string> MapFiles = new Dictionary<string, string>();
        public Dictionary<string, string> SoundFiles = new Dictionary<string, string>();
        public Dictionary<string, string> SpellEffectFiles = new Dictionary<string, string>();
        public Dictionary<string, string> StorylineFiles = new Dictionary<string, string>();
     //   public Dictionary<string, string> UIFiles = new Dictionary<string, string>();
     //   public Dictionary<string, string> AtlasFiles = new Dictionary<string, string>();

    }
}
