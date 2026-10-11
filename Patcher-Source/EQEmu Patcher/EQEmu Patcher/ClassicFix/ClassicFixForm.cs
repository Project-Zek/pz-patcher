using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Text;
using System.Runtime.InteropServices;

namespace ProjectZekClassic {
    public sealed class ClassicFixForm : Form {
        readonly TextBox folder=new TextBox(),status=new TextBox();
        readonly CheckBox models=new CheckBox(),spells=new CheckBox(),loading=new CheckBox(),login=new CheckBox(),music=new CheckBox(),backgrounds=new CheckBox();
        readonly Button apply=new Button(),restore=new Button(),browse=new Button(),defender=new Button();
        bool busy;
        public ClassicFixForm(string root) {
            Text="Project Zek - Classic fixes";ClientSize=new Size(660,560);MinimumSize=new Size(676,599);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;
            Font=new Font("Segoe UI",10);Controls.Add(new Label {Text="Restore the classic look using files already on your computer.",Left=18,Top=16,Width=625,Height=28});
            folder.SetBounds(18,56,518,29);folder.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;folder.Text=root??"";
            browse.Text="Browse...";browse.SetBounds(546,54,96,31);browse.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            browse.Click+=(s,e)=>{using(var picker=new FolderBrowserDialog {Description="Choose the TAKPv22 folder containing eqgame.exe",SelectedPath=folder.Text})if(picker.ShowDialog(this)==DialogResult.OK)folder.Text=picker.SelectedPath;};
            Configure(models,"Classic skeletons and wolves",100,true);
            Configure(spells,"Classic spell effects",134,true);
            Configure(loading,"Classic loading screen",168,true);
            Configure(login,"Classic login screen",202,true);
            Configure(backgrounds,"Classic character backgrounds",236,true);
            Configure(music,"Classic music settings and zone definitions",270,false);
            Controls.Add(new Label {Text="Classic wolves include the corrected animations and height. Install the separate music pack first; Classic music then applies its zone definitions and enables in-game and character-screen music.",Left=18,Top=310,Width=625,Height=60});
            apply.Text="Apply selected fixes";apply.SetBounds(18,380,226,37);restore.Text="Restore latest fix";restore.SetBounds(256,380,186,37);defender.Text="Defender options...";defender.SetBounds(454,380,188,37);
            status.SetBounds(18,434,624,108);status.Multiline=true;status.ReadOnly=true;status.ScrollBars=ScrollBars.Vertical;status.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top|AnchorStyles.Bottom;
            status.Text="Choose your client folder, close EverQuest, then press Apply. Backups are kept for Restore. The music pack is a separate optional download.";
            Controls.AddRange(new Control[]{folder,browse,apply,restore,defender,status});
            apply.Click+=async(s,e)=>{string chosen=folder.Text;bool m=models.Checked,sp=spells.Checked,ld=loading.Checked,lg=login.Checked,mu=music.Checked,bg=backgrounds.Checked;await Run(()=>ClassicFix.ApplyWithMusic(chosen,m,sp,ld,lg,false,mu,bg));};
            restore.Click+=async(s,e)=>{string chosen=folder.Text;await Run(()=>ClassicFix.Restore(chosen));};
            defender.Click+=(s,e)=>{try{using(var dialog=new DefenderForm(ClassicFix.ClientRoot(folder.Text)))dialog.ShowDialog(this);}catch(Exception error){status.Text=error.Message;}};
            FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};AllowDrop=true;
            DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)&&!busy)e.Effect=DragDropEffects.Copy;};
            DragDrop+=(s,e)=>{var paths=(string[])e.Data.GetData(DataFormats.FileDrop);if(paths.Length==1)folder.Text=Directory.Exists(paths[0])?paths[0]:Path.GetDirectoryName(paths[0]);};
            folder.TextChanged+=(s,e)=>RefreshMusic(true);
            RefreshMusic(true);
        }
        void RefreshMusic(bool describe) {
            try {
                string root=ClassicFix.ClientRoot(folder.Text);
                music.Checked=MusicDefinitionAssets.SavedEnabled(root)&&MusicDefinitionAssets.MissingSupport(root).Length==0;
                if(describe)status.Text=MusicDefinitionAssets.Describe(root);
            }catch(Exception error){music.Checked=false;if(describe)status.Text=error.Message;}
        }
        void Configure(CheckBox box,string text,int top,bool selected){box.Text=text;box.SetBounds(22,top,610,30);box.Checked=selected;Controls.Add(box);}
        async Task Run(Func<string> action) {
            if(busy)return;busy=true;foreach(Control control in Controls)if(control!=status)control.Enabled=false;status.Text="Checking local files and preparing backups...";
            try {string result=await Task.Run(action);RefreshMusic(true);status.Text=result+Environment.NewLine+status.Text;}catch(Exception error){status.Text=error.Message;}
            finally {busy=false;foreach(Control control in Controls)control.Enabled=true;}
        }
    }
    sealed class DefenderForm : Form {
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool Wow64DisableWow64FsRedirection(out IntPtr previous);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool Wow64RevertWow64FsRedirection(IntPtr previous);
        readonly string root;
        readonly CheckBox path=new CheckBox(),process=new CheckBox();
        readonly Label status=new Label();readonly Button add=new Button(),remove=new Button();
        internal DefenderForm(string client) {
            root=client;Text="Optional Defender exclusions";ClientSize=new Size(650,340);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
            Controls.Add(new Label {Text="These settings reduce antivirus scanning. Use only for a known compatibility problem. Both are off by default; Windows will ask for administrator approval.",Left=18,Top=16,Width=614,Height=65});
            path.Text="Exclude this entire client folder";path.SetBounds(18,90,610,28);
            process.Text="Exclude files opened by this eqgame.exe process";process.SetBounds(18,122,610,28);
            Controls.Add(new Label {Text=root+Environment.NewLine+Path.Combine(root,"eqgame.exe"),Left=18,Top=160,Width=614,Height=58,AutoEllipsis=true});
            add.Text="Add selected";add.SetBounds(18,230,186,36);remove.Text="Remove tool-added";remove.SetBounds(216,230,186,36);
            status.SetBounds(18,279,614,48);Controls.AddRange(new Control[]{path,process,add,remove,status});
            add.Click+=(s,e)=>Run(false);remove.Click+=(s,e)=>Run(true);
        }
        static string Quote(string value) {
            // Windows command-line quoting; paths cannot contain a literal quote.
            return "\""+value.TrimEnd('\\')+"\"";
        }
        internal static FileStream OpenVerifiedHelper(string helper) {
            var file=new FileStream(helper,FileMode.Open,FileAccess.Read,FileShare.Read);
            try {
                using(var sha=System.Security.Cryptography.SHA256.Create())using(var expected=typeof(DefenderForm).Assembly.GetManifestResourceStream("ProjectZek.DefenderHelper")) {
                    if(expected==null||!sha.ComputeHash(file).SequenceEqual(sha.ComputeHash(expected)))throw new IOException("The Defender helper differs from this build. Restore the matching helper before requesting administrator approval.");
                }
                file.Position=0;return file;
            }catch{file.Dispose();throw;}
        }
        internal static string ExtractHelper(string client) {
            string helper=ClassicFix.Safe(client,"ClassicFix/Defender-Helper.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(helper));
            using(var output=new FileStream(helper,FileMode.Create,FileAccess.Write,FileShare.None)) {
                byte[] bytes=Client.EmbeddedBytes("ProjectZek.DefenderHelper");output.Write(bytes,0,bytes.Length);
            }
            return helper;
        }
        internal static ProcessStartInfo HelperCommand(string helper,string client,bool undo,bool folder,bool process) {
            string system=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32");
            return new ProcessStartInfo {FileName=Path.Combine(system,"WindowsPowerShell/v1.0/powershell.exe"),
                Arguments="-NoProfile -ExecutionPolicy Bypass -File "+Quote(helper)+" -ClientRoot "+Quote(client)+" -Action "+(undo?"Remove":"Add")+(folder?" -Folder":"")+(process?" -Process":""),UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        }
        void Run(bool undo) {
            if(!undo&&!path.Checked&&!process.Checked){status.Text="Select an exclusion first.";return;}
            try {
                string helper=ExtractHelper(root);
                // The UAC broker resolves System32. Sysnative is only visible to a 32-bit
                // process and cannot be used as the elevated shell's executable path.
                using(var verified=OpenVerifiedHelper(helper)) {
                var info=HelperCommand(helper,root,undo,path.Checked,process.Checked);
                Process child=null;IntPtr previous=IntPtr.Zero;bool redirected=false;
                try {
                    if(Environment.Is64BitOperatingSystem&&!Environment.Is64BitProcess){redirected=Wow64DisableWow64FsRedirection(out previous);if(!redirected)throw new IOException("Windows could not select native PowerShell.");}
                    child=Process.Start(info);
                }finally{if(redirected)Wow64RevertWow64FsRedirection(previous);}
                using(child){child.WaitForExit();status.Text=child.ExitCode==0?"Completed. See ClassicFix/defender-result.txt for details.":"Not applied. See ClassicFix/defender-result.txt or check Windows policy.";}
                }
            }catch(Exception error){status.Text=error.Message;}
        }
    }
    internal static class ClassicProgram {
        [STAThread] static int Main(string[] args) {
            if(args.Length>0&&args[0].StartsWith("--",StringComparison.Ordinal)) {
                try {
                    int index=Array.IndexOf(args,"--root");if(index<0||index+1>=args.Length)throw new ArgumentException("Supply --root and the client folder.");string root=args[index+1];
                    if(args[0]=="--restore")Console.WriteLine(ClassicFix.Restore(root));
                    else if(args[0]=="--verify-models"){ClassicFix.VerifyModels(ClassicFix.ClientRoot(root));Console.WriteLine("Model archives match a supported profile.");}
                    else if(args[0]=="--apply")Console.WriteLine(ClassicFix.Apply(root,args.Contains("--models"),args.Contains("--spells"),args.Contains("--loading"),args.Contains("--login"),args.Contains("--wolf-timing")));
                    else throw new ArgumentException("Use --apply, --restore or --verify-models.");return 0;
                }catch(Exception error){Console.Error.WriteLine(error.ToString());return 1;}
            }
            string folder=args.Length==1?(Directory.Exists(args[0])?args[0]:Path.GetDirectoryName(args[0])):AppDomain.CurrentDomain.BaseDirectory;
            if(!File.Exists(Path.Combine(folder,"eqgame.exe")))folder="";
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new ClassicFixForm(folder));return 0;
        }
    }
}
