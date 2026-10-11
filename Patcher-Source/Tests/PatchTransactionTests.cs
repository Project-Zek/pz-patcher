using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;
using EQEmu_Patcher;
class PatchTransactionTests {
    static string root, url;static int failed;
    static readonly byte[] a=Encoding.ASCII.GetBytes("NEW-A-123"), b=Encoding.ASCII.GetBytes("NEW-B-456");
    static string Hash(byte[] bytes) {using(var md5=MD5.Create())return BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Check(bool condition,string why) {if(!condition)throw new Exception(why);}
    static string ClientRoot(string test) {string path=Path.Combine(root,test);Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"a.txt"),"OLD-A");File.WriteAllText(Path.Combine(path,"b.txt"),"OLD-B");return path;}
    static List<PatchFile> Files() {return new List<PatchFile>{new PatchFile("a.txt",Hash(a),a.Length),new PatchFile("b.txt",Hash(b),b.Length)};}
    static async Task Test(string name,Func<Task> action) {
        try {await action();Console.WriteLine("PASS\t"+name);}catch(Exception e){failed++;Console.WriteLine("FAIL\t"+name+"\t"+e.Message);}
    }
    static async Task Reject(string name,List<PatchFile> files,IEnumerable<string> deletes=null) {
        string path=ClientRoot(name);bool rejected=false;
        try {await new PatchTransaction(path,true).ApplyAsync(files,deletes,new Uri(url+"/"+name+"/"),CancellationToken.None,x=>{},x=>{});}catch {rejected=true;}
        Check(rejected,"Operation unexpectedly succeeded");Check(File.ReadAllText(Path.Combine(path,"a.txt"))=="OLD-A","Installed data changed on rejection");
    }
    static async Task Run() {
        await Test("whole release and deletion commit",async()=> {
            string path=ClientRoot("success");File.WriteAllText(Path.Combine(path,"obsolete.txt"),"KEEP BACKUP");
            await new PatchTransaction(path,true).ApplyAsync(Files(),new[]{"obsolete.txt"},new Uri(url+"/success/"),CancellationToken.None,x=>{},x=>{});
            Check(File.ReadAllBytes(Path.Combine(path,"a.txt")).Length==a.Length,"New file missing");
            Check(!File.Exists(Path.Combine(path,"obsolete.txt")),"Deletion missing");
            Check(Directory.GetFiles(Path.Combine(path,"LaunchSupport/patch-transactions"),"obsolete.txt",SearchOption.AllDirectories).Length==1,"Deleted file backup missing");
            Check(!File.Exists(new PatchTransaction(path).Marker),"Completion marker left behind");
        });
        await Test("already matching files do not download",async()=> {
            string path=ClientRoot("already");File.WriteAllBytes(Path.Combine(path,"a.txt"),a);File.WriteAllBytes(Path.Combine(path,"b.txt"),b);
            await new PatchTransaction(path,true).ApplyAsync(Files(),null,new Uri(url+"/already/"),CancellationToken.None,x=>{},x=>{});
        });
        await Test("wrong downloaded MD5 preserves complete installed release",()=>Reject("badhash",Files()));
        await Test("404 on later file preserves earlier installed file",()=>Reject("missing",Files()));
        await Test("truncated download preserves installed release",()=>Reject("truncated",Files()));
        await Test("cancel during staging preserves installed release",async()=> {
            string path=ClientRoot("cancel");var stop=new CancellationTokenSource();bool rejected=false;
            try {await new PatchTransaction(path,true).ApplyAsync(Files(),null,new Uri(url+"/cancel/"),stop.Token,x=>{},x=>stop.Cancel());}catch(OperationCanceledException){rejected=true;}
            Check(rejected&&File.ReadAllText(Path.Combine(path,"a.txt"))=="OLD-A","Cancellation changed installed files");
        });
        await Test("sharing violation rolls back files already committed",async()=> {
            string path=ClientRoot("locked");FileStream locked=null;bool rejected=false;
            try {
                await new PatchTransaction(path,true).ApplyAsync(Files(),null,new Uri(url+"/locked/"),CancellationToken.None,x=>{},x=>{if(x=="Verified b.txt")locked=new FileStream(Path.Combine(path,"b.txt"),FileMode.Open,FileAccess.Read,FileShare.None);});
            } catch(IOException){rejected=true;}finally{if(locked!=null)locked.Dispose();}
            Check(rejected,"Sharing violation was not observed");
            Check(File.ReadAllText(Path.Combine(path,"a.txt"))=="OLD-A"&&File.ReadAllText(Path.Combine(path,"b.txt"))=="OLD-B","Rollback did not restore the previous pair");
            Check(!File.Exists(new PatchTransaction(path).Marker),"Successful rollback retained incomplete marker");
        });
        foreach(string unsafePath in new[]{"../outside.txt","..\\outside.txt","C:/outside.txt","NUL.txt","bad./a.txt","a.txt:stream","bad\nname.txt","zeal.ini","LaunchSupport/logs/player.log","gequip5.s3d","global6_chr.s3d","bmpwad8.s3d","eqfeart7.pak","spellsnew.edd","spellsnew.eff","ClassicFix/latest.json","ClassicFix/backups/original.s3d"}) {
            string item=unsafePath;
            await Test("reject path "+item.Replace('\n','_'),()=>Reject("unsafe-"+Guid.NewGuid().ToString("N"),new List<PatchFile>{new PatchFile(item,Hash(a),a.Length)}));
        }
        await Test("reject duplicate case-insensitive names",()=>Reject("duplicate",new List<PatchFile>{new PatchFile("a.txt",Hash(a),a.Length),new PatchFile("A.txt",Hash(a),a.Length)}));
        await Test("reject download/delete collision",()=>Reject("collision",Files(),new[]{"a.txt"}));
        await Test("reject invalid MD5 before download",()=>Reject("invalid-md5",new List<PatchFile>{new PatchFile("a.txt","bad",a.Length)}));
        await Test("incomplete installation marker blocks further updates",async()=> {
            string path=ClientRoot("incomplete");var transaction=new PatchTransaction(path,true);Directory.CreateDirectory(Path.GetDirectoryName(transaction.Marker));File.WriteAllText(transaction.Marker,"previous transaction");bool rejected=false;
            try {await transaction.ApplyAsync(Files(),null,new Uri(url+"/incomplete/"),CancellationToken.None,x=>{},x=>{});}catch(IOException){rejected=true;}
            Check(rejected&&File.ReadAllText(Path.Combine(path,"a.txt"))=="OLD-A","Incomplete marker ignored");
        });
        await Test("production updater rejects HTTP",async()=> {
            string path=ClientRoot("http");bool rejected=false;
            try {await new PatchTransaction(path).ApplyAsync(Files(),null,new Uri(url+"/http/"),CancellationToken.None,x=>{},x=>{});}catch(IOException){rejected=true;}
            Check(rejected,"Unencrypted production URL accepted");
        });
        await Test("classic installation marker blocks updates before downloads",async()=> {
            string path=ClientRoot("classic-incomplete");Directory.CreateDirectory(Path.Combine(path,"ClassicFix"));File.WriteAllText(Path.Combine(path,"ClassicFix/incomplete.json"),"audit marker");bool rejected=false;
            try{await new PatchTransaction(path,true).ApplyAsync(Files(),null,new Uri(url+"/classic-incomplete/"),CancellationToken.None,x=>{},x=>{});}catch(IOException){rejected=true;}
            Check(rejected&&File.ReadAllText(Path.Combine(path,"a.txt"))=="OLD-A","Classic marker ignored");
        });
        await Test("protected classic archives cannot be removed by a manifest",()=>Reject("unsafe-delete",Files(),new[]{"global6_chr.s3d"}));
    }
    static int Main(string[] args) {root=args[0];url=args[1];Run().GetAwaiter().GetResult();return failed==0?0:1;}
}
