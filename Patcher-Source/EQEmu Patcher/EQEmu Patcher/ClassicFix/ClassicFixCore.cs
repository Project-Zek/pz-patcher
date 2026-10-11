using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;

namespace ProjectZekClassic {
    public sealed class RecipeBook {
        public string version {get;set;}
        public Dictionary<string,ArchiveRecipe> recipes {get;set;}
        public List<TimingEdit> timing {get;set;}
        public string timing_before_wld_sha256 {get;set;}
        public string timing_after_wld_sha256 {get;set;}
    }
    public sealed class ArchiveRecipe {
        public Dictionary<string,MemberRecipe> members {get;set;}
        public List<Dictionary<string,string>> profiles {get;set;}
        public string role {get;set;}
        public bool preserve_other_members {get;set;}
    }
    public sealed class MemberUpgrade {
        public string before_sha256 {get;set;}
        public List<FragmentRecipe> fragments {get;set;}
    }
    public sealed class MemberRecipe {
        public List<MemberUpgrade> upgrades {get;set;}
        public string archive {get;set;}
        public string member {get;set;}
        public string sha256 {get;set;}
        public uint[] header {get;set;}
        public string strings {get;set;}
        public List<FragmentRecipe> fragments {get;set;}
    }
    public sealed class FragmentRecipe {
        public string archive {get;set;}
        public string member {get;set;}
        public int fragment {get;set;}
        public int length {get;set;}
        public string sha256 {get;set;}
        public List<uint[]> writes {get;set;}
    }
    public sealed class TimingEdit {
        public string name {get;set;}
        public int offset {get;set;}
        public uint before {get;set;}
        public uint after {get;set;}
    }
    public sealed class Change {
        public string name {get;set;}
        public bool existed {get;set;}
        public string before {get;set;}
        public string after {get;set;}
    }
    public sealed class Journal {
        public string root {get;set;}
        public string id {get;set;}
        public string phase {get;set;}
        public List<Change> changes {get;set;}
    }
    public static class ClassicFix {
        static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=8*1024*1024,RecursionLimit=128};
        static RecipeBook book;
        static readonly HashSet<string> Targets=new HashSet<string>(new[]{"gequip5.s3d","global6_chr.s3d","bmpwad8.s3d","eqfeart7.pak","spellsnew.edd","spellsnew.eff","eqclient.ini","zeal.ini"},StringComparer.Ordinal);
        static bool CanTarget(string name) {return Targets.Contains(name)||MusicDefinitionAssets.IsDefinition(name);}
        internal static RecipeBook Book {get {if(book==null) {
            using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("ProjectZek.ClassicRecipes")))
                book=Json.Deserialize<RecipeBook>(reader.ReadToEnd());
        } return book;}}
        public static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public static string HashFile(string path) {using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        static void Require(bool condition,string message) {if(!condition)throw new IOException(message);}
        internal static string Safe(string root,string relative) {
            Require(!String.IsNullOrEmpty(relative)&&relative.IndexOf(':')<0&&relative.IndexOf('\\')<0&&!relative.StartsWith("/")&&!relative.Split('/').Any(x=>x.Length==0||x=="."||x==".."),"Unsafe classic-fix path.");
            root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            Require(path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Classic-fix path leaves the client.");
            for(string p=path;p!=null;p=Path.GetDirectoryName(p)) {
                if(File.Exists(p)||Directory.Exists(p))Require((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"Choose a client without linked files or folders.");
                if(p.Equals(root,StringComparison.OrdinalIgnoreCase))break;
            }
            return path;
        }
        public static string ClientRoot(string root) {
            Require(!String.IsNullOrWhiteSpace(root),"Choose the folder containing eqgame.exe.");
            root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            Require(root!=Path.GetPathRoot(root).TrimEnd(Path.DirectorySeparatorChar),"Choose the EverQuest client folder, not a drive.");
            string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            Require(!root.Equals(windows,StringComparison.OrdinalIgnoreCase)&&!root.StartsWith(windows+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Choose the EverQuest client folder outside Windows.");
            Require(File.Exists(Safe(root,"eqgame.exe")),"This folder does not contain eqgame.exe.");
            return root;
        }
        public static void Idle() {Require(Process.GetProcessesByName("eqgame").Length==0,"Close all EverQuest game windows before applying or restoring fixes.");}
        static void Save(string path,object value) {
            string temp=path+".tmp";Require(!File.Exists(temp),"A previous state save is unfinished; preserve the files for recovery.");
            File.WriteAllText(temp,Json.Serialize(value),new UTF8Encoding(false));
            string previous=path+".previous";Require(!File.Exists(previous),"A previous state save is unfinished; preserve the files for recovery.");
            bool existed=File.Exists(path);if(existed)File.Move(path,previous);
            try{File.Move(temp,path);}catch{if(existed&&!File.Exists(path))File.Move(previous,path);throw;}
            if(existed)File.Delete(previous);
        }
        public static bool HasIncomplete(string root) {return File.Exists(Safe(root,"ClassicFix/incomplete.json"));}
        static bool Matches(Pfs archive,Dictionary<string,string> profile) {
            return archive.Files.Count==profile.Count&&profile.All(x=>archive.Files.ContainsKey(x.Key)&&Hash(archive.Files[x.Key])==x.Value);
        }
        public static bool ApprovedAsset(string root,string name) {
            if(!Book.recipes.ContainsKey(name))return false;
            try {var archive=Pfs.Read(Safe(root,name));return Book.recipes[name].profiles.Any(p=>Matches(archive,p));}
            catch(IOException){return false;}
        }
        public static void VerifyModels(string root) {
            Require(!HasIncomplete(root),"A classic fix stopped during installation. Use Classic fixes > Restore before playing.");
            foreach(string name in new[]{"gequip5.s3d","global6_chr.s3d"})Require(ApprovedAsset(root,name),"Unsupported or damaged model archive: "+name+". Use a supported client or restore your backup.");
        }
        static byte[] Member(string root,string archive,string member,Dictionary<string,Pfs> cache) {
            if(!cache.ContainsKey(archive))cache[archive]=Pfs.Read(Safe(root,archive));
            Require(cache[archive].Files.ContainsKey(member),"Missing local asset "+archive+" / "+member+". Use the fresh TAKPv22 base.");
            return cache[archive].Files[member];
        }
        static byte[] BuildMember(string root,MemberRecipe rule,Dictionary<string,Pfs> cache,Dictionary<string,List<byte[]>> fragments) {
            if(rule.fragments==null) {
                byte[] data=Member(root,rule.archive,rule.member,cache);
                Require(Hash(data)==rule.sha256,"An existing source asset differs from the supported base: "+rule.archive+" / "+rule.member);
                return data;
            }
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                foreach(uint value in rule.header)writer.Write(value);
                byte[] strings=Encoding.ASCII.GetBytes(rule.strings),key={0x95,0x3a,0xc5,0x2a,0x95,0x7a,0x95,0x6a};
                for(int i=0;i<strings.Length;i++)strings[i]^=key[i%8];writer.Write(strings);
                foreach(var f in rule.fragments) {
                    string id=f.archive+"/"+f.member;
                    if(!fragments.ContainsKey(id))fragments[id]=WldFragments(Member(root,f.archive,f.member,cache));
                    Require(f.fragment>0&&f.fragment<=fragments[id].Count,"Invalid fragment recipe.");
                    byte[] original=fragments[id][f.fragment-1];
                    Require(Hash(original)==f.sha256,"Local model fragment differs from the supported base: "+id+" #"+f.fragment);
                    byte[] copy=new byte[f.length];Buffer.BlockCopy(original,0,copy,0,Math.Min(copy.Length,original.Length));
                    foreach(var edit in f.writes) {Require(edit.Length==2&&edit[0]+4<=copy.Length,"Invalid fragment edit.");Buffer.BlockCopy(BitConverter.GetBytes(edit[1]),0,copy,(int)edit[0],4);}
                    writer.Write(copy);
                }
                writer.Flush();byte[] data=stream.ToArray();Require(Hash(data)==rule.sha256,"Reconstructed model did not match the release recipe.");return data;
            }
        }
        static byte[] BuildLocalUpgrade(byte[] before,MemberRecipe rule,MemberUpgrade upgrade) {
            Require(Hash(before)==upgrade.before_sha256,"Local model upgrade source changed.");
            var fragments=WldFragments(before);
            Require(upgrade.fragments!=null&&upgrade.fragments.Count==rule.header[2],"Invalid local upgrade fragment count.");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                foreach(uint value in rule.header)writer.Write(value);
                byte[] strings=Encoding.ASCII.GetBytes(rule.strings),key={0x95,0x3a,0xc5,0x2a,0x95,0x7a,0x95,0x6a};
                for(int i=0;i<strings.Length;i++)strings[i]^=key[i%8];writer.Write(strings);
                foreach(var f in upgrade.fragments) {
                    Require(f.fragment>0&&f.fragment<=fragments.Count&&f.length>=8,"Invalid local upgrade fragment.");
                    byte[] source=fragments[f.fragment-1];Require(Hash(source)==f.sha256,"Local upgrade fragment changed.");
                    byte[] rebuilt=new byte[f.length];Buffer.BlockCopy(source,0,rebuilt,0,Math.Min(source.Length,rebuilt.Length));
                    foreach(var edit in f.writes) {Require(edit.Length==2&&edit[0]<=rebuilt.Length-4,"Invalid local upgrade scalar edit.");Buffer.BlockCopy(BitConverter.GetBytes(edit[1]),0,rebuilt,(int)edit[0],4);}
                    writer.Write(rebuilt);
                }
                writer.Flush();byte[] result=stream.ToArray();Require(Hash(result)==rule.sha256,"Local model upgrade result failed verification.");return result;
            }
        }
        internal static List<byte[]> WldFragments(byte[] bytes) {
            Require(bytes.Length>=28&&BitConverter.ToUInt32(bytes,0)==0x54503d02,"Invalid WLD header.");
            uint count=BitConverter.ToUInt32(bytes,8),strings=BitConverter.ToUInt32(bytes,20);
            Require(count<=100000&&strings<=bytes.Length-28,"Invalid WLD bounds.");
            int pos=28+(int)strings;var result=new List<byte[]>();
            for(int i=0;i<count;i++) {
                Require(pos+8<=bytes.Length,"Truncated WLD fragment.");uint size=BitConverter.ToUInt32(bytes,pos);
                Require(size<=bytes.Length-pos-8,"Invalid WLD fragment size.");var fragment=new byte[size+8];Buffer.BlockCopy(bytes,pos,fragment,0,fragment.Length);result.Add(fragment);pos+=fragment.Length;
            }
            Require(pos==bytes.Length||(bytes.Length-pos==4&&BitConverter.ToUInt32(bytes,pos)==0xffffffff),"Unexpected WLD trailing data.");return result;
        }
        static byte[] Timed(byte[] bytes,bool enable) {
            string digest=Hash(bytes);Require(digest==Book.timing_before_wld_sha256||digest==Book.timing_after_wld_sha256,"Apply the supported classic models before testing wolf timing.");
            var data=(byte[])bytes.Clone();
            foreach(var edit in Book.timing)Buffer.BlockCopy(BitConverter.GetBytes(enable?edit.after:edit.before),0,data,edit.offset,4);
            Require(Hash(data)==(enable?Book.timing_after_wld_sha256:Book.timing_before_wld_sha256),"Wolf timing result failed verification.");return data;
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool WritePrivateProfileString(string section,string key,string value,string file);
        public static string Apply(string input,bool models,bool spells,bool loading,bool login,bool timing) {
            return ApplyWithMusic(input,models,spells,loading,login,timing,false,false);
        }
        public static string ApplyWithMusic(string input,bool models,bool spells,bool loading,bool login,bool timing,bool music,bool backgrounds) {
            string root=ClientRoot(input);Idle();Require(!HasIncomplete(root),"A previous fix was interrupted. Restore it first.");
            Require(!timing,"The retired timing-only experiment is unavailable. Select Classic skeletons and wolves for the complete local fix.");
            Require(models||spells||loading||login||timing||music||backgrounds,"Select at least one fix.");
            string support=Safe(root,"ClassicFix");Directory.CreateDirectory(support);
            using(var gate=new FileStream(Safe(root,"ClassicFix/operation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
                string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N");
                string tx=Safe(root,"ClassicFix/backups/"+id);Directory.CreateDirectory(tx);Directory.CreateDirectory(Path.Combine(tx,"before"));Directory.CreateDirectory(Path.Combine(tx,"after"));
                var staged=new Dictionary<string,byte[]>();var cache=new Dictionary<string,Pfs>(StringComparer.OrdinalIgnoreCase);var fragments=new Dictionary<string,List<byte[]>>();
                foreach(string name in new[]{"gequip5.s3d","global6_chr.s3d","bmpwad8.s3d","eqfeart7.pak"}) {
                    bool selected=(models&&(name=="gequip5.s3d"||name=="global6_chr.s3d"))||(loading&&name=="bmpwad8.s3d")||(login&&name=="eqfeart7.pak")||(timing&&name=="global6_chr.s3d");
                    if(!selected)continue;var rule=Book.recipes[name];Pfs archive=Pfs.Read(Safe(root,name));
                    bool already=Matches(archive,rule.profiles[0]);
                    if(!already && models && name=="global6_chr.s3d" && rule.profiles.Any(p=>Matches(archive,p))) {
                        var mr=rule.members["global6_chr.wld"];
                        var upgrade=mr.upgrades==null?null:mr.upgrades.SingleOrDefault(u=>u.before_sha256==Hash(archive.Files["global6_chr.wld"]));
                        if(upgrade!=null) {
                            byte[] upgradeBytes=BuildLocalUpgrade(archive.Files["global6_chr.wld"],mr,upgrade);
                            archive.Files["global6_chr.wld"]=upgradeBytes;already=Matches(archive,rule.profiles[0]);
                            Require(already,"Local model upgrade changed unexpected content.");
                        }
                    }
                    if(!already) {
                        if(!models&&timing&&name=="global6_chr.s3d")throw new IOException("Apply the classic models before testing wolf timing.");
                        var output=rule.preserve_other_members?new Dictionary<string,byte[]>(archive.Files,StringComparer.Ordinal):new Dictionary<string,byte[]>(StringComparer.Ordinal);
                        foreach(var member in rule.members)output[member.Key]=BuildMember(root,member.Value,cache,fragments);
                        archive=new Pfs(output,archive.Footer);Require(Matches(archive,rule.profiles[0]),"Reconstructed archive did not match the expected member set: "+name);
                    }
                    if(name=="global6_chr.s3d")archive.Files["global6_chr.wld"]=Timed(archive.Files["global6_chr.wld"],timing);
                    Require(rule.profiles.Any(p=>Matches(archive,p)),"Archive approval failed: "+name);
                    var bytes=archive.Write();Pfs roundtrip=Pfs.Read(bytes);Require(rule.profiles.Any(p=>Matches(roundtrip,p)),"Saved archive failed verification: "+name);staged[name]=bytes;
                }
                if(spells) {
                    Require(File.Exists(Safe(root,"spells.eff")),"Classic spell effects are missing from this client.");
                    staged["spellsnew.edd"]=null;staged["spellsnew.eff"]=null;
                    string ini=Safe(root,"eqclient.ini"),temp=Path.Combine(tx,"eqclient-stage.ini");
                    if(File.Exists(ini))File.Copy(ini,temp);else File.WriteAllText(temp,"[Defaults]\r\n",Encoding.Default);
                    Require(WritePrivateProfileString("Defaults","ShowSpellEffects","1",temp),"Could not save classic spell-effect preferences.");
                    staged["eqclient.ini"]=File.ReadAllBytes(temp);File.Delete(temp);
                }
                if(music)foreach(var file in MusicDefinitionAssets.Stage(root))staged.Add(file.Key,file.Value);
                if(music||backgrounds) {
                    string ini=Safe(root,"zeal.ini"),temp=Path.Combine(tx,"zeal-stage.ini");
                    if(File.Exists(ini))File.Copy(ini,temp);else File.WriteAllText(temp,"",Encoding.Default);
                    if(music) {
                        Require(WritePrivateProfileString("Zeal","ClassicMusic","TRUE",temp),"Could not enable classic music.");
                        Require(WritePrivateProfileString("Zeal","ClassicMenuMusic","TRUE",temp),"Could not enable classic character music.");
                    }
                    if(backgrounds) {
                        Require(File.Exists(Safe(root,"load.s3d"))&&File.Exists(Safe(root,"load_obj.s3d")),"This client is missing the classic character-preview room.");
                        Require(WritePrivateProfileString("CharacterSelect","ClassicBackgrounds","TRUE",temp),"Could not enable classic backgrounds.");
                    }
                    staged["zeal.ini"]=File.ReadAllBytes(temp);File.Delete(temp);
                }
                var journal=new Journal {root=root,id=id,phase="prepared",changes=new List<Change>()};
                foreach(var pair in staged) {
                    string path=Safe(root,pair.Key);bool existed=File.Exists(path);byte[] old=existed?File.ReadAllBytes(path):null;
                    if((old==null&&pair.Value==null)||(old!=null&&pair.Value!=null&&Hash(old)==Hash(pair.Value)))continue;
                    if(existed)File.WriteAllBytes(Path.Combine(tx,"before",pair.Key),old);
                    if(pair.Value!=null)File.WriteAllBytes(Path.Combine(tx,"after",pair.Key),pair.Value);
                    journal.changes.Add(new Change {name=pair.Key,existed=existed,before=old==null?null:Hash(old),after=pair.Value==null?null:Hash(pair.Value)});
                }
                if(journal.changes.Count==0)return "These fixes are already applied. No client files changed.";
                Save(Path.Combine(tx,"journal.json"),journal);string marker=Safe(root,"ClassicFix/incomplete.json");Save(marker,journal);
                try {
                    foreach(var change in journal.changes) {
                        string path=Safe(root,change.name);Require((File.Exists(path)?HashFile(path):null)==change.before,"A client file changed while the fix was being prepared: "+change.name);
                        Install(root,tx,change.name,change.after!=null?"after":null);
                    }
                    journal.phase="complete";Save(Path.Combine(tx,"journal.json"),journal);Save(Safe(root,"ClassicFix/latest.json"),journal);File.Delete(marker);
                    return "Applied "+journal.changes.Count+" changes. Your originals are saved for Restore."+(music?" Music definitions and Zeal settings are updated; audio files are obtained separately.":"");
                } catch {
                    bool rolledBack=true;
                    foreach(var change in journal.changes.AsEnumerable().Reverse())try{Install(root,tx,change.name,change.existed?"before":null);}catch{rolledBack=false;}
                    if(rolledBack){journal.phase="rolled-back";Save(Path.Combine(tx,"journal.json"),journal);File.Delete(marker);}
                    throw;
                }
            }
        }
        static void Install(string root,string tx,string name,string source) {
            Require(CanTarget(name),"Unsupported restore target.");string path=Safe(root,name);
            if(source==null){if(File.Exists(path))File.Delete(path);return;}
            string staged=Path.Combine(tx,source,name),temp=Safe(root,"ClassicFix/install-"+Guid.NewGuid().ToString("N")+".tmp");File.Copy(staged,temp);
            string held=Path.Combine(tx,"held-"+Guid.NewGuid().ToString("N"));bool existed=File.Exists(path);if(existed)File.Move(path,held);
            try{File.Move(temp,path);}catch{if(existed&&!File.Exists(path))File.Move(held,path);throw;}
            // Originals and staged versions remain in the transaction for recovery.
        }
        public static string Restore(string input) {
            string root=ClientRoot(input);Idle();string marker=Safe(root,"ClassicFix/incomplete.json"),latest=Safe(root,"ClassicFix/latest.json");
            Require(File.Exists(marker)||File.Exists(latest),"This client has no classic-fix backup to restore.");
            using(var gate=new FileStream(Safe(root,"ClassicFix/operation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
                Journal journal=Json.Deserialize<Journal>(File.ReadAllText(File.Exists(marker)?marker:latest));
                Require(journal!=null&&journal.root.Equals(root,StringComparison.OrdinalIgnoreCase)&&journal.changes!=null&&journal.changes.Count<=136,"Backup belongs to another client.");
                Require(journal.changes.Select(c=>c.name).Distinct(StringComparer.OrdinalIgnoreCase).Count()==journal.changes.Count,"Duplicate backup target.");
                Require(journal.id.All(c=>Char.IsLetterOrDigit(c)||c=='-'),"Invalid backup identifier.");string tx=Safe(root,"ClassicFix/backups/"+journal.id);
                foreach(var change in journal.changes) {
                    Require(CanTarget(change.name),"Invalid backup target.");
                    if(change.existed)Require(HashFile(Safe(root,"ClassicFix/backups/"+journal.id+"/before/"+change.name))==change.before,"Backup verification failed: "+change.name);
                    if(!File.Exists(marker))Require((File.Exists(Safe(root,change.name))?HashFile(Safe(root,change.name)):null)==change.after,"This file changed after applying fixes: "+change.name+". Preserve it before restoring.");
                }
                if(!File.Exists(marker))Save(marker,journal);
                foreach(var change in journal.changes.AsEnumerable().Reverse())Install(root,tx,change.name,change.existed?"before":null);
                journal.phase="restored";Save(Path.Combine(tx,"journal.json"),journal);File.Delete(marker);if(File.Exists(latest))File.Delete(latest);
                return "Restored the original files from the latest fix. Backups are retained.";
            }
        }
    }
    internal sealed class Pfs {
        internal Dictionary<string,byte[]> Files;
        internal byte[] Footer;
        internal Pfs(Dictionary<string,byte[]> files,byte[] footer=null){Files=files;Footer=footer??new byte[0];}
        static void Check(bool yes,string why){if(!yes)throw new IOException(why);}
        static uint U(byte[] b,int p){Check(p>=0&&p<=b.Length-4,"Truncated PFS data.");return BitConverter.ToUInt32(b,p);}
        internal static Pfs Read(string path){Check(new FileInfo(path).Length<=128*1024*1024,"Archive exceeds supported size.");return Read(File.ReadAllBytes(path));}
        static uint Adler(byte[] b){uint a=1,c=0;foreach(byte x in b){a=(a+x)%65521;c=(c+a)%65521;}return (c<<16)|a;}
        static uint Crc(string name) {
            uint crc=0;foreach(byte b in Encoding.ASCII.GetBytes(name+"\0")){crc^=(uint)b<<24;for(int i=0;i<8;i++)crc=(crc<<1)^((crc&0x80000000)!=0?0x04c11db7U:0U);}return crc;
        }
        internal static Pfs Read(byte[] bytes) {
            Check(bytes.Length>=16&&Encoding.ASCII.GetString(bytes,4,4)=="PFS ","Invalid PFS archive.");uint directory=U(bytes,0);
            Check(directory<=bytes.Length-4,"Invalid PFS directory.");uint count=U(bytes,(int)directory);
            Check(count>1&&count<=10000&&directory+4+(long)count*12<=bytes.Length,"Invalid PFS entry count.");
            var entries=new Dictionary<uint,byte[]>();long total=0;
            for(int i=0;i<count;i++) {
                int p=(int)directory+4+i*12;uint crc=U(bytes,p),offset=U(bytes,p+4),size=U(bytes,p+8);
                Check(size<=32*1024*1024&&offset>=12&&offset<directory&&!entries.ContainsKey(crc),"Invalid PFS file entry.");total+=size;Check(total<=256*1024*1024,"Expanded archive exceeds supported size.");
                byte[] data=new byte[size];int read=0,pos=(int)offset;
                while(read<size) {
                    uint packed=U(bytes,pos),plain=U(bytes,pos+4);pos+=8;
                    Check(packed>=6&&packed<=2*1024*1024&&plain>0&&plain<=1024*1024&&plain<=size-read&&(long)pos+packed<=directory,"Invalid compressed PFS block.");
                    Check((bytes[pos]&15)==8&&((bytes[pos]<<8)|bytes[pos+1])%31==0&&(bytes[pos+1]&32)==0,"Invalid zlib header.");
                    byte[] block=new byte[plain];int got=0;
                    using(var compressed=new MemoryStream(bytes,pos+2,(int)packed-6))using(var inflater=new DeflateStream(compressed,CompressionMode.Decompress)) {
                        while(got<block.Length){int n=inflater.Read(block,got,block.Length-got);Check(n>0,"Truncated compressed data.");got+=n;}Check(inflater.ReadByte()==-1,"Expanded block exceeds declared size.");
                    }
                    int tail=pos+(int)packed-4;uint adler=((uint)bytes[tail]<<24)|((uint)bytes[tail+1]<<16)|((uint)bytes[tail+2]<<8)|bytes[tail+3];Check(Adler(block)==adler,"PFS block checksum mismatch.");
                    Buffer.BlockCopy(block,0,data,read,block.Length);read+=block.Length;pos+=(int)packed;
                }
                entries.Add(crc,data);
            }
            Dictionary<string,byte[]> files=null;int verifiedTables=0;
            // Some editor-produced archives use another directory CRC. Accept only a complete,
            // uniquely verified filename table whose CRCs account for every other entry.
            foreach(var entry in entries) {
                byte[] table=entry.Value;if(table.Length<4||U(table,0)!=count-1)continue;
                try {
                    int p=4;var candidate=new Dictionary<string,byte[]>(StringComparer.Ordinal);var crcs=new HashSet<uint>();
                    for(int i=0;i<count-1;i++) {
                        uint length=U(table,p);p+=4;Check(length>1&&length<256&&p+length<=table.Length&&table[p+length-1]==0,"Invalid filename table.");
                        string name=Encoding.ASCII.GetString(table,p,(int)length-1);p+=(int)length;
                        Check(name.All(c=>c>=32&&c<=126)&&name.IndexOfAny(new[]{'/','\\',':'})<0&&name!="."&&name!="..","Invalid archive member name.");
                        uint crc=Crc(name);Check(crc!=entry.Key&&entries.ContainsKey(crc)&&crcs.Add(crc),"PFS filename CRC mismatch.");candidate.Add(name,entries[crc]);
                    }
                    Check(p==table.Length&&candidate.Count==entries.Count-1,"Incomplete filename table.");files=candidate;verifiedTables++;
                }catch(ArgumentException){}catch(IOException){}
            }
            Check(files!=null,"PFS filename table could not be verified.");Check(verifiedTables==1,"Ambiguous PFS filename table.");
            int end=(int)(directory+4+count*12);byte[] footer=bytes.Skip(end).ToArray();
            Check(footer.Length==0||(footer.Length==9&&Encoding.ASCII.GetString(footer,0,5)=="STEVE"),"Unsupported PFS footer.");return new Pfs(files,footer);
        }
        static void Block(BinaryWriter writer,byte[] data) {
            // The vintage client reader needs the original 8 KiB maximum block size.
            for(int start=0;start<data.Length;start+=8192) {
                byte[] block=new byte[Math.Min(8192,data.Length-start)];Buffer.BlockCopy(data,start,block,0,block.Length);
                byte[] packed;using(var memory=new MemoryStream()){using(var deflate=new DeflateStream(memory,CompressionLevel.Optimal,true))deflate.Write(block,0,block.Length);packed=memory.ToArray();}
                writer.Write((uint)(packed.Length+6));writer.Write((uint)block.Length);writer.Write((byte)0x78);writer.Write((byte)0x9c);writer.Write(packed);uint adler=Adler(block);
                writer.Write((byte)(adler>>24));writer.Write((byte)(adler>>16));writer.Write((byte)(adler>>8));writer.Write((byte)adler);
            }
        }
        internal byte[] Write() {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                writer.Write((uint)0);writer.Write(Encoding.ASCII.GetBytes("PFS "));writer.Write(0x00020000U);
                var entries=new List<uint[]>();var names=Files.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                foreach(string name in names){entries.Add(new[]{Crc(name),(uint)stream.Position,(uint)Files[name].Length});Block(writer,Files[name]);}
                byte[] table;using(var memory=new MemoryStream())using(var metadata=new BinaryWriter(memory)){metadata.Write((uint)names.Length);foreach(string name in names){byte[] encoded=Encoding.ASCII.GetBytes(name+"\0");metadata.Write((uint)encoded.Length);metadata.Write(encoded);}metadata.Flush();table=memory.ToArray();}
                entries.Add(new[]{0x61580ac9U,(uint)stream.Position,(uint)table.Length});Block(writer,table);uint directory=(uint)stream.Position;
                writer.Write((uint)entries.Count);foreach(var entry in entries.OrderBy(x=>x[0]))foreach(uint value in entry)writer.Write(value);writer.Write(Footer);
                stream.Position=0;writer.Write(directory);writer.Flush();return stream.ToArray();
            }
        }
    }
}
