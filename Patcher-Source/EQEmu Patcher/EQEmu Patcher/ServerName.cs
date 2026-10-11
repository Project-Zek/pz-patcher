using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQEmu_Patcher
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ServerName : Attribute
    {
        public string Value { get; set; }
        public ServerName(string value)
        {
            Value = value;
        }
    }
}
