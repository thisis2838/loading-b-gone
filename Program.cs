using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace loading_b_gone_ui
{
    static class Program
    {
        public static Version Version = new Version("1.0");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            Thread _checkVersion = new Thread(new ThreadStart(() =>
            {
                using (WebClient wb = new WebClient())
                {
                    try
                    {
                        Version ver = Version.Parse(wb.DownloadString(@"https://raw.githubusercontent.com/thisis2838/loading-b-gone/main/current-version.txt"));
                        if (ver > Version)
                        {
                            if (MessageBox.Show(
                                $"There is a new version released! ({ver} > {Version})\r\n" +
                                $"Would you like to open the download page?", "New Update", MessageBoxButtons.YesNo) == DialogResult.Yes)
                                Process.Start(@"https://github.com/thisis2838/loading-b-gone/releases");
                        }

                    }
                    catch { }
                }
            }));

            _checkVersion.Start();

            Application.EnableVisualStyles();
            CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
