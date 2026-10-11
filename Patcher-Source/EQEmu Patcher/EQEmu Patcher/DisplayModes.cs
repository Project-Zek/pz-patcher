using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ProjectZekDisplay {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    internal struct DevMode {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
        public ushort SpecVersion,DriverVersion,Size,DriverExtra;
        public uint Fields;
        public int PositionX,PositionY;
        public uint Orientation,FixedOutput;
        public short Color,Duplex,YResolution,TTOption,Collate;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPixel,Width,Height,DisplayFlags,Frequency,ICMMethod,ICMIntent,MediaType,DitherType,
                    Reserved1,Reserved2,PanningWidth,PanningHeight;
        public static DevMode Empty() {return new DevMode {Size=(ushort)Marshal.SizeOf(typeof(DevMode)),DeviceName="",FormName=""};}
    }
    internal interface IDisplayApi {
        DevMode Current(string device);
        IEnumerable<DevMode> Modes(string device);
    }
    internal sealed class WindowsDisplayApi:IDisplayApi {
        [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="EnumDisplaySettingsW")]
        static extern bool EnumDisplaySettings(string device,int index,ref DevMode mode);
        public DevMode Current(string device) {
            var mode=DevMode.Empty();
            if(!EnumDisplaySettings(device,-1,ref mode)) throw new IOException("Windows could not read this monitor's display settings.");
            return mode;
        }
        public IEnumerable<DevMode> Modes(string device) {
            for(int i=0;i<10000;i++) {
                var mode=DevMode.Empty();
                if(!EnumDisplaySettings(device,i,ref mode))yield break;
                yield return mode;
            }
        }

    }
    internal sealed class ResolutionChoice {
        public readonly string Value,Label;
        public readonly int? Width,Height;
        public ResolutionChoice(string value,string label,int? width=null,int? height=null) {Value=value;Label=label;Width=width;Height=height;}
        public override string ToString() {return Label;}
    }
    internal static class DisplayPolicy {
        public static bool UsableResolution(DevMode mode) {return mode.BitsPerPixel==32 && mode.Width>=640 && mode.Height>=480 && mode.Width<=8192 && mode.Height<=8192 && (mode.DisplayFlags&2)==0;}
        public static ResolutionChoice[] Resolutions(DevMode current,IEnumerable<DevMode> modes,int? savedWidth,int? savedHeight) {
            string suffix=savedWidth.HasValue&&savedHeight.HasValue?" ("+savedWidth+" × "+savedHeight+")":"";
            var result=new List<ResolutionChoice>{new ResolutionChoice("keep","Keep current"+suffix)};
            if(UsableResolution(current)) result.Add(new ResolutionChoice("desktop","Desktop ("+current.Width+" × "+current.Height+")",(int)current.Width,(int)current.Height));
            result.AddRange(modes.Where(UsableResolution).GroupBy(m=>new{m.Width,m.Height}).Select(g=>g.First())
                .OrderBy(m=>m.Width).ThenBy(m=>m.Height).Select(m=>new ResolutionChoice(m.Width+"x"+m.Height,m.Width+" × "+m.Height,(int)m.Width,(int)m.Height)));
            return result.ToArray();
        }
    }
}
