using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.WindowsAPICodePack.Taskbar;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace EQEmu_Patcher
{

    public partial class MainForm : Form
    {
        internal static bool OfflineReview;

        public static string serverName; // server title name
        public static string filelistUrl; //filelist url
        public static string patcherUrl; //patcher url e.g. eqemupatcher-hash.txt
        public static string version; //version of file
        string fileName; //base name of executable
        bool isPatching = false;

        bool isPendingPatch = false; // This is used to indicate that someone pressed "Patch" before we did some background update checks
        string myHash; //my MD5 generated hash

        bool isLoading;
        bool isAutoPatch = false;
        bool isAutoPlay = false;
        bool isDirectYaml = false;
        CancellationTokenSource cts;


        //Note that for supported versions, the 3 letter suffix is needed on the filelist_###.yml file.
        public static List<VersionTypes> supportedClients = new List<VersionTypes> { //Supported clients for patcher
            //VersionTypes.Unknown, //unk
            //VersionTypes.Titanium, //tit
            //VersionTypes.Underfoot, //und
            //VersionTypes.Secrets_Of_Feydwer, //sof
            //VersionTypes.Seeds_Of_Destruction, //sod
            VersionTypes.Rain_Of_Fear, //rof
            VersionTypes.Rain_Of_Fear_2 //rof
            //VersionTypes.Broken_Mirror, //bro
        };

        private Dictionary<VersionTypes, ClientVersion> clientVersions = new Dictionary<VersionTypes, ClientVersion>();

        VersionTypes currentVersion;

       // TaskbarItemInfo tii = new TaskbarItemInfo();
        public MainForm()
        {
            InitializeComponent();
            AddLaunchOptions();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try { await InitializeAsync(); }
            catch(Exception error) { isLoading=false;MessageBox.Show(this,"The patcher could not finish loading: "+error.Message, "Project Zek");Close(); }
        }

        private async Task InitializeAsync()
        {
            isLoading = true;
            try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            version = Assembly.GetEntryAssembly().GetName().Version.ToString();
            Console.WriteLine($"Initializing {version}");
            Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
            // Intentionally avoid logging here to keep splash visible during initial load
            cts = new CancellationTokenSource();

            serverName = Assembly.GetExecutingAssembly().GetCustomAttribute<ServerName>().Value;
            if (serverName == "") {
                MessageBox.Show("This patcher was built incorrectly. Please contact the distributor of this and inform them the server name is not provided or screenshot this message.");
                this.Close();
                return;
            }

            fileName = Assembly.GetExecutingAssembly().GetCustomAttribute<FileName>().Value;
            if (fileName == "")
            {
                MessageBox.Show("This patcher was built incorrectly. Please contact the distributor of this and inform them the file name is not provided or screenshot this message.");
                this.Close();
                return;
            }

            filelistUrl = Assembly.GetExecutingAssembly().GetCustomAttribute<FileListUrl>().Value;
            if (filelistUrl == "") {
                MessageBox.Show("This patcher was built incorrectly. Please contact the distributor of this and inform them the file list url is not provided or screenshot this message.", serverName);
                this.Close();
                return;
            }
            // If a direct YAML URL is provided, don't append suffix; otherwise, ensure trailing slash for suffix-mode
            isDirectYaml = filelistUrl.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || filelistUrl.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);
            if (!isDirectYaml && !filelistUrl.EndsWith("/")) filelistUrl += "/";

            patcherUrl = Assembly.GetExecutingAssembly().GetCustomAttribute<PatcherUrl>().Value;
            if (patcherUrl == "")
            {
                MessageBox.Show("This patcher was built incorrectly. Please contact the distributor of this and inform them the patcher url is not provided or screenshot this message.", serverName);
                this.Close();
                return;
            }
            if (!patcherUrl.EndsWith("/")) patcherUrl += "/";

            txtList.Visible = false;
            splashLogo.Visible = true;
            if (this.Width < 432) {
                this.Width = 432;
            }
            if (this.Height < 550)
            {
                this.Height = 550;
            }
            buildClientVersions();
            IniLibrary.Load();
            LoadLaunchOptions();
            detectClientVersion();
            isAutoPlay = (IniLibrary.instance.AutoPlay.ToLower() == "true");
            isAutoPatch = (IniLibrary.instance.AutoPatch.ToLower() == "true");
            chkAutoPlay.Checked = isAutoPlay;
            chkAutoPatch.Checked = isAutoPatch;
            try
            {
                if (File.Exists(Application.ExecutablePath + ".old"))
                {
                    File.Delete(Application.ExecutablePath + ".old");
                }

            } catch (Exception exDelete)
            {
                Console.WriteLine($"Failed to delete .old file: {exDelete.Message}");
            }

            if (IniLibrary.instance.ClientVersion == VersionTypes.Unknown)
            {
                detectClientVersion();
                IniLibrary.instance.ClientVersion = currentVersion;
                IniLibrary.Save();
            }
            string suffix = "unk";
            if (currentVersion == VersionTypes.Titanium) suffix = "tit";
            if (currentVersion == VersionTypes.Underfoot) suffix = "und";
            if (currentVersion == VersionTypes.Seeds_Of_Destruction) suffix = "sod";
            if (currentVersion == VersionTypes.Broken_Mirror) suffix = "bro";
            if (currentVersion == VersionTypes.Secrets_Of_Feydwer) suffix = "sof";
            if (currentVersion == VersionTypes.Rain_Of_Fear || currentVersion == VersionTypes.Rain_Of_Fear_2) suffix = "rof";

            // Proceed regardless of client detection; direct YAML mode does not need version gating

            this.Text = "Project-Zek-Patcher";
            progressBar.Minimum = 0;
            progressBar.Maximum = 10000;
            progressBar.Value = 0;
            StatusLibrary.SubscribeProgress(new StatusLibrary.ProgressHandler((int value) => {
                Invoke((MethodInvoker)delegate {
                    progressBar.Value = value;
                    if (Environment.OSVersion.Version.Major < 6) {
                        return;
                    }
                    var taskbar = TaskbarManager.Instance;
                    taskbar.SetProgressValue(value, 10000);
                    taskbar.SetProgressState((value == 10000) ? TaskbarProgressBarState.NoProgress : TaskbarProgressBarState.Normal);
                });
            }));

            StatusLibrary.SubscribeLogAdd(new StatusLibrary.LogAddHandler((string message) => {
                Invoke((MethodInvoker)delegate {
                    // Keep splash visible during initial load; switch to log view after load completes
                    if (!txtList.Visible && !isLoading)
                    {
                        txtList.Visible = true;
                        splashLogo.Visible = false;
                    }
                    txtList.AppendText(message + "\r\n");
                });
            }));

            StatusLibrary.SubscribePatchState(new StatusLibrary.PatchStateHandler((bool isPatchGoing) => {
                Invoke((MethodInvoker)delegate {

                    btnCheck.BackColor = SystemColors.Control;
                    if (isPatchGoing)
                    {
                        btnCheck.Text = "Cancel";
                        return;
                    }

                    btnCheck.Text = "Patch";
                });
            }));

            if (OfflineReview) {
                ReadManifest();isAutoPlay=false;isAutoPatch=false;
                btnCheck.Enabled=false;chkAutoPatch.Enabled=false;chkAutoPlay.Enabled=false;
                StatusLibrary.Log("Downloads are disabled for this local build. Play verifies the prepared local manifest.");
                isLoading=false;cts.Cancel();return;
            }
            string webUrl;
            string response;
            if (isDirectYaml)
            {
                webUrl = filelistUrl;
                // Avoid early logs that would hide splash during initial load
                response = await DownloadFile(cts, webUrl, "filelist.yml");
                if (response != "")
                {
                    StatusLibrary.Log($"Failed to fetch filelist from {webUrl}: {response}");
                    MessageBox.Show("Failed to fetch filelist from " + webUrl + ": " + response);
                    this.Close();
                    return;
                }
            }
            else
            {
                webUrl = $"{filelistUrl}{suffix}/filelist_{suffix}.yml";
                // Avoid early logs that would hide splash during initial load
                response = await DownloadFile(cts, webUrl, "filelist.yml");
                if (response != "")
                {
                    webUrl = $"{filelistUrl}/filelist_{suffix}.yml";
                    // Avoid early logs that would hide splash during initial load
                    response = await DownloadFile(cts, webUrl, "filelist.yml");
                    if (response != "")
                    {
                        StatusLibrary.Log($"Failed to fetch filelist from {webUrl}: {response}");
                        MessageBox.Show("Failed to fetch filelist from " + webUrl + ": " + response);
                        this.Close();
                        return;
                    }
                }
            }

            string url = $"{patcherUrl}{fileName}-hash.txt";
            try
            {
                var data = await Download(cts, url);
                response = System.Text.Encoding.Default.GetString(data).ToUpper();
            } catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch patch from {url}: {ex.Message}");
            }

            if (response != "")
            {
                myHash = UtilityLibrary.GetMD5(Application.ExecutablePath);
                if (response.Trim() != myHash)
                {
                    StatusLibrary.Log("A published patcher update is available. This review candidate does not replace itself automatically.");
                    //StatusLibrary.Log($"{myHash} vs {response} selfpatch");
                    if (!isPendingPatch)
                    {
                        btnCheck.BackColor = Color.Red;
                    }
                }
            }

            FileList filelist=ReadManifest();

            if (filelist.version != IniLibrary.instance.LastPatchedVersion)
            {
                if (!isPendingPatch)
                {
                    btnCheck.BackColor = Color.Red;
                }
            } else
            {
                if (isAutoPlay) launchWhenReady = true;
            }
            isLoading = false;
            if (launchWhenReady) PlayGame();
            // Always use the bundled project logo as splash image
            splashLogo.Image = Properties.Resources.project_zek_logo;
            cts.Cancel();
        }

        private void detectClientVersion()
        {
            try
            {

                var hash = UtilityLibrary.GetEverquestExecutableHash(AppDomain.CurrentDomain.BaseDirectory);
                if (hash == "")
                {
                    // Could not find eqgame.exe; proceed without closing, assume unknown client
                    currentVersion = VersionTypes.Unknown;
                    return;
                }
                switch (hash)
                {
                    case "85218FC053D8B367F2B704BAC5E30ACC":
                        currentVersion = VersionTypes.Secrets_Of_Feydwer;
                        splashLogo.Image = Properties.Resources.project_zek_logo;
                        break;
                    case "859E89987AA636D36B1007F11C2CD6E0":
                    case "EF07EE6649C9A2BA2EFFC3F346388E1E78B44B48": //one of the torrented uf clients, used by B&R too
                        currentVersion = VersionTypes.Underfoot;
                        splashLogo.Image = Properties.Resources.project_zek_logo;
                        break;
                    case "A9DE1B8CC5C451B32084656FCACF1103": //p99 client
                    case "BB42BC3870F59B6424A56FED3289C6D4": //vanilla titanium
                        currentVersion = VersionTypes.Titanium;
                        splashLogo.Image = Properties.Resources.project_zek_logo;
                        break;
                    case "368BB9F425C8A55030A63E606D184445":
                        currentVersion = VersionTypes.Rain_Of_Fear;
                        splashLogo.Image = Properties.Resources.project_zek_logo;
                        break;
                    case "240C80800112ADA825C146D7349CE85B":
                    case "A057A23F030BAA1C4910323B131407105ACAD14D": //This is a custom ROF2 from a torrent download
                    case "389709EC0E456C3DAE881A61218AAB3F": // This is a 4gb patched eqgame
                    case "6574AC667D4C522D21A47F4D00920CC2": // Unknown origin, issue #29
                    case "AE4E4C995DF8842DAE3127E88E724033": // gangsta of RoT 4gb patched eqgame
                    case "3B44C6CD42313CB80C323647BCB296EF": //https://github.com/xackery/eqemupatcher/issues/15
                    case "513FDC2B5CC63898D7962F0985D5C207": //aslr checksum removed
                    case "2FD5E6243BCC909D9FD0587A156A1165": //https://github.com/xackery/eqemupatcher/issues/20
                    case "26DC13388395A20B73E1B5A08415B0F8": //Legacy of Norrath Custom RoF2 Client https://github.com/xackery/eqemupatcher/issues/16
                        currentVersion = VersionTypes.Rain_Of_Fear_2;
                        splashLogo.Image = Properties.Resources.project_zek_logo;
                        break;
                    case "6BFAE252C1A64FE8A3E176CAEE7AAE60": //This is one of the live EQ binaries.
                    case "AD970AD6DB97E5BB21141C205CAD6E68": //2016/08/27
                        currentVersion = VersionTypes.Broken_Mirror;
                        splashLogo.Image = Properties.Resources.project_zek_logo;
                        break;
                    default:
                        currentVersion = VersionTypes.Unknown;
                        break;
                }

                // Allow project-specific overrides via assembly attributes if unknown
                if (currentVersion == VersionTypes.Unknown && !string.IsNullOrEmpty(hash))
                {
                    try
                    {
                        var asm = Assembly.GetExecutingAssembly();
                        var knownHashesAttr = asm.GetCustomAttribute<KnownClientHashes>();
                        var knownVersionAttr = asm.GetCustomAttribute<KnownClientVersion>();
                        var knownHashes = knownHashesAttr?.Value ?? string.Empty;
                        if (!string.IsNullOrEmpty(knownHashes))
                        {
                            var parts = knownHashes.Replace("\r", "").Split(new[] { ';', '\n', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var p in parts)
                            {
                                if (string.Equals(p.Trim(), hash, StringComparison.OrdinalIgnoreCase))
                                {
                                    var verStr = knownVersionAttr?.Value ?? "Rain_Of_Fear_2";
                                    VersionTypes parsed;
                                    if (!Enum.TryParse(verStr, true, out parsed))
                                    {
                                        parsed = VersionTypes.Rain_Of_Fear_2;
                                    }
                                    currentVersion = parsed;
                                    // Prefer custom logo if provided
                                    splashLogo.Image = Properties.Resources.project_zek_logo;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }
                // Do not block on unknown client; proceed without prompts
                else
                {
                    // Prefer custom logo if a display name is configured
                    var displayAttr = Assembly.GetExecutingAssembly().GetCustomAttribute<ClientDisplayName>();
                    if (displayAttr != null)
                    {
                        splashLogo.Image = Properties.Resources.project_zek_logo;
                    }
                    //StatusLibrary.Log($"You seem to have put me in a {clientVersions[currentVersion].FullName} client directory");
                }

                //MessageBox.Show(""+currentVersion);
                //StatusLibrary.Log($"If you wish to help out, press the scan button on the bottom left and wait for it to complete, then copy paste this data as an Issue on github!");
            }
            catch (UnauthorizedAccessException err)
            {
                MessageBox.Show("You need to run this program with Administrative Privileges" + err.Message);
                return;
            }
        }

        //Build out all client version's dictionary
        private void buildClientVersions()
        {
            clientVersions.Clear();
            clientVersions.Add(VersionTypes.Titanium, new ClientVersion("Titanium", "titanium"));
            clientVersions.Add(VersionTypes.Secrets_Of_Feydwer, new ClientVersion("Secrets Of Feydwer", "sof"));
            clientVersions.Add(VersionTypes.Seeds_Of_Destruction, new ClientVersion("Seeds of Destruction", "sod"));
            clientVersions.Add(VersionTypes.Rain_Of_Fear, new ClientVersion("Rain of Fear", "rof"));
            clientVersions.Add(VersionTypes.Rain_Of_Fear_2, new ClientVersion("Rain of Fear 2", "rof2"));
            clientVersions.Add(VersionTypes.Underfoot, new ClientVersion("Underfoot", "underfoot"));
            clientVersions.Add(VersionTypes.Broken_Mirror, new ClientVersion("Broken Mirror", "brokenmirror"));
        }


        private void btnStart_Click(object sender, EventArgs e)
        {
            PlayGame();
        }

        private void PlayGame()
        {
            if (isLoading || isPatching) return;
            try {
                VerifyManifestBeforePlay();
                string frames=frameModes[frameChoice.SelectedIndex];
                SaveLaunchOptions();
                var resolution=(ProjectZekDisplay.ResolutionChoice)resolutionChoice.SelectedItem;
                int? width=resolution.Width,height=resolution.Height;
                if(resolution.Value=="desktop") {
                    var desktop=new ProjectZekDisplay.WindowsDisplayApi().Current(displayDevice);
                    width=(int)desktop.Width;height=(int)desktop.Height;
                }
                Client.Launch(rendererModes[graphicsChoice.SelectedIndex],frames=="keep"?(int?)null:Int32.Parse(frames),
                    width,height,displayDevice);
                this.Close();
            } catch (Exception error) { MessageBox.Show(this,error.Message,"Project Zek"); }
        }

        private void btnCheck_Click(object sender, EventArgs e)
        {
            if (isLoading && !isPendingPatch)
            {
                isPendingPatch = true;
                pendingPatchTimer.Enabled = true;
                StatusLibrary.Log("Checking for updates...");
                btnCheck.Text = "Cancel";
                return;
            }

            if (isPatching)
            {
                
                cts.Cancel();
            }
            Console.WriteLine("patch button called");
            StartPatch();
        }

        public static async Task<string> DownloadFile(CancellationTokenSource cts, string url, string path)
        {
            return await UtilityLibrary.DownloadFile(cts,url,path);
        }

        public static async Task<byte[]> Download(CancellationTokenSource cts, string url)
        {
            return await UtilityLibrary.Download(cts, url);
        }

        private async void StartPatch()
        {
            if (OfflineReview || isPatching || isLoading) return;
            try { Client.Idle(); }
            catch(Exception error) { MessageBox.Show(this,error.Message,"Project Zek"); return; }
            cts=new CancellationTokenSource(); isPatching=true;
            btnStart.Enabled=graphicsChoice.Enabled=frameChoice.Enabled=displayRepair.Enabled=runtimeInstall.Enabled=classicFixes.Enabled=false;
            StatusLibrary.SetPatchState(true); bool completed=false;
            try {
                var manifest=ReadManifest();
                await new PatchTransaction(Client.Root).ApplyAsync(
                    manifest.downloads.Select(x=>new PatchFile(x.name,x.md5,x.size)),
                    (manifest.deletes??new List<FileEntry>()).Select(x=>x.name),new Uri(manifest.downloadprefix),
                    cts.Token,StatusLibrary.SetProgress,StatusLibrary.Log);
                IniLibrary.instance.LastPatchedVersion=manifest.version;IniLibrary.Save();completed=true;
            } catch(OperationCanceledException) {StatusLibrary.Log("Update cancelled. Play will verify all required files again.");}
            catch(Exception error) {StatusLibrary.Log("Update failed: "+error.Message);}
            finally {
                isPatching=false;StatusLibrary.SetPatchState(false);
                btnStart.Enabled=graphicsChoice.Enabled=frameChoice.Enabled=displayRepair.Enabled=runtimeInstall.Enabled=classicFixes.Enabled=true;
                cts.Dispose();
            }
            if(completed&&isAutoPlay)PlayGame();
        }

        private void chkAutoPlay_CheckedChanged(object sender, EventArgs e)
        {
            if (isLoading) return;
            isAutoPlay = chkAutoPlay.Checked;
            IniLibrary.instance.AutoPlay = (isAutoPlay) ? "true" : "false";
            if (isAutoPlay) StatusLibrary.Log("To disable autoplay: edit eqemupatcher.yml or wait until next patch.");

            IniLibrary.Save();
        }

        private void chkAutoPatch_CheckedChanged(object sender, EventArgs e)
        {
            if (isLoading) return;
            isAutoPatch = chkAutoPatch.Checked;
            IniLibrary.instance.AutoPatch = (isAutoPatch) ? "true" : "false";
            IniLibrary.Save();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (isAutoPatch)
            {
                if (!isLoading)
                {
                    StartPatch();
                    return;
                }
                isPendingPatch = true;
                pendingPatchTimer.Enabled = true;
                StatusLibrary.Log("Checking for updates...");
                btnCheck.Text = "Cancel";
            }
        }

        private string generateSize(double size) {
            if (size < 1024) {
                return $"{Math.Round(size, 2)} bytes";
            }

            size /= 1024;
            if (size < 1024)
            {
                return $"{Math.Round(size, 2)} KB";
            }

            size /= 1024;
            if (size < 1024)
            {
                return $"{Math.Round(size, 2)} MB";
            }

            size /= 1024;
            if (size < 1024)
            {
                return $"{Math.Round(size, 2)} GB";
            }

            return $"{Math.Round(size, 2)} TB";
        }

        private void pendingPatchTimer_Tick(object sender, EventArgs e)
        {
            if (isLoading) return;
            pendingPatchTimer.Enabled = false;
            isPendingPatch = false;
            btnCheck_Click(sender, e);
        }
    }

    public class FileList
    {
        public string version { get; set; }

        public List<FileEntry> deletes { get; set; }
        public string downloadprefix { get; set; }
        public List<FileEntry> downloads { get; set; }
        public List<FileEntry> unpacks { get; set; }

    }

    public class FileEntry
    {
        public string name { get; set;  }
        public string md5 { get; set; }
        public string date { get; set; }
        public string zip { get; set; }
        public int size { get; set; }
    }
}


