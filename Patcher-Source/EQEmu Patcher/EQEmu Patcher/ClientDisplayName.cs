using System;

namespace EQEmu_Patcher
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ClientDisplayName : Attribute
    {
        public string Value { get; set; }
        public ClientDisplayName(string value)
        {
            Value = value;
        }
    }
}

