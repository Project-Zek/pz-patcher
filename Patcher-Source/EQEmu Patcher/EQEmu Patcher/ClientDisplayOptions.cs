using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ProjectZekDisplay;

internal static partial class Client {
    internal static void ReadResolution(out int? width,out int? height) {
        int w,h;
        width=Int32.TryParse(ReadIni(PathFor("eqclient.ini"),"VideoMode","Width"),out w)&&w>=640&&w<=8192?(int?)w:null;
        height=Int32.TryParse(ReadIni(PathFor("eqclient.ini"),"VideoMode","Height"),out h)&&h>=480&&h<=8192?(int?)h:null;
    }
    static void ValidateDisplay(int? width,int? height,string device) {
        if(width.HasValue!=height.HasValue)throw new IOException("Choose a complete resolution.");
        if(!width.HasValue)return;
        if(device!=Screen.PrimaryScreen.DeviceName)throw new IOException("The primary monitor changed. Reopen the patcher to reload its display modes.");
        var api=new WindowsDisplayApi();var current=api.Current(device);var modes=api.Modes(device).ToArray();
        if(!modes.Concat(new[]{current}).Any(m=>DisplayPolicy.UsableResolution(m)&&m.Width==width.Value&&m.Height==height.Value))
            throw new IOException("This resolution is no longer supported by the primary monitor. Reopen the patcher and choose another resolution.");
    }
    internal static void ApplyResolution(int? width,int? height) {
        if(!width.HasValue)return;
        Backup(PathFor("eqclient.ini"));
        SetIni("eqclient.ini","Defaults","VideoMode","-1",false);
        SetIni("eqclient.ini","Defaults","VideoModeBitsPerPixel","32",false);
        SetIni("eqclient.ini","VideoMode","BitsPerPixel","32",false);
        SetIni("eqclient.ini","VideoMode","Width",width.Value.ToString(),false);
        SetIni("eqclient.ini","VideoMode","Height",height.Value.ToString(),false);
        // EQW supplies windowed presentation; use its automatic refresh value.
        SetIni("eqclient.ini","VideoMode","RefreshRate","0",false);
    }
}
