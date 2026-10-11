using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EQEmu_Patcher
{
    static class Program
    {
        internal static bool SelectReviewMode(string[] args)
        {
            if(args.Length>1 || (args.Length==1 && args[0]!="--offline"))
                throw new ArgumentException("This patcher accepts --offline for a prepared local client.");
#if ONLINE_PATCHER
            return args.Length==1;
#else
            // A review executable must be safe when opened directly, without a CMD wrapper.
            return true;
#endif
        }
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if(args.Length==1&&args[0]=="--licenses") {
                Application.EnableVisualStyles();MainForm.ShowLicenseNotices();return;
            }
            try {MainForm.OfflineReview=SelectReviewMode(args);}
            catch(ArgumentException error) {MessageBox.Show(error.Message,"Project Zek");return;}
            // Ensure modern TLS for HTTPS endpoints (GitHub raw/releases)
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
