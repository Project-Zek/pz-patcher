using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class MusicPackCompatibilityTests {
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly assembly;
    static Type assets,main,client,core;
    static string root,manifest;
    static object verifier;
    static int failed;
    static object Call(Type type,string name,object target,params object[] args) {
        try {return type.GetMethod(name,Flags).Invoke(target,args);}
        catch(TargetInvocationException e) {throw e.InnerException;}
    }
    static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
    static void Test(string name,Action action) {try {action();Console.WriteLine("PASS\t"+name);}catch(Exception e){failed++;Console.WriteLine("FAIL\t"+name+"\t"+e);}}
    static void Reject(Action action,string expected=null) {try{action();}catch(IOException e){Check(expected==null||e.Message.Contains(expected),e.Message);return;}throw new Exception("Invalid input accepted");}
    static Dictionary<string,byte[]> Stage() {return (Dictionary<string,byte[]>)Call(assets,"Stage",null,root);}
    static void Verify() {Call(main,"VerifyManifestBeforePlay",verifier);Call(client,"Verify",null);}
    static string Md5(string path) {using(var m=MD5.Create())return BitConverter.ToString(m.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static string Row(string name,string hash,long size) {return "- name: \""+name+"\"\n  md5: "+hash+"\n  size: "+size+"\n  date: \"20261011\"\n";}
    static string FileRow(string name) {string file=Path.Combine(root,name);return Row(name,Md5(file),new FileInfo(file).Length);}
    [STAThread] static int Main(string[] args) {try {
        assembly=Assembly.LoadFrom(args[0]);assets=assembly.GetType("ProjectZekClassic.MusicDefinitionAssets",true);root=args[1];
        if(args.Length>2&&args[2]=="--expect-old-rejection") {Reject(()=>Stage(),"music/pvp_quake_neutral.mp3");Console.WriteLine("PASS\tOriginal patcher reproduces the updated-neutral/old-catalog rejection");return 0;}
        main=assembly.GetType("EQEmu_Patcher.MainForm",true);client=assembly.GetType("Client",true);core=assembly.GetType("ProjectZekClassic.ClassicFix",true);
        client.GetField("Root",Flags).SetValue(null,root);main.GetField("OfflineReview",Flags).SetValue(null,true);verifier=FormatterServices.GetUninitializedObject(main);
        Test("Updated PvP recordings work with the existing R1 music catalog",()=>Check(Stage().Count==109,"Definition count differs"));
        Test("All three PvP cues may be absent without blocking Classic music Apply",()=>{
            var names=new[]{"evil","good","neutral"}.Select(t=>Path.Combine(root,"music/pvp_quake_"+t+".mp3")).ToArray();
            foreach(var n in names)File.Move(n,n+".held");
            try{Check(Stage().Count==109,"Optional cues affected definitions");}finally{foreach(var n in names)File.Move(n+".held",n);}
        });
        Test("Another valid PvP recording can replace neutral without rebuilding metadata",()=>{
            string path=Path.Combine(root,"music/pvp_quake_neutral.mp3");byte[] before=File.ReadAllBytes(path);
            try{File.Copy(Path.Combine(root,"music/pvp_quake_good.mp3"),path,true);Check(Stage().Count==109,"Recording replacement rejected");}finally{File.WriteAllBytes(path,before);}
        });
        string combat=Path.Combine(root,"music/trilogy_gl_combat_1.mp3");
        Test("Missing required Classic combat music still refuses Apply",()=>{File.Move(combat,combat+".held");try{Reject(()=>Stage(),"trilogy_gl_combat_1.mp3");}finally{File.Move(combat+".held",combat);}});
        Test("Damaged required Classic music still refuses Apply",()=>{byte[] before=File.ReadAllBytes(combat);try{File.WriteAllBytes(combat,new byte[]{1,2,3});Reject(()=>Stage(),"trilogy_gl_combat_1.mp3");}finally{File.WriteAllBytes(combat,before);}});
        Test("Damaged EMT support package still refuses Apply",()=>{
            string path=Path.Combine(root,"LaunchSupport/classic-music-definitions.zip");byte[] before=File.ReadAllBytes(path);
            try{File.WriteAllBytes(path,new byte[]{1,2,3});Reject(()=>Stage(),"incomplete or changed");}finally{File.WriteAllBytes(path,before);}
        });
        Test("All staged definitions remain the original tuned EMT bytes",()=>{foreach(var row in Stage())Check(row.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(root,row.Key))),row.Key+" changed");});
        foreach(string name in new[]{"eqgame.exe","eqmain.dll","eqgfx_dx8.dll","mss32.dll","eqw.dll","D3D8.dll","Zeal.asi","eqgame.dll"})File.WriteAllText(Path.Combine(root,name),"PRIVATE-FIXTURE-NOT-EXECUTABLE");
        File.WriteAllText(Path.Combine(root,"eqclient.ini"),"[Defaults]\r\nSound=TRUE\r\n");
        File.WriteAllText(Path.Combine(root,"zeal.ini"),"[Zeal]\r\nFPS=120\r\nPersonalSetting=KeepMe\r\n");
        var catalog=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(root,"LaunchSupport/classic-music-definitions.json"))) as Dictionary<string,object>;
        var text=new StringBuilder("version: legacy-music-catalog\ndownloadprefix: https://example.invalid/\ndownloads:\n"+FileRow("Zeal.asi")+FileRow("eqgame.dll"));
        foreach(string key in new[]{"audioFiles","files"})foreach(var value in (object[])catalog[key]) {
            var row=(Dictionary<string,object>)value;text.Append(Row((string)row["path"],(string)row["md5"],Convert.ToInt64(row["bytes"])));
        }
        foreach(string name in new[]{"classic-music-definitions.json","classic-music-definitions.zip","classic-music-files.sha256"})text.Append(FileRow("LaunchSupport/"+name));
        manifest=text+"deletes: []\n";File.WriteAllText(Path.Combine(root,"filelist-review.yml"),manifest);
        Test("Play accepts updated optional tracks despite old manifest MD5 values",()=>Verify());
        Test("Play works with the entire external music folder and support files absent",()=>{
            string music=Path.Combine(root,"music"),support=Path.Combine(root,"LaunchSupport");Directory.Move(music,music+".held");Directory.Move(support,support+".held");
            try{Verify();}finally{Directory.Move(music+".held",music);Directory.Move(support+".held",support);}
        });
        Test("Changed optional EMT definitions cannot block Play",()=>{string path=Path.Combine(root,"gfaydark.emt");byte[] before=File.ReadAllBytes(path);try{File.AppendAllText(path,";custom music setting\n");Verify();}finally{File.WriteAllBytes(path,before);}});
        Test("Changed mandatory Zeal still fails current-manifest verification",()=>{string path=Path.Combine(root,"Zeal.asi");byte[] before=File.ReadAllBytes(path);try{File.AppendAllText(path,"changed");Reject(()=>Verify(),"Zeal.asi");}finally{File.WriteAllBytes(path,before);}});
        Test("Optional music paths still reject traversal before skipping hashes",()=>{
            string path=Path.Combine(root,"filelist-review.yml");File.WriteAllText(path,manifest.Replace("deletes: []","deletes:\n"+Row("music/../../outside.mp3",new string('0',32),1)));
            try{Reject(()=>Verify());}finally{File.WriteAllText(path,manifest);}
        });
        Test("Native code disguised as a music-directory file remains mandatory",()=>{
            string path=Path.Combine(root,"filelist-review.yml");File.WriteAllText(path,manifest.Replace("deletes: []",Row("music/required.dll",new string('0',32),1)+"deletes: []"));
            try{Reject(()=>Verify(),"music/required.dll");}finally{File.WriteAllText(path,manifest);}
        });
        Test("Legacy optional-music delete entries cannot block Play",()=>{
            string path=Path.Combine(root,"filelist-review.yml");File.WriteAllText(path,manifest.Replace("deletes: []","deletes:\n"+FileRow("music/pvp_quake_neutral.mp3")));
            try{Verify();}finally{File.WriteAllText(path,manifest);}
        });
        byte[] originalIni=File.ReadAllBytes(Path.Combine(root,"zeal.ini"));
        Test("Classic music Apply enables character-creation and world music with updated PvP",()=>{
            Call(core,"ApplyWithMusic",null,root,false,false,false,false,false,true,false);
            string ini=File.ReadAllText(Path.Combine(root,"zeal.ini"));Check(ini.Contains("ClassicMusic=TRUE")&&ini.Contains("ClassicMenuMusic=TRUE")&&ini.Contains("FPS=120")&&ini.Contains("PersonalSetting=KeepMe"),"Music/menu preferences incorrect");
        });
        Test("Reopening Classic fixes keeps Classic music selected",()=>{
            using(var form=(Form)Activator.CreateInstance(assembly.GetType("ProjectZekClassic.ClassicFixForm",true),root))Check(form.Controls.OfType<CheckBox>().Single(x=>x.Text.StartsWith("Classic music")).Checked,"Music state not remembered");
        });
        Test("Restore preserves the original preferences after the music update",()=>{
            Call(core,"Restore",null,root);Check(File.ReadAllBytes(Path.Combine(root,"zeal.ini")).SequenceEqual(originalIni),"Preferences not restored");Verify();
        });
        Console.WriteLine("No game, server event or elevation started.");return failed==0?0:1;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
