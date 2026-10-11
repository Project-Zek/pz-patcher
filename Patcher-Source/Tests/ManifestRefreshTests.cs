using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class ManifestRefreshTests {
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly assembly; static Type main,utility; static int checks;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;Console.WriteLine("PASS\t"+why);}
    static object Call(Type t,string n,object o,params object[] args){try{return t.GetMethod(n,F).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static string root;
    static void RunPatch(Form form){
        Call(main,"StartPatch",form);
        var end=DateTime.UtcNow.AddMinutes(8);
        while((bool)main.GetField("isPatching",F).GetValue(form)){
            if(DateTime.UtcNow>end)throw new Exception("Patch timed out.");
            Application.DoEvents();Thread.Sleep(10);
        }
        Application.DoEvents();
    }
    static int FileCount(Form form){object m=Call(main,"ReadManifest",form);return ((IList)m.GetType().GetProperty("downloads").GetValue(m,null)).Count;}
    class CacheHandler:HttpMessageHandler {
        public readonly List<Uri> Uris=new List<Uri>();public int Version=1;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken token){
            token.ThrowIfCancellationRequested();
            bool fresh=r.RequestUri.Query.Contains("_pz_metadata=")&&r.Headers.CacheControl.NoCache&&r.Headers.CacheControl.NoStore&&r.Headers.Pragma.ToString().Contains("no-cache");
            Uris.Add(r.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(fresh?"version-"+Version:"stale-version",Encoding.UTF8)});
        }
    }
    class ResponseHandler:HttpMessageHandler {
        public HttpStatusCode Code=HttpStatusCode.OK;public long? Length;public bool Requested;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken token){
            Requested=true;token.ThrowIfCancellationRequested();
            var response=new HttpResponseMessage(Code){Content=new ByteArrayContent(new byte[]{1,2})};
            if(Length.HasValue)response.Content.Headers.ContentLength=Length;
            return Task.FromResult(response);
        }
    }
    static byte[] Fetch(HttpClient client,string url,CancellationTokenSource cts){return ((Task<byte[]>)Call(utility,"DownloadMetadata",null,cts,url,client)).GetAwaiter().GetResult();}
    static bool Reject(Action action,Type type){try{action();return false;}catch(Exception e){return type.IsInstanceOfType(e);}}
    static void MetadataChecks(){
        string url="https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/filelist.yml?existing=value";
        using(var handler=new CacheHandler())using(var http=new HttpClient(handler))using(var cts=new CancellationTokenSource()){
            Check(Encoding.UTF8.GetString(Fetch(http,url,cts))=="version-1","metadata bypasses an earlier cached branch manifest");
            handler.Version=2;
            Check(Encoding.UTF8.GetString(Fetch(http,url,cts))=="version-2"&&handler.Uris[0]!=handler.Uris[1],"consecutive metadata requests receive the new version");
            Check(handler.Uris[0].Query.Contains("existing=value"),"cache bypass preserves existing GitHub query values");
        }
        string signed="https://example.org/current.yml?signature=keep-this";
        Check(((Uri)Call(utility,"FreshMetadataUri",null,signed)).AbsoluteUri==signed,"other hosts keep their complete configured URL");
        using(var handler=new ResponseHandler())using(var http=new HttpClient(handler))using(var cts=new CancellationTokenSource()){
            Check(Reject(()=>Fetch(http,"http://example.org/current.yml",cts),typeof(IOException))&&!handler.Requested,"HTTP metadata rejected before requesting it");
            handler.Code=HttpStatusCode.NotFound;
            Check(Reject(()=>Fetch(http,signed,cts),typeof(HttpRequestException)),"missing manifest response cannot be used as metadata");
            handler.Code=HttpStatusCode.OK;handler.Length=16L*1024*1024+1;
            Check(Reject(()=>Fetch(http,signed,cts),typeof(IOException)),"oversized manifest rejected");
            handler.Length=3;
            Check(Reject(()=>Fetch(http,signed,cts),typeof(IOException)),"truncated manifest rejected");
            handler.Length=2;cts.Cancel();
            Check(Reject(()=>Fetch(http,signed,cts),typeof(OperationCanceledException)),"cancelled manifest fetch cannot continue patching");
        }
    }
    [STAThread] static int Main(string[] args){try{
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;Application.EnableVisualStyles();
        assembly=Assembly.LoadFrom(args[0]);root=Path.GetDirectoryName(Application.ExecutablePath);
        main=assembly.GetType("EQEmu_Patcher.MainForm",true);utility=assembly.GetType("EQEmu_Patcher.UtilityLibrary",true);
        Check(assembly.GetName().Version.ToString()=="1.0.19.0","compiled patcher version is 1.0.19");
        MetadataChecks();
        assembly.GetType("Client",true).GetField("Root",F).SetValue(null,root);
        main.GetField("OfflineReview",F).SetValue(null,false);
        main.GetField("filelistUrl",F).SetValue(null,"https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/filelist.yml");
        Call(assembly.GetType("EQEmu_Patcher.IniLibrary",true),"Load",null);
        using(var form=(Form)Activator.CreateInstance(main)){
            var handle=form.Handle;
            Check(FileCount(form)==78,"fixture starts with the old 78-file list reported by the user");
            RunPatch(form);
            Check(FileCount(form)==388,"actual Patch action replaces the old list with the current 388-file GitHub manifest");
            Check(Directory.GetFiles(Path.Combine(root,"uifiles/pz_ui"),"*",SearchOption.AllDirectories).Length==310,"actual Patch action downloads all 310 UI files including Extras");
            Call(main,"VerifyManifestBeforePlay",form);Check(true,"downloaded UI and runtime pass actual Play manifest validation");
            Check(File.ReadAllText(Path.Combine(root,"eqclient.ini"))=="PLAYER-SETTINGS-UNCHANGED"&&File.ReadAllText(Path.Combine(root,"zeal.ini"))=="ZEAL-SETTINGS-UNCHANGED","player settings retained");
            string optional=Path.Combine(root,"uifiles/pz_ui/Extras/HP_Number White (standard)/HP_Number White - No ExpPerHourBar/EQUI_PlayerWindow.xml");
            File.Copy(Path.Combine(root,"old-filelist.txt"),Path.Combine(root,"filelist.yml"),true);
            File.WriteAllText(optional,"DAMAGED-UI-FILE");
            RunPatch(form);
            Check(FileCount(form)==388&&File.ReadAllText(optional)!="DAMAGED-UI-FILE","second Patch refreshes a stale local list and repairs a deep optional UI file");
            Call(main,"VerifyManifestBeforePlay",form);Check(true,"repaired release passes Play validation");
            File.WriteAllText(optional,"PRESERVE-AFTER-FAILED-REFRESH");
            main.GetField("filelistUrl",F).SetValue(null,"https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/missing-"+Guid.NewGuid().ToString("N")+".yml");
            RunPatch(form);
            Check(File.ReadAllText(optional)=="PRESERVE-AFTER-FAILED-REFRESH","failed refresh stops Patch before changing any client file");
            Check(FileCount(form)==388&&!File.Exists(Path.Combine(root,"LaunchSupport/patch-incomplete.txt")),"failed fetch preserves valid metadata and leaves no incomplete install marker");
            Check(File.ReadAllText(Path.Combine(root,"eqemupatcher.log")).Contains("Could not refresh the server file list"),"failed refresh explains the error in the patcher log");
            File.Delete(optional);
            main.GetField("filelistUrl",F).SetValue(null,"https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/filelist.yml");RunPatch(form);
            Call(main,"VerifyManifestBeforePlay",form);Check(true,"Patch recovers after a failed refresh");
            Check(!Directory.Exists(Path.Combine(root,"music")),"core Patch keeps music optional and separate");
        }
        Console.WriteLine("SUMMARY\t"+checks+" passed; no game started.");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
