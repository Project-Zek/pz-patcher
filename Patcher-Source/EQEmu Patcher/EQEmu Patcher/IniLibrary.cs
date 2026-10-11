using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Windows.Forms;

namespace EQEmu_Patcher
{
    class IniLibrary
    {
        public static IniLibrary instance;
        public string AutoPatch { get; set; }
        public string AutoPlay { get; set; }
        public VersionTypes ClientVersion { get; set; }
        public string LastPatchedVersion { get; set; }
        public string GraphicsMode { get; set; }
        public string FrameLimit { get; set; }
        public string DisplayResolution { get; set; }


        public static void Save()
        {
            var serializerBuilder = new SerializerBuilder().WithNamingConvention(new CamelCaseNamingConvention());
            var serializer = serializerBuilder.Build();
            string body = serializer.Serialize(instance);

            Console.WriteLine(body);
            File.WriteAllText($"{System.IO.Path.GetDirectoryName(Application.ExecutablePath)}\\eqemupatcher.yml", body);
        }

        public static void Load()
        {
            string path = $"{System.IO.Path.GetDirectoryName(Application.ExecutablePath)}\\eqemupatcher.yml";
            bool needsReset = false;
            try
            {
                if (!File.Exists(path))
                {
                    needsReset = true;
                }
                else
                {
                    // Read to memory first to avoid locking the file while we decide to rewrite it
                    string yaml = File.ReadAllText(path);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(new CamelCaseNamingConvention())
                        .IgnoreUnmatchedProperties()
                        .Build();

                    try
                    {
                        instance = deserializer.Deserialize<IniLibrary>(yaml);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed parsing config, resetting: {ex.Message}");
                        needsReset = true;
                    }

                    if (instance == null)
                    {
                        needsReset = true;
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"Failed loading config: {e.Message}");
                needsReset = true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error loading config: {e.Message}");
                needsReset = true;
            }

            if (needsReset)
            {
                ResetDefaults();
                try
                {
                    Save();
                }
                catch (IOException ioex)
                {
                    Console.WriteLine($"Failed to save default config (in use?): {ioex.Message}");
                }
            }

            if (instance.AutoPatch == null) instance.AutoPatch = "false";
            if (instance.AutoPlay == null) instance.AutoPlay = "false";
        }

        public static void ResetDefaults()
        {
            instance = new IniLibrary();
            instance.AutoPlay = "false";
            instance.AutoPatch = "false";
        }
    }
}
