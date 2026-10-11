using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Security.Cryptography;
class PatcherFormTests {
    static int failed;
    static void Test(string name,Action action) {try{action();Console.WriteLine("PASS\t"+name);}catch(Exception e){failed++;Console.WriteLine("FAIL\t"+name+"\t"+e.Message);}}
    static void Check(bool ok,string why) {if(!ok)throw new Exception(why);}
    static void Verify(Form form) {form.GetType().GetMethod("VerifyManifestBeforePlay",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);}
    static void Reject(Form form) {bool rejected=false;try{Verify(form);}catch(TargetInvocationException){rejected=true;}Check(rejected,"Play verification accepted incomplete data");}
    [STAThread] static int Main(string[] args) {
        AppDomain.CurrentDomain.UnhandledException+=(s,e)=>{Console.Error.WriteLine(e.ExceptionObject);Environment.Exit(1);};
        Application.ThreadException+=(s,e)=>{Console.Error.WriteLine(e.Exception);Environment.Exit(1);};
        try { return Run(args); } catch(Exception error) {Console.Error.WriteLine(error);return 1;}
    }
    static int Run(string[] args) {
        Application.EnableVisualStyles();
        var assembly=Assembly.LoadFrom(args[0]);var type=assembly.GetType("EQEmu_Patcher.MainForm");
        var reviewMode=type.GetField("OfflineReview",BindingFlags.NonPublic|BindingFlags.Static);
        reviewMode.SetValue(null,true);
        var client=assembly.GetType("Client");client.GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,args[1]);
        using(var form=(Form)Activator.CreateInstance(type)) {
            Test("compiled executable defaults to its explicitly built review or online mode",()=>{
                var select=assembly.GetType("EQEmu_Patcher.Program").GetMethod("SelectReviewMode",BindingFlags.NonPublic|BindingFlags.Static);
                bool online=args.Length>3&&args[3]=="online";
                Check((bool)select.Invoke(null,new object[]{new string[0]})==!online,"Incorrect default mode");
                Check((bool)select.Invoke(null,new object[]{new[]{"--offline"}}),"Explicit offline mode rejected");
            });
            Test("compiled patcher form has graphics, frame-limit, resolution and repair controls",()=>{
                var combos=form.Controls.OfType<ComboBox>().ToArray();Check(combos.Length==3,"Missing options");
                Check(combos.Any(x=>x.Items.Count==4)&&combos.Any(x=>x.Items.Count==7),"Wrong option counts");
                Check(type.GetField("refreshChoice",BindingFlags.NonPublic|BindingFlags.Instance)==null,"Obsolete refresh control retained");
                Check(client.GetMethod("LaunchWithRefresh",BindingFlags.NonPublic|BindingFlags.Static)==null,"Obsolete monitor-switch launch retained");
                var play=form.Controls.Find("btnStart",true)[0];
                Check(combos.All(x=>!x.Bounds.IntersectsWith(play.Bounds)),"Options overlap Play");
                var log=form.Controls.Find("txtList",true)[0];Check(combos.All(x=>!x.Bounds.IntersectsWith(log.Bounds)),"Options overlap log");
                Check(combos.All(x=>form.Controls.OfType<CheckBox>().All(c=>!x.Bounds.IntersectsWith(c.Bounds))),"Options overlap automatic launch controls");
            });
            Test("compiled review verifies its separate MD5 manifest despite an old online cache",()=>Verify(form));
            string cache=Path.Combine(args[1],"filelist.yml"),local=Path.Combine(args[1],"filelist-review.yml");
            string oldCache=File.ReadAllText(cache),current=File.ReadAllText(local);
            Test("compiled online patcher rejects the old nested Zeal layout before using it",()=>{
                try {
                    reviewMode.SetValue(null,false);bool rejected=false;
                    try{Verify(form);}catch(TargetInvocationException error){rejected=error.InnerException.Message.Contains("older Zeal/ layout");}
                    Check(rejected,"Old layout was not explicitly rejected");
                } finally {reviewMode.SetValue(null,true);}
            });
            Test("compiled online patcher accepts the complete current root layout",()=>{
                try{reviewMode.SetValue(null,false);File.WriteAllText(cache,current);Verify(form);}
                finally{File.WriteAllText(cache,oldCache);reviewMode.SetValue(null,true);}
            });
            Test("compiled patcher reads an updated Zeal MD5 from the manifest without recompilation",()=>{
                string zeal=Path.Combine(args[1],"Zeal.asi");byte[] original=File.ReadAllBytes(zeal),changed=(byte[])original.Clone();changed[changed.Length-1]^=1;
                using(var md5=MD5.Create()) {
                    string before=BitConverter.ToString(md5.ComputeHash(original)).Replace("-","").ToLowerInvariant();
                    string after=BitConverter.ToString(md5.ComputeHash(changed)).Replace("-","").ToLowerInvariant();
                    Check(current.Contains(before),"Fixture missing Zeal MD5");
                    try{File.WriteAllBytes(zeal,changed);File.WriteAllText(local,current.Replace(before,after));Verify(form);}
                    finally{File.WriteAllBytes(zeal,original);File.WriteAllText(local,current);}
                }
            });
            Test("compiled review refuses patching even if its patch method is invoked directly",()=>{
                type.GetField("isLoading",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,false);
                type.GetMethod("StartPatch",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                Check(!(bool)type.GetField("isPatching",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form),"Review started patching");
            });
            Test("compiled patcher launch core checks required files without a pinned SHA list",()=>client.GetMethod("Verify",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null));
            var utility=assembly.GetType("EQEmu_Patcher.UtilityLibrary");
            Test("compiled bootstrap rejects HTTP metadata",()=>{
                var task=(System.Threading.Tasks.Task<string>)utility.GetMethod("DownloadFile").Invoke(null,new object[]{new System.Threading.CancellationTokenSource(),"http://example.invalid/manifest","filelist.yml"});
                Check(task.GetAwaiter().GetResult().Contains("HTTPS"),"Bootstrap accepted HTTP");
            });
            Test("compiled bootstrap rejects traversal before downloading",()=>{
                var task=(System.Threading.Tasks.Task<string>)utility.GetMethod("DownloadFile").Invoke(null,new object[]{new System.Threading.CancellationTokenSource(),"https://example.invalid/manifest","../outside.yml"});
                Check(task.GetAwaiter().GetResult().Contains("Unsafe"),"Bootstrap accepted traversal");
            });
            string path=Path.Combine(args[1],"music/pvp_quake_good.mp3");byte[] old=File.ReadAllBytes(path);
            Test("compiled patcher permits Play without optional PvP event music",()=>{try{File.Delete(path);Verify(form);}finally{File.WriteAllBytes(path,old);}});
            path=Path.Combine(args[1],"LaunchSupport/patch-incomplete.txt");
            Test("compiled patcher blocks Play after an interrupted installation",()=>{try{File.WriteAllText(path,"review transaction");Reject(form);}finally{File.Delete(path);}});
            Test("compiled patcher Play handler refuses during patching and initial load",()=>{
                var play=type.GetMethod("PlayGame",BindingFlags.NonPublic|BindingFlags.Instance);
                foreach(string state in new[]{"isPatching","isLoading"}) {
                    var field=type.GetField(state,BindingFlags.NonPublic|BindingFlags.Instance);field.SetValue(form,true);play.Invoke(form,null);field.SetValue(form,false);
                }
            });
            form.Text="Project-Zek-Patcher";
            using(var bitmap=new Bitmap(form.Width,form.Height)) {
                form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
                using(var graphics=Graphics.FromImage(bitmap)) {
                    int border=(form.Width-form.ClientSize.Width)/2, top=form.Height-form.ClientSize.Height-border;
                    foreach(Control control in form.Controls) {
                        if(control.Name=="txtList")continue; // Initial splash state, before asynchronous loading.
                        using(var child=new Bitmap(control.Width,control.Height)) {control.CreateControl();control.DrawToBitmap(child,new Rectangle(Point.Empty,control.Size));graphics.DrawImageUnscaled(child,border+control.Left,top+control.Top);}
                        // Native dropdown text is not reliably returned by offscreen WM_PRINT.
                        var combo=control as ComboBox;
                        if(combo!=null) {
                            var inside=new Rectangle(border+combo.Left+3,top+combo.Top+2,combo.Width-24,combo.Height-4);
                            using(var brush=new SolidBrush(SystemColors.Window)) graphics.FillRectangle(brush,inside);
                            TextRenderer.DrawText(graphics,combo.Text,combo.Font,inside,combo.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
                        }
                    }
                }
                bitmap.Save(args[2],System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        return failed==0?0:1;
    }
}
