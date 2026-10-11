using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectZekClassic {
    public sealed class MusicDefinitionRecord {
        public string path {get;set;}
        public int bytes {get;set;}
        public string sha256 {get;set;}
        public string md5 {get;set;}
    }
    public sealed class MusicDefinitionCatalog {
        public int schema {get;set;}
        public string version {get;set;}
        public List<MusicDefinitionRecord> files {get;set;}
        public List<MusicDefinitionRecord> audioFiles {get;set;}
    }
    internal static class MusicDefinitionAssets {
        static readonly string[] SupportFiles={"LaunchSupport/classic-music-definitions.json","LaunchSupport/classic-music-definitions.zip","LaunchSupport/classic-music-files.sha256"};
        static readonly string[] PvpEventTracks={"music/pvp_quake_evil.mp3","music/pvp_quake_good.mp3","music/pvp_quake_neutral.mp3"};
        // Event recordings are supplied separately and may be replaced without
        // changing the Classic zone/music catalog or rebuilding the patcher.
        internal static bool IsPvpEventTrack(string path) {
            return PvpEventTracks.Contains((path??"").Replace('\\','/'),StringComparer.OrdinalIgnoreCase);
        }
        internal static bool IsOptionalMusicAsset(string path) {
            string name=(path??"").Replace('\\','/');
            return Regex.IsMatch(name,@"\Amusic/[A-Za-z0-9_.-]{1,100}\.(mp3|wav)\z",RegexOptions.IgnoreCase)||
                IsDefinition(name.ToLowerInvariant())||SupportFiles.Contains(name,StringComparer.OrdinalIgnoreCase);
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]
        static extern uint GetPrivateProfileString(string section,string key,string fallback,StringBuilder value,uint size,string file);
        static bool EnabledKey(string root,string key) {
            var value=new StringBuilder(32);
            GetPrivateProfileString("Zeal",key,"FALSE",value,(uint)value.Capacity,ClassicFix.Safe(root,"zeal.ini"));
            return value.ToString().Equals("TRUE",StringComparison.OrdinalIgnoreCase)||value.ToString()=="1";
        }
        internal static bool SavedEnabled(string root) {return EnabledKey(root,"ClassicMusic")&&EnabledKey(root,"ClassicMenuMusic");}
        internal static string[] MissingSupport(string root) {return SupportFiles.Where(name=>!File.Exists(ClassicFix.Safe(root,name))).ToArray();}
        static string MissingMessage(string root) {
            return "Music-pack support files are missing from this client: "+root+Environment.NewLine+
                String.Join(Environment.NewLine,MissingSupport(root))+Environment.NewLine+
                "Copy both music and LaunchSupport from the music pack into the folder containing eqgame.exe, then apply Classic music. Music is optional for Play.";
        }
        internal static string Describe(string root) {
            string saved=SavedEnabled(root)?"Classic music is enabled in Zeal. ":"Classic music is not enabled in Zeal. ";
            if(MissingSupport(root).Length!=0)return saved+MissingMessage(root);
            var catalog=ReadCatalog(root);
            VerifiedSupport(root,"LaunchSupport/classic-music-definitions.zip");
            var names=new HashSet<string>(StringComparer.Ordinal);
            bool applied=true;
            foreach(var row in catalog.files) {
                Require(row!=null&&IsDefinition(row.path)&&row.bytes>0&&row.bytes<=256*1024&&Regex.IsMatch(row.sha256??"",@"\A[0-9a-f]{64}\z")&&names.Add(row.path),"Invalid music definition entry.");
                string file=ClassicFix.Safe(root,row.path);
                if(!File.Exists(file)||new FileInfo(file).Length!=row.bytes||ClassicFix.HashFile(file)!=row.sha256)applied=false;
            }
            return saved+(applied?"Zone definitions match the installed music pack.":"The music pack is present; select Classic music and Apply to install or update its zone definitions.")+Environment.NewLine+
                "PvP event music is optional and can be updated separately."+Environment.NewLine+
                "Client: "+root+Environment.NewLine+"Select Classic music to enable or update it. Clearing the box leaves saved music settings unchanged.";
        }
        internal static bool IsDefinition(string path) {
            return path != null && Regex.IsMatch(path,@"\A[a-z0-9]{1,48}\.emt\z");
        }
        static void Require(bool yes,string message) {if(!yes)throw new IOException(message);}
        static string VerifiedSupport(string root,string name) {
            string path=ClassicFix.Safe(root,name),list=ClassicFix.Safe(root,"LaunchSupport/classic-music-files.sha256");
            Require(File.Exists(path)&&File.Exists(list),MissingMessage(root));
            Require(new FileInfo(list).Length<=128*1024,"Music pack file list is too large.");
            var rows=File.ReadAllLines(list).Select(s=>s.Split('\t')).Where(p=>p.Length==2&&p[1]==name).ToArray();
            Require(rows.Length==1&&Regex.IsMatch(rows[0][0],@"\A[0-9a-f]{64}\z")&&ClassicFix.HashFile(path)==rows[0][0],
                "The music pack is incomplete or changed. Reinstall the matching separate music pack before applying Classic music.");
            return path;
        }
        static MusicDefinitionCatalog ReadCatalog(string root) {
            string catalogPath=VerifiedSupport(root,"LaunchSupport/classic-music-definitions.json");
            Require(new FileInfo(catalogPath).Length<=256*1024,"Music definition catalog is too large.");
            var json=new JavaScriptSerializer {MaxJsonLength=256*1024,RecursionLimit=16};
            var catalog=json.Deserialize<MusicDefinitionCatalog>(File.ReadAllText(catalogPath));
            Require(catalog!=null&&catalog.schema==1&&catalog.files!=null&&catalog.files.Count>0&&catalog.files.Count<=128,"Invalid music definition catalog.");
            Require(catalog.audioFiles!=null&&catalog.audioFiles.Count>0&&catalog.audioFiles.Count<=512,"Invalid music audio catalog.");
            return catalog;
        }
        internal static Dictionary<string,byte[]> Stage(string root) {
            var catalog=ReadCatalog(root);
            var audioNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var audio in catalog.audioFiles) {
                Require(audio!=null&&Regex.IsMatch(audio.path??"",@"\Amusic/[A-Za-z0-9_.-]{1,100}\.(mp3|wav)\z")&&audio.bytes>0&&audio.bytes<=64*1024*1024&&Regex.IsMatch(audio.sha256??"",@"\A[0-9a-f]{64}\z")&&audioNames.Add(audio.path),"Invalid music audio entry.");
                // These cues are selected by the earthquake packet handler,
                // independently of ClassicMusic. Zeal loads the installed MP3.
                if(IsPvpEventTrack(audio.path))continue;
                string path=ClassicFix.Safe(root,audio.path);
                Require(File.Exists(path)&&new FileInfo(path).Length==audio.bytes&&ClassicFix.HashFile(path)==audio.sha256,"Music pack audio is missing or changed: "+audio.path+". Reinstall the separate music pack.");
            }
            var expected=new Dictionary<string,MusicDefinitionRecord>(StringComparer.Ordinal);
            foreach(var row in catalog.files) {
                Require(row!=null&&IsDefinition(row.path)&&row.bytes>0&&row.bytes<=256*1024&&Regex.IsMatch(row.sha256??"",@"\A[0-9a-f]{64}\z"),"Invalid music definition entry.");
                Require(!expected.ContainsKey(row.path),"Duplicate music definition entry.");
                expected.Add(row.path,row);
            }
            string zipPath=VerifiedSupport(root,"LaunchSupport/classic-music-definitions.zip");
            Require(new FileInfo(zipPath).Length<=4*1024*1024,"Music definition package is too large.");
            var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            long total=0;
            using(var stream=File.OpenRead(zipPath))using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)) {
                Require(archive.Entries.Count==expected.Count,"Music definition package is incomplete.");
                foreach(var entry in archive.Entries) {
                    MusicDefinitionRecord row;
                    Require(expected.TryGetValue(entry.FullName,out row)&&!result.ContainsKey(entry.FullName)&&entry.Length==row.bytes,"Unexpected music definition package entry.");
                    total+=entry.Length;Require(total<=4*1024*1024,"Expanded music definition package is too large.");
                    var bytes=new byte[row.bytes];
                    using(var input=entry.Open()) {
                        int offset=0;
                        while(offset<bytes.Length) {int read=input.Read(bytes,offset,bytes.Length-offset);Require(read>0,"Truncated music definition.");offset+=read;}
                        Require(input.ReadByte()==-1,"Music definition exceeds its recorded size.");
                    }
                    Require(ClassicFix.Hash(bytes)==row.sha256,"Music definition failed verification: "+row.path);
                    result.Add(row.path,bytes);
                }
            }
            return result;
        }
    }
}
