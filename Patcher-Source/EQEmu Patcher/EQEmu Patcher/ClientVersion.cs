using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQEmu_Patcher
{    
    public enum VersionTypes
    {
        Unknown,
        Titanium,
        Rain_Of_Fear,
        Rain_Of_Fear_2,
        Seeds_Of_Destruction,
        Underfoot,
        Secrets_Of_Feydwer,
        Broken_Mirror
    }

    class ClientVersion
    {
        public string FullName;
        public string ShortName;

        public ClientVersion(string fName, string sName)
        {
            FullName = fName;
            ShortName = sName;
        }
    }
}
