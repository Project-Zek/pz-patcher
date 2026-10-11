using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

class DisplayOptionsTests {
    static int failed,passed;static Assembly assembly;static Type client,ini,formType;
    const BindingFlags Static=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static;
    const BindingFlags Instance=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
    static object Call(Type type,string method,object target,params object[] args) {
        try{return type.GetMethod(method,Static|Instance).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException;}
    }
    static void Check(bool value,string error){if(!value)throw new Exception(error);}
    static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS\t"+name);}catch(Exception e){failed++;Console.WriteLine("FAIL\t"+name+"\t"+e);}}
    static object Settings(){return ini.GetField("instance",Static).GetValue(null);}
    static void Set(string name,string value){ini.GetProperty(name).SetValue(Settings(),value,null);}
    static string Get(string name){return (string)ini.GetProperty(name).GetValue(Settings(),null);}
    static ComboBox Combo(Form form,string name){return (ComboBox)formType.GetField(name,Instance).GetValue(form);}
    static object Item(ComboBox combo,string field,string value){return combo.Items.Cast<object>().First(x=>x.GetType().GetField(field).GetValue(x).ToString()==value);}
    static string Value(ComboBox combo,string field){return combo.SelectedItem.GetType().GetField(field).GetValue(combo.SelectedItem).ToString();}
    static void Load(Form form){Call(formType,"LoadLaunchOptions",form);}
    static string Read(string path,string section,string key){return (string)Call(client,"ReadIni",null,path,section,key);}
    static void Layout(Form form){
        Check(form.Controls.Cast<Control>().All(c=>form.ClientRectangle.Contains(c.Bounds)),"Control outside client area");
        var combos=form.Controls.OfType<ComboBox>().ToArray();
        var image=form.Controls.OfType<PictureBox>().Single();
        Check(combos.All(c=>!image.Bounds.IntersectsWith(c.Bounds)),"Logo overlaps display controls");
        Check(combos.All(a=>combos.All(b=>a==b||!a.Bounds.IntersectsWith(b.Bounds))),"Display controls overlap");
        foreach(var combo in combos)Check(form.Controls.OfType<Button>().All(b=>!combo.Bounds.IntersectsWith(b.Bounds)),"Display control overlaps a button");
    }
    static void Render(Form form,string path){
        form.Text="Project-Zek-Patcher";form.CreateControl();
        using(var bitmap=new Bitmap(form.Width,form.Height)){
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
            using(var graphics=Graphics.FromImage(bitmap)){
                int border=(form.Width-form.ClientSize.Width)/2,top=form.Height-form.ClientSize.Height-border;
                foreach(Control control in form.Controls){
                    if(control.Name=="txtList")continue;
                    control.CreateControl();using(var child=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(child,new Rectangle(Point.Empty,control.Size));graphics.DrawImageUnscaled(child,border+control.Left,top+control.Top);}
                    var combo=control as ComboBox;
                    if(combo!=null){var inside=new Rectangle(border+combo.Left+3,top+combo.Top+2,combo.Width-24,combo.Height-4);
                        using(var brush=new SolidBrush(SystemColors.Window))graphics.FillRectangle(brush,inside);
                        TextRenderer.DrawText(graphics,combo.Text,combo.Font,inside,combo.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}
                }
            }bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    [STAThread] static int Main(string[] args){
        try{
            Application.EnableVisualStyles();assembly=Assembly.LoadFrom(args[0]);client=assembly.GetType("Client",true);
            ini=assembly.GetType("EQEmu_Patcher.IniLibrary",true);formType=assembly.GetType("EQEmu_Patcher.MainForm",true);
            client.GetField("Root",Static).SetValue(null,args[1]);formType.GetField("OfflineReview",Static).SetValue(null,true);
            string yaml=Path.Combine(Path.GetDirectoryName(Application.ExecutablePath),"eqemupatcher.yml");
            File.WriteAllText(yaml,"autoPatch: false\nautoPlay: false\nclientVersion: Rain_Of_Fear\nlastPatchedVersion: retained\ngraphicsMode: d3d12\nframeLimit: 120\nrefreshRate: 120\n");
            Call(ini,"Load",null);
            using(var form=(Form)Activator.CreateInstance(formType)){
                Test("old refresh settings load without resetting graphics or FPS",()=>{
                    Load(form);Check(Combo(form,"graphicsChoice").SelectedIndex==1&&Combo(form,"frameChoice").SelectedIndex==2,"Previous launch choices lost");
                    Check(Value(Combo(form,"resolutionChoice"),"Value")=="keep","Old settings changed monitor defaults");
                });
                Test("supported monitor resolutions are read without changing hardware",()=>{
                    var resolutions=Combo(form,"resolutionChoice");
                    Check(resolutions.Items.Count>1,"No real supported modes found");
                    File.WriteAllText(Path.Combine(args[2],"monitor-options.txt"),Screen.PrimaryScreen.DeviceName+Environment.NewLine+
                        String.Join(Environment.NewLine,resolutions.Items.Cast<object>().Select(x=>x.ToString())));
                });
                Test("graphics, FPS and resolution persist after saving and reopening",()=>{
                    Combo(form,"resolutionChoice").SelectedItem=Item(Combo(form,"resolutionChoice"),"Value","desktop");
                    Call(formType,"SaveLaunchOptions",form);Call(ini,"Load",null);
                    using(var reopened=(Form)Activator.CreateInstance(formType)){
                        Load(reopened);Check(Value(Combo(reopened,"resolutionChoice"),"Value")=="desktop","Display choice lost");
                        Check(Combo(reopened,"graphicsChoice").SelectedIndex==1&&Combo(reopened,"frameChoice").SelectedIndex==2,"Graphics/FPS choice lost");
                    }
                    Check(Get("LastPatchedVersion")=="retained"&&Get("AutoPatch")=="false","Unrelated patcher settings lost");
                    Check(!File.ReadAllText(yaml).Contains("refreshRate:"),"Obsolete refresh choice was saved again");
                });
                Test("unavailable saved monitor modes fall back without changing saved preferences",()=>{
                    Set("DisplayResolution","12345x99999");Call(ini,"Save",null);string before=File.ReadAllText(yaml);
                    Load(form);Check(Value(Combo(form,"resolutionChoice"),"Value")=="keep","Unsupported choice selected");
                    Check(File.ReadAllText(yaml)==before,"Loading altered saved preference");
                });
                Test("normal window layout fits all three launch controls",()=>{form.ClientSize=new Size(640,680);Layout(form);Render(form,Path.Combine(args[2],"display-normal.png"));});
                Test("compact window layout fits all three launch controls",()=>{form.ClientSize=new Size(640,526);Layout(form);Render(form,Path.Combine(args[2],"display-compact.png"));});
            }
            string eq=Path.Combine(args[1],"eqclient.ini");string original=File.ReadAllText(eq);
            Test("keeping display settings makes no additional game INI change",()=>{
                Call(client,"ApplyResolution",null,null,null);Check(File.ReadAllText(eq)==original,"Keep changed INI");
            });
            Test("resolution is applied to client INI while unrelated preferences remain intact",()=>{
                File.WriteAllText(eq,"[Defaults]\r\nSound=TRUE\r\nNewUI=TRUE\r\nShowSpellEffects=1\r\nPersonal=KeepMe\r\n[VideoMode]\r\nWidth=1024\r\nHeight=768\r\nRefreshRate=60\r\n[Personal]\r\nData=Preserve\r\n");
                Call(client,"ApplyResolution",null,1280,720);
                Check(Read(eq,"Defaults","VideoMode")=="-1"&&Read(eq,"Defaults","VideoModeBitsPerPixel")=="32","Custom resolution mode not enabled");
                Check(Read(eq,"VideoMode","Width")=="1280"&&Read(eq,"VideoMode","Height")=="720"&&Read(eq,"VideoMode","BitsPerPixel")=="32"&&Read(eq,"VideoMode","RefreshRate")=="0","Resolution not written");
                Check(Read(eq,"Defaults","Sound")=="TRUE"&&Read(eq,"Defaults","NewUI")=="TRUE"&&Read(eq,"Defaults","ShowSpellEffects")=="1"&&Read(eq,"Defaults","Personal")=="KeepMe"&&Read(eq,"Personal","Data")=="Preserve","Unrelated setting altered");
            });
            Test("compiled patcher has no refresh UI or monitor-changing launch API",()=>{
                Check(formType.GetField("refreshChoice",Instance)==null,"Refresh UI remains");
                Check(client.GetMethod("LaunchWithRefresh",Static)==null,"Refresh helper launch remains");
                var api=assembly.GetType("ProjectZekDisplay.WindowsDisplayApi",true);
                Check(api.GetMethod("ChangeDisplaySettingsEx",Static)==null&&api.GetMethod("Change",Instance)==null,"Monitor-changing API remains");
            });
            Test("resolution reader reports the selected client size",()=>{
                object[] values={null,null};client.GetMethod("ReadResolution",Static).Invoke(null,values);Check((int)values[0]==1280&&(int)values[1]==720,"Wrong client resolution");
            });
            Test("partial resolutions are rejected before touching client or monitor",()=>{
                string before=File.ReadAllText(eq);bool refused=false;try{Call(client,"ValidateDisplay",null,1280,null,Screen.PrimaryScreen.DeviceName);}catch(IOException){refused=true;}
                Check(refused&&File.ReadAllText(eq)==before,"Partial resolution accepted");
            });
            Test("changed monitor and unsupported modes are rejected without applying them",()=>{
                foreach(object[] values in new[]{new object[]{1280,720,"wrong-monitor"},new object[]{9999,9999,Screen.PrimaryScreen.DeviceName}}){
                    bool refused=false;try{Call(client,"ValidateDisplay",null,values);}catch(IOException){refused=true;}Check(refused,"Unavailable display option accepted");
                }
            });
            File.WriteAllText(eq,original);Console.WriteLine("SUMMARY\t"+passed+" passed; "+failed+" failed. No hardware display changes or game launches performed.");return failed==0?0:1;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
