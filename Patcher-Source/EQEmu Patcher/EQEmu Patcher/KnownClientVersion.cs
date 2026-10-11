using System;

namespace EQEmu_Patcher
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class KnownClientVersion : Attribute
    {
        public string Value { get; set; }
        public KnownClientVersion(string value)
        {
            Value = value;
        }
    }
}

