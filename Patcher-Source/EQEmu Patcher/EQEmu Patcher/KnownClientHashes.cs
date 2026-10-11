using System;

namespace EQEmu_Patcher
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class KnownClientHashes : Attribute
    {
        public string Value { get; set; }
        public KnownClientHashes(string value)
        {
            Value = value;
        }
    }
}

