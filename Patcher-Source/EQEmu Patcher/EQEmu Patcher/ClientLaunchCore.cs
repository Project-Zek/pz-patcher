using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

// Game-specific launcher. No credentials, downloads, registry edits or elevation.
internal static partial class Client
{
    internal static string Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    internal static readonly Encoding TextEncoding = new UTF8Encoding(false);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool WritePrivateProfileString(string section, string key, string value, string file);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    static extern uint GetPrivateProfileString(string section, string key, string fallback, StringBuilder value, uint size, string file);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool CreateProcess(string application, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit,
        uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo info);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct StartupInfo { public int cb; public string reserved, desktop, title; public int x,y,xsize,ysize,xchars,ychars,fill;
        public uint flags; public short show,reserved2; public IntPtr reservedPtr,input,output,error; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr process,thread; public uint id,threadId; }
    internal static string PathFor(string relative)
    {
        if (String.IsNullOrEmpty(relative) || relative.IndexOf(':')>=0 || relative.IndexOf('\\')>=0 ||
            relative.StartsWith("/") || relative.Split('/').Any(p=>p==".." || p=="." || p.Length==0))
            throw new IOException("Invalid package path.");
        string path=Path.GetFullPath(Path.Combine(Root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid package path.");
        for(string p=path; p!=null; p=Path.GetDirectoryName(p)) {
            if((File.Exists(p)||Directory.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Use a client folder without linked files or folders: "+relative);
            if(p.Equals(Root,StringComparison.OrdinalIgnoreCase)) break;
        }
        return path;
    }
    static string Digest(string path) { using(var sha=SHA256.Create()) using(var f=File.OpenRead(path))
        return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
    static void CheckList(string manifest)
    {
        var lines=File.ReadAllLines(PathFor(manifest));
        if(lines.Length==0) throw new IOException("Verification list is empty.");
        foreach(string line in lines) {
            if(String.IsNullOrWhiteSpace(line)) continue;
            var parts=line.Split('\t');
            if(parts.Length!=2 || parts[0].Length!=64 || !parts[0].All(c=>"0123456789abcdef".Contains(c)))
                throw new IOException("Verification list is invalid.");
            string file=PathFor(parts[1]);
            if(!File.Exists(file)) throw new IOException("Missing file: "+parts[1]+". Run the patcher again.");
            if(Digest(file)!=parts[0] && !ProjectZekClassic.ClassicFix.ApprovedAsset(Root,parts[1])) throw new IOException("File needs repair: "+parts[1]+". Run the patcher again.");
        }
    }
    internal static void Verify()
    {
        CheckList("LaunchSupport/files.sha256");
        CheckList("LaunchSupport/baseline.sha256");
        ProjectZekClassic.ClassicFix.VerifyModels(Root);
        var allowed=new HashSet<string>(new[]{"Zeal.asi","mssmp3.asi","mssvoice.asi"},StringComparer.OrdinalIgnoreCase);
        foreach(string f in Directory.GetFiles(Root,"*.asi")) if(!allowed.Contains(Path.GetFileName(f)))
            throw new IOException("Move this extra plugin outside the client folder: "+Path.GetFileName(f)+". Plugin backups also load.");
        if(File.Exists(Path.Combine(Root,"Zeal.dll"))) throw new IOException("Move the old Zeal.dll outside the client folder.");
        if(File.Exists(Path.Combine(Root,"nosound.txt"))) throw new IOException("Move nosound.txt outside the client folder. Sound must be enabled to load Zeal.");
    }
    internal static void Idle()
    {
        // Conservative: never replace shared files while any EQ client is running.
        if(Process.GetProcessesByName("eqgame").Length!=0) throw new IOException("Close all EverQuest clients before changing settings or starting this check.");
    }
    static string ReadIni(string path,string section,string key) {
        var text=new StringBuilder(4096); GetPrivateProfileString(section,key,"",text,4096,path); return text.ToString(); }
    static void Backup(string path) {
        if(File.Exists(path)) {
            var folder=PathFor("LaunchSupport/backups"); Directory.CreateDirectory(folder);
            string name=Path.GetFileName(path)+"."+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"."+Guid.NewGuid().ToString("N")+".bak";
            File.Copy(path,Path.Combine(folder,name));
        }
    }
    static void SetIni(string file,string section,string key,string value,bool onlyMissing)
    {
        string path=PathFor(file), old=ReadIni(path,section,key);
        if((onlyMissing && old.Length!=0) || old.Equals(value,StringComparison.OrdinalIgnoreCase)) return;
        if(!WritePrivateProfileString(section,key,value,path)) throw new IOException("Cannot save "+file+". Move the client to a folder you can write to.");
    }
    internal static void Prepare(string renderer,bool repair)
    {
        Verify(); Idle();
        // Settings files can be user-modified; they are deliberately not hashed as package files.
        Backup(PathFor("eqclient.ini")); Backup(PathFor("zeal.ini"));
        SetIni("eqclient.ini","Defaults","Sound","TRUE",false);
        SetIni("eqclient.ini","Defaults","NewUI","TRUE",false);
        SetIni("eqclient.ini","EqwGeneral","FullScreenMode","FALSE",!repair);
        SetIni("eqclient.ini","EqwOffsets","LoginX","0",!repair);
        SetIni("eqclient.ini","EqwOffsets","LoginY","0",!repair);
        SetIni("eqclient.ini","VideoMode","BitsPerPixel","32",false);
        SetIni("eqclient.ini","VideoMode","Width","1024",!repair);
        SetIni("eqclient.ini","VideoMode","Height","768",!repair);
        SetIni("zeal.ini","Zeal","FPS","60",true);
        if(repair) SetIni("eqclient.ini","EqwGeneral","DebugLogLevel","2",false);
        if(renderer!="keep") {
            if(renderer!="d3d11" && renderer!="d3d12" && renderer!="d3d12-quality") throw new IOException("Choose a supported graphics mode.");
            string target=PathFor("dgVoodoo.conf"), source=PathFor("LaunchSupport/profiles/"+renderer+".conf");
            if(!File.Exists(target) || Digest(target)!=Digest(source)) {
                Backup(target); File.Copy(source,target,true);
            }
        }
    }
    internal static bool HasRuntime() {
        // The launcher is x86. This therefore checks the required 32-bit runtime directory.
        return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msvcp140.dll")) &&
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"vcruntime140.dll"));
    }
    internal static void Runtime() {
        Verify(); Idle();
        Process.Start(new ProcessStartInfo(PathFor("LaunchSupport/prerequisites/vc_redist.x86.exe"),"/install") {UseShellExecute=true});
    }
    internal static void SelectFps(int fps) {
        if(!new[]{0,60,120,144,240,300}.Contains(fps)) throw new IOException("Choose a supported frame limit.");
        Verify(); Idle(); Backup(PathFor("zeal.ini")); SetIni("zeal.ini","Zeal","FPS",fps.ToString(),false);
    }
    internal static void Launch(string renderer,int? fps=null,int? width=null,int? height=null,string displayDevice=null) {
        ValidateDisplay(width,height,displayDevice);
        Prepare(renderer,false);
        if(fps.HasValue) SelectFps(fps.Value);
        ApplyResolution(width,height);
        if(!HasRuntime()) throw new IOException("Install the Microsoft runtime with the button below, then press Play again.");
        var start=new StartupInfo(); start.cb=Marshal.SizeOf(start); start.flags=1; start.show=1;
        ProcessInfo info;
        // Explicit application path; short command line avoids the legacy client's path/argument parser.
        if(!CreateProcess(PathFor("eqgame.exe"),new StringBuilder("eqgame.exe patchme"),IntPtr.Zero,IntPtr.Zero,false,0,
            IntPtr.Zero,Root,ref start,out info)) throw new IOException("The game could not start. Windows error "+Marshal.GetLastWin32Error()+".");
        CloseHandle(info.thread); CloseHandle(info.process);
    }
    internal static void Log(string message) {
        try { Directory.CreateDirectory(PathFor("LaunchSupport/logs")); File.AppendAllText(PathFor("LaunchSupport/logs/launcher.log"),
            DateTime.Now.ToString("s")+" "+message+Environment.NewLine,TextEncoding); } catch { }
    }
}
