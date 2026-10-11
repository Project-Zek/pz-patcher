using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ProjectZekDisplay;

namespace EQEmu_Patcher {
    public partial class MainForm {
        readonly ComboBox graphicsChoice=new ComboBox(), frameChoice=new ComboBox();
        readonly ComboBox resolutionChoice=new ComboBox();
        readonly Label displayHint=new Label();
        bool loadingLaunchOptions;
        string displayDevice;
        readonly Button displayRepair=new Button(), runtimeInstall=new Button();
        bool launchWhenReady;
        readonly Button classicFixes=new Button();
        readonly string[] rendererModes={"d3d11","d3d12","d3d12-quality","keep"};
        readonly string[] frameModes={"keep","60","120","144","240","300","0"};
        void AddLaunchOptions() {
            ClientSize=new Size(640,680); MinimumSize=new Size(656,565);
            btnStart.SetBounds(525,566,95,52);btnCheck.SetBounds(12,566,95,52);
            progressBar.SetBounds(10,640,620,30);
            chkAutoPatch.Location=new Point(140,566);chkAutoPlay.Location=new Point(300,566);
            chkAutoPatch.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;chkAutoPlay.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;
            txtList.MinimumSize=Size.Empty;txtList.SetBounds(10,6,620,414);
            splashLogo.MinimumSize=Size.Empty;splashLogo.SetBounds(10,6,620,414);
            splashLogo.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
            splashLogo.SizeMode=PictureBoxSizeMode.Zoom;
            var graphicsLabel=new Label {Text="Graphics",Left=12,Top=434,Width=83,Height=23,Anchor=AnchorStyles.Left|AnchorStyles.Bottom};
            graphicsChoice.SetBounds(95,429,235,26);graphicsChoice.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
            graphicsChoice.DropDownStyle=ComboBoxStyle.DropDownList;
            graphicsChoice.Items.AddRange(new object[]{"Standard (recommended)","DirectX 12","DirectX 12 Quality","Keep my graphics settings"});graphicsChoice.SelectedIndex=0;
            var frameLabel=new Label {Text="Frame limit",Left=345,Top=434,Width=80,Height=23,Anchor=AnchorStyles.Right|AnchorStyles.Bottom};
            frameChoice.SetBounds(425,429,195,26);frameChoice.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;
            frameChoice.DropDownStyle=ComboBoxStyle.DropDownList;
            frameChoice.Items.AddRange(new object[]{"Keep current limit","60 FPS","120 FPS","144 FPS","240 FPS","300 FPS","Unlimited"});frameChoice.SelectedIndex=0;
            var resolutionLabel=new Label {Text="Resolution",Left=12,Top=470,Width=83,Height=23,Anchor=AnchorStyles.Left|AnchorStyles.Bottom};
            resolutionChoice.SetBounds(95,465,525,26);resolutionChoice.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;
            resolutionChoice.DropDownStyle=ComboBoxStyle.DropDownList;
            resolutionChoice.Name="resolutionChoice";resolutionChoice.AccessibleName="Resolution";
            displayHint.SetBounds(12,499,608,23);displayHint.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;
            displayHint.Text="Resolution changes apply when you press Play.";
            displayRepair.Text="Reset display";displayRepair.SetBounds(12,526,143,30);
            classicFixes.Text="Classic fixes";classicFixes.SetBounds(165,526,142,30);classicFixes.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
            classicFixes.Click+=(s,e)=>{if(isLoading||isPatching)return;using(var dialog=new ProjectZekClassic.ClassicFixForm(Client.Root))dialog.ShowDialog(this);};displayRepair.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
            runtimeInstall.Text="Install Microsoft runtime";runtimeInstall.SetBounds(327,526,293,30);runtimeInstall.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;
            displayRepair.Click+=(s,e)=>LaunchUtility(()=>{Client.Prepare("d3d11",true);graphicsChoice.SelectedIndex=0;resolutionChoice.SelectedIndex=0;SaveLaunchOptions();});
            runtimeInstall.Click+=(s,e)=>LaunchUtility(Client.Runtime);
            graphicsChoice.SelectedIndexChanged+=(s,e)=>SaveLaunchOptions();frameChoice.SelectedIndexChanged+=(s,e)=>SaveLaunchOptions();
            resolutionChoice.SelectedIndexChanged+=(s,e)=>SaveLaunchOptions();
            Controls.AddRange(new Control[]{graphicsLabel,graphicsChoice,frameLabel,frameChoice,resolutionLabel,resolutionChoice,displayHint,displayRepair,runtimeInstall,classicFixes});
            var notices=new ContextMenuStrip();
            notices.Items.Add("License notices",null,(s,e)=>ShowLicenseNotices());
            ContextMenuStrip=notices;splashLogo.ContextMenuStrip=notices;
            PopulateDisplayChoices();
            int fitHeight=Math.Max(526,Math.Min(680,Screen.PrimaryScreen.WorkingArea.Height-50));
            if(fitHeight<ClientSize.Height)ClientSize=new Size(640,fitHeight);
            FormClosing+=(s,e)=>{if(isPatching){cts.Cancel();e.Cancel=true;StatusLibrary.Log("Cancelling the update safely. Wait for it to finish before closing.");}};
        }
        internal static void ShowLicenseNotices() {
            using(var window=new Form {Text="Project Zek - License notices",Width=760,Height=580,StartPosition=FormStartPosition.CenterScreen}) {
                var text=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,WordWrap=false,
                    Text=System.Text.Encoding.UTF8.GetString(Client.EmbeddedBytes("ProjectZek.LicenseNotices"))};
                window.Controls.Add(text);window.ShowDialog();
            }
        }
        internal static string ResolveManifestUrl(string configured,string built) {
            string value=String.IsNullOrWhiteSpace(configured)?(built??"").Trim():configured.Trim();
            if(value.Length==0)return "";
            Uri uri;
            if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.UserInfo.Length!=0||
                !(uri.AbsolutePath.EndsWith(".yml",StringComparison.OrdinalIgnoreCase)||uri.AbsolutePath.EndsWith(".yaml",StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Set ManifestUrl to a direct HTTPS .yml or .yaml address in Project-Zek-Patcher.exe.config.");
            return value;
        }
        void LoadLaunchOptions() {
            loadingLaunchOptions=true;
            try {
                int graphics=Array.IndexOf(rendererModes,IniLibrary.instance.GraphicsMode??"d3d11");
                int frames=Array.IndexOf(frameModes,IniLibrary.instance.FrameLimit??"keep");
                graphicsChoice.SelectedIndex=Math.Max(0,graphics);frameChoice.SelectedIndex=Math.Max(0,frames);
                PopulateDisplayChoices();
                string saved=IniLibrary.instance.DisplayResolution??"keep";
                int resolution=resolutionChoice.Items.Cast<ResolutionChoice>().ToList().FindIndex(x=>x.Value==saved);
                resolutionChoice.SelectedIndex=Math.Max(0,resolution);
            } finally {loadingLaunchOptions=false;}
        }
        void PopulateDisplayChoices() {
            bool previous=loadingLaunchOptions;loadingLaunchOptions=true;
            try {
                resolutionChoice.Items.Clear();
                displayDevice=Screen.PrimaryScreen.DeviceName;
                int? width,height;Client.ReadResolution(out width,out height);
                var api=new WindowsDisplayApi();var current=api.Current(displayDevice);var modes=api.Modes(displayDevice).ToArray();
                resolutionChoice.Items.AddRange(DisplayPolicy.Resolutions(current,modes,width,height));
            } catch(Exception) {
                resolutionChoice.Items.Clear();
                resolutionChoice.Items.Add(new ResolutionChoice("keep","Keep current resolution"));
                displayHint.Text="Monitor modes unavailable. Keep current settings and reopen to retry.";
            } finally {resolutionChoice.SelectedIndex=0;loadingLaunchOptions=previous;}
        }
        void SaveLaunchOptions() {
            if(isLoading||loadingLaunchOptions||IniLibrary.instance==null||graphicsChoice.SelectedIndex<0||frameChoice.SelectedIndex<0||resolutionChoice.SelectedIndex<0)return;
            IniLibrary.instance.GraphicsMode=rendererModes[graphicsChoice.SelectedIndex];
            IniLibrary.instance.FrameLimit=frameModes[frameChoice.SelectedIndex];
            IniLibrary.instance.DisplayResolution=((ResolutionChoice)resolutionChoice.SelectedItem).Value;
            IniLibrary.Save();
        }
        void LaunchUtility(Action action) {
            if(isLoading||isPatching)return;
            try { action(); } catch(Exception error) {MessageBox.Show(this,error.Message,"Project Zek");}
        }
        FileList ReadManifest() {
            string filename=OfflineReview?"filelist-review.yml":"filelist.yml";
            using(var reader=File.OpenText(Path.Combine(Client.Root,filename))) {
                var result=new DeserializerBuilder().WithNamingConvention(new CamelCaseNamingConvention()).Build().Deserialize<FileList>(reader);
                if(result==null||result.downloads==null||result.downloads.Count==0)throw new IOException("The patch manifest is empty.");
                var names=result.downloads.Select(x=>(x.name??"").Replace('\\','/')).ToArray();
                if(names.Any(x=>x.StartsWith("Zeal/",StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("This manifest uses the older Zeal/ layout. The server manifest must be updated for this client release before patching or playing.");
                if(names.Any(x=>new[]{"gequip5.s3d","global6_chr.s3d","bmpwad8.s3d","eqfeart7.pak","spellsnew.edd","spellsnew.eff"}.Contains(x,StringComparer.OrdinalIgnoreCase)))
                    throw new IOException("This patcher creates classic models, screens and effects locally. Remove those client asset files from the published download manifest.");
                foreach(string required in new[]{"Zeal.asi","eqgame.dll"})
                    if(!names.Contains(required,StringComparer.OrdinalIgnoreCase))
                        throw new IOException("The manifest does not contain the complete current client release: "+required);
                return result;
            }
        }
        void VerifyManifestBeforePlay() {
            if(ProjectZekClassic.ClassicFix.HasIncomplete(Client.Root))
                throw new IOException("A classic fix stopped during installation. Use Classic fixes > Restore before playing.");
            var transaction=new PatchTransaction(Client.Root);
            if(File.Exists(transaction.Marker))throw new IOException("An update stopped during installation. Restore its transaction backup before playing.");
            var manifest=ReadManifest();
            foreach(var entry in manifest.downloads) {
                string path=transaction.Resolve(entry.name);
                // Resolve every path before skipping optional audio/definitions.
                // Patch still validates their current downloaded MD5; Play must
                // also work when an external music pack is updated or absent.
                if(ProjectZekClassic.MusicDefinitionAssets.IsOptionalMusicAsset(entry.name))continue;
                if(!File.Exists(path)||new FileInfo(path).Length!=entry.size||!String.Equals(PatchTransaction.Md5(path),entry.md5,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Press Patch to repair this missing or mismatched file: "+entry.name);
            }
            foreach(var entry in manifest.deletes??new System.Collections.Generic.List<FileEntry>()) {
                string path=transaction.Resolve(entry.name);
                if(ProjectZekClassic.MusicDefinitionAssets.IsOptionalMusicAsset(entry.name))continue;
                if(File.Exists(path))throw new IOException("Press Patch to finish removing an obsolete file: "+entry.name);
            }
        }
    }
}
