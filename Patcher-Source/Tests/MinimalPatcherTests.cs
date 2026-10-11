using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class MinimalPatcherTests {
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly assembly; static Type client, main; static object verifier; static string root, url; static int passed;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);++passed;Console.WriteLine("PASS\t"+why);}
    static object Call(Type type,string name,object target,params object[] args){try{return type.GetMethod(name,Flags).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static string Hash(byte[] bytes){using(var md5=MD5.Create())return BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static byte[] Payload(int version,string name){return Encoding.ASCII.GetBytes((name=="Zeal.asi"?"ZEAL":"DLL")+"-VERSION-"+version+"-VALIDATED");}
    static string Manifest(int version){var text=new StringBuilder("version: release-"+version+"\ndownloadprefix: https://example.invalid/\ndownloads:\n");foreach(string name in new[]{"Zeal.asi","eqgame.dll"}){var b=Payload(version,name);text.Append("- name: \""+name+"\"\n  md5: "+Hash(b)+"\n  date: \"20261011\"\n  size: "+b.Length+"\n");}return text+"deletes: []\n";}
    static void Verify(){Call(main,"VerifyManifestBeforePlay",verifier);Call(client,"Verify",null);}
    static bool Rejected(Action action){try{action();return false;}catch(IOException){return true;}}
    static async Task Patch(int version){
        var item=assembly.GetType("EQEmu_Patcher.PatchFile",true);
        var list=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(item));
        foreach(string name in new[]{"Zeal.asi","eqgame.dll"}){var b=Payload(version,name);list.Add(Activator.CreateInstance(item,Flags,null,new object[]{name,Hash(b),(long)b.Length},null));}
        var type=assembly.GetType("EQEmu_Patcher.PatchTransaction",true);
        var tx=Activator.CreateInstance(type,Flags,null,new object[]{root,true},null);
        var task=(Task)Call(type,"ApplyAsync",tx,list,null,new Uri(url+"/v"+version+"/"),CancellationToken.None,new Action<int>(x=>{}),new Action<string>(x=>{}));
        await task.ConfigureAwait(false);
    }
    [STAThread] static int Main(string[] args){try{
        Application.EnableVisualStyles();assembly=Assembly.LoadFrom(args[0]);client=assembly.GetType("Client",true);main=assembly.GetType("EQEmu_Patcher.MainForm",true);
        root=args[1];url=args[2];Directory.CreateDirectory(root);client.GetField("Root",Flags).SetValue(null,root);main.GetField("OfflineReview",Flags).SetValue(null,true);
        verifier=FormatterServices.GetUninitializedObject(main);
        Check(assembly.GetName().Name=="Project-Zek-Patcher"&&assembly.GetName().Version.ToString()=="1.0.17.0","new patcher name and version");
        var program=assembly.GetType("EQEmu_Patcher.Program",true);
        Check(!(bool)Call(program,"SelectReviewMode",null,new object[]{new string[0]})&&(bool)Call(program,"SelectReviewMode",null,new object[]{new[]{"--offline"}}),"online-capable default with explicit local-test mode");
        Check((string)Call(main,"ResolveManifestUrl",null,"","")=="","blank server address is intentionally unconfigured");
        Check((string)Call(main,"ResolveManifestUrl",null,"https://example.org/current.yml","https://example.org/old.yml")=="https://example.org/current.yml","configuration overrides compiled server address");
        Check((string)Call(main,"ResolveManifestUrl",null,"","https://example.org/current.yaml")=="https://example.org/current.yaml","explicit source-build address still supported");
        foreach(string bad in new[]{"http://example.org/files.yml","https://user:password@example.org/files.yml","https://example.org/page","../files.yml"})Check(Rejected(()=>Call(main,"ResolveManifestUrl",null,bad,"")),"invalid or credential-bearing server address rejected");
        foreach(string name in new[]{"eqgame.exe","eqmain.dll","eqgfx_dx8.dll","mss32.dll","eqw.dll","D3D8.dll","Zeal.asi","eqgame.dll"})File.WriteAllText(Path.Combine(root,name),"PRIVATE-TEST-ONLY");
        File.WriteAllText(Path.Combine(root,"eqclient.ini"),"[Defaults]\r\nSound=TRUE\r\n");File.WriteAllText(Path.Combine(root,"zeal.ini"),"[Zeal]\r\nFPS=60\r\n");
        Check(!Directory.Exists(Path.Combine(root,"LaunchSupport")),"fixture starts without any LaunchSupport files");
        File.WriteAllText(Path.Combine(root,"filelist-review.yml"),Manifest(1));Task.Run(()=>Patch(1)).GetAwaiter().GetResult();Verify();
        Check(true,"first downloaded release passes Play with no SHA lists");
        Directory.CreateDirectory(Path.Combine(root,"LaunchSupport"));File.WriteAllText(Path.Combine(root,"LaunchSupport/files.sha256"),"STALE SHA DATA");File.WriteAllText(Path.Combine(root,"LaunchSupport/baseline.sha256"),"STALE BASE DATA");Verify();
        Check(true,"leftover obsolete SHA lists cannot block Play");
        File.WriteAllText(Path.Combine(root,"filelist-review.yml"),Manifest(2));Check(Rejected(Verify),"new manifest rejects old client before patching");
        Task.Run(()=>Patch(2)).GetAwaiter().GetResult();Verify();Call(client,"Prepare",null,"keep",false);Call(client,"SelectFps",null,120);
        Check(true,"second downloaded Zeal/DLL release launches checks without rebuilding patcher or updating SHA lists");
        Call(client,"Prepare",null,"d3d11",true);
        var profile=(byte[])Call(client,"EmbeddedBytes",null,"ProjectZek.Graphics.d3d11");Check(Hash(File.ReadAllBytes(Path.Combine(root,"dgVoodoo.conf")))==Hash(profile),"graphics profile is applied from embedded resource");
        Check(!Directory.Exists(Path.Combine(root,"LaunchSupport/profiles")),"no external graphics profiles needed");
        foreach(string name in new[]{"d3d12","d3d12-quality"})Check(((byte[])Call(client,"EmbeddedBytes",null,"ProjectZek.Graphics."+name)).Length>0,"remaining graphics profile embedded");
        var defender=assembly.GetType("ProjectZekClassic.DefenderForm",true);string helper=(string)Call(defender,"ExtractHelper",null,root);
        using((FileStream)Call(defender,"OpenVerifiedHelper",null,helper))Check(true,"Defender helper extracts and verifies without external bundled script or elevation");
        Check(helper.StartsWith(Path.Combine(root,"ClassicFix"),StringComparison.OrdinalIgnoreCase),"optional helper is generated only inside selected client");
        string notices=Encoding.UTF8.GetString((byte[])Call(client,"EmbeddedBytes",null,"ProjectZek.LicenseNotices"));Check(notices.Contains("GNU GENERAL PUBLIC LICENSE")&&notices.Contains("MIT License"),"full license notices remain embedded");
        File.WriteAllText(Path.Combine(root,"LaunchSupport/patch-incomplete.txt"),"interrupted update");Check(Rejected(Verify),"interrupted patch still blocks Play");File.Delete(Path.Combine(root,"LaunchSupport/patch-incomplete.txt"));
        File.WriteAllText(Path.Combine(root,"ClassicFix/incomplete.json"),"interrupted classic fix");Check(Rejected(Verify),"interrupted local fix still blocks Play");File.Delete(Path.Combine(root,"ClassicFix/incomplete.json"));
        byte[] dll=File.ReadAllBytes(Path.Combine(root,"eqgame.dll"));File.Delete(Path.Combine(root,"eqgame.dll"));Check(Rejected(Verify),"missing required client file still blocks Play");File.WriteAllBytes(Path.Combine(root,"eqgame.dll"),dll);
        main.GetField("OfflineReview",Flags).SetValue(null,false);
        using(var form=(Form)Activator.CreateInstance(main)){
            var handle=form.Handle;
            ((Task)Call(main,"InitializeAsync",form)).GetAwaiter().GetResult();
            Check(!(bool)main.GetField("isLoading",Flags).GetValue(form)&&!form.Controls.Find("btnCheck",true)[0].Enabled&&!form.Controls.Find("btnStart",true)[0].Enabled,"blank endpoint opens normally and disables downloads/Play without network requests");
            Check(form.Text=="Project-Zek-Patcher","normal patcher title retained");
        }
        Console.WriteLine("SUMMARY\t"+passed+" checks passed; no game, browser, PowerShell or administrator action started.");return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
