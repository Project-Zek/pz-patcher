using System;
using System.Linq;
using System.Runtime.InteropServices;
using ProjectZekDisplay;
class DisplayPolicyTests {
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);++checks;}
    static DevMode Mode(uint w,uint h,uint hz,uint bits=32,uint flags=0){var m=DevMode.Empty();m.Width=w;m.Height=h;m.Frequency=hz;m.BitsPerPixel=bits;m.DisplayFlags=flags;return m;}
    static int Main(){try{
        Check(Marshal.SizeOf(typeof(DevMode))==220,"Native display structure size");
        var desktop=Mode(1920,1080,144);
        var choices=DisplayPolicy.Resolutions(desktop,new[]{desktop,Mode(1920,1080,60),Mode(1280,720,120),Mode(1280,720,60),Mode(640,480,60),Mode(320,200,60),Mode(9999,9999,60),Mode(1600,900,60,16),Mode(1600,900,60,32,2)},1600,1200);
        Check(choices[0].Value=="keep"&&choices[0].Label.Contains("1600"),"Existing client resolution retained");
        Check(choices[1].Value=="desktop"&&choices[1].Width==1920&&choices[1].Height==1080,"Desktop choice uses current geometry");
        Check(choices.Count(x=>x.Value=="1920x1080")==1&&choices.Count(x=>x.Value=="1280x720")==1,"Same resolution at multiple rates deduplicated");
        Check(choices.Any(x=>x.Value=="640x480"),"Supported minimum resolution available");
        Check(!choices.Any(x=>x.Value=="320x200"||x.Value=="9999x9999"||x.Value=="1600x900"),"Unsupported low/large/16bit/interlaced modes filtered");
        Check(!DisplayPolicy.Resolutions(desktop,new DevMode[0],null,null)[0].Label.Contains("("),"Unset existing resolution handled");
        Check(desktop.Frequency==144&&desktop.Width==1920,"Enumeration leaves supplied display state unchanged");
        Console.WriteLine("PASS: "+checks+" resolution policy checks; enumeration only, no display changes.");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
