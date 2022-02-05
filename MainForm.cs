using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static loading_b_gone_ui.Util;

namespace loading_b_gone_ui
{
    public partial class MainForm : Form
    {
        public static SettingsHandler Settings;

        private TimeStampFile _tsFile;
        private MediaInfo _videoInfo;

        private string[] _lastFileDiagDir = new string[4] { "", "", "", "" };

        private HelpForm _help = new HelpForm();

        private double _FPS = -1;
        private double FPS 
        { 
            get 
            {
                if (_FPS == -1)
                    return (double)(nudDefFPS?.Value ?? 30);
                else return _FPS;
            } 
            set { _FPS = value; nudDefFPS_ValueChanged(null, null); }
        }

        private bool ReadyForProcessing 
        { 
            get
            {
                return 
                    (gActions?.Enabled ?? false ) && 
                    (gStartOffsets?.Enabled ?? false) && 
                    (gLoadOffsets?.Enabled ?? false);
            }
            set
            {
                gActions.Enabled = gStartOffsets.Enabled = gLoadOffsets.Enabled = value;
            }
        }

        public MainForm()
        {
            InitializeComponent();

            Settings = new SettingsHandler();

            TraceListener debugListener = new TextBoxListener(boxMessages);
            Debug.Listeners.Add(debugListener);

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;

            this.Text = $"Loading-B-Gone v{Program.Version.ToString()}";
            labVersion.Text = $"v{Program.Version.ToString()}";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.WriteSettings();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Settings.SubscribedSettings.Add(new SettingEntry(
                "ffmpeg_path",
                s => boxFFmpegPath.Text = s,
                () => boxFFmpegPath.Text));
            Settings.SubscribedSettings.Add(new SettingEntry(
                "ffprobe_path",
                s => boxFFprobePath.Text = s,
                () => boxFFprobePath.Text));
            Settings.SubscribedSettings.Add(new SettingEntry(
                "default_fps",
                s => nudDefFPS.ParseInput(s),
                () => nudDefFPS.Value.ToString()));
            Settings.SubscribedSettings.Add(new SettingEntry(
                "start_offset",
                s => boxStartOffset.Text = s,
                () => boxStartOffset.Text));
            Settings.SubscribedSettings.Add(new SettingEntry(
                "end_offset",
                s => boxEndOffset.Text = s,
                () => boxEndOffset.Text));
            Settings.SubscribedSettings.Add(new SettingEntry(
                "num_previews",
                s => nudNumPreviews.ParseInput(s),
                () => nudNumPreviews.Value.ToString()));
            Settings.SubscribedSettings.Add(new SettingEntry(
                "last_paths",
                s => 
                {
                    var members = s.Split('|');
                    if (members.Length < _lastFileDiagDir.Length)
                        return;

                    members.CopyTo(_lastFileDiagDir, 0);
                },
                () => string.Join("|", _lastFileDiagDir)));

            Settings.LoadSettings();
            boxMessages.Clear();
            UpdateDisabled();
            FPS =  -1;
        }

        private void UpdateDisabled()
        {
            if (!File.Exists(boxFFmpegPath.Text) || !File.Exists(boxFFprobePath.Text))
            {
                tabControl1.SelectedIndex = 1;
                Trace.WriteLine("Please enter in the paths for FFmpeg and FFprobe to continue! If this is your first time, click the Help button on the top right to get started.");
                ((Control)tabPage1).Enabled = false;
            }
            else
            {
                tabControl1.SelectedIndex = 0;
                ((Control)tabPage1).Enabled = true;
            }

            ReadyForProcessing =
                (File.Exists(boxVideoPath.Text) && File.Exists(boxTimestampPath.Text));
            if (ReadyForProcessing) LoadVideoAndTimeStamps();
            else ResetVideoAndTimeStamps();

        }

        private void ResetVideoAndTimeStamps()
        {
            FPS = -1;
            _tsFile = null;
        }

        private void LoadVideoAndTimeStamps()
        {
            _videoInfo = new MediaInfo(boxVideoPath.Text, boxFFprobePath.Text);

            string[] fpsRatio = _videoInfo.FPS.Split('/');
            if (fpsRatio.Length == 2 && fpsRatio.All(x => x != "0"))
                FPS = double.Parse(fpsRatio[0]) / double.Parse(fpsRatio[1]);
            else
            {
                FPS = -1;
                Trace.WriteLine($"Couldn't figure out FPS! Defaulting to {FPS}");
            }

            _tsFile = new TimeStampFile(boxTimestampPath.Text);
            Trace.WriteLine($"Loaded {_tsFile.TimeStamps.Count()} timestamps");
        }

        private void ExecuteTrim(string script, string outputFile)
        {
            File.WriteAllText("params.txt", script);
            string launchParams =
                $" -i \"{boxVideoPath.Text}\" -f lavfi -i anullsrc -f lavfi -i \"color=c=black:s={_videoInfo.Resolution}:r={_videoInfo.FPS}\" " +
                $" -filter_complex_script \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "params.txt")}\" " +
                $" -map \"[outv]\" -map \"[outa]\"" +
                $" \"{outputFile}\"";
            Trace.WriteLine($"Launch params:\n{launchParams}\n\n");

            Process.Start("cmd.exe", $"/C " +
                $"{Path.GetPathRoot(boxFFmpegPath.Text).Replace("\\", "")} " +
                $"&\"{boxFFmpegPath.Text}\" " +
                $"{launchParams}" +
                $"& PAUSE");

            //Process.Start(boxFFmpegPath.Text, launchParams);
        }

        private void butDoFull_Click(object sender, EventArgs e)
        {
            string filePath = SaveFileDialog("", string.IsNullOrWhiteSpace(_lastFileDiagDir[2]) ? _lastFileDiagDir[0] : _lastFileDiagDir[2]);

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _lastFileDiagDir[2] = filePath;

            string script = _tsFile.MakeTrimScript(
                GetMatchedTime(boxVideoStart.Text, FPS).Item1,
                GetMatchedTime(boxRTAStart.Text, FPS).Item1,
                GetMatchedTime(boxStartOffset.Text, FPS).Item1,
                GetMatchedTime(boxEndOffset.Text, FPS).Item1);

            ExecuteTrim(script, filePath);
        }

        private void butDoPreview_Click(object sender, EventArgs e)
        {
            string filePath = SaveFileDialog("", string.IsNullOrWhiteSpace(_lastFileDiagDir[3]) ? _lastFileDiagDir[0] : _lastFileDiagDir[3]);

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _lastFileDiagDir[3] = filePath;

            string script = _tsFile.MakePreviewScript(
                GetMatchedTime(boxVideoStart.Text, FPS).Item1,
                GetMatchedTime(boxRTAStart.Text, FPS).Item1,
                GetMatchedTime(boxStartOffset.Text, FPS).Item1,
                GetMatchedTime(boxEndOffset.Text, FPS).Item1,
                (int)nudNumPreviews.Value);

            ExecuteTrim(script, filePath);
        }

        private void butBrVideoPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog(ref boxVideoPath, initDir:_lastFileDiagDir[0]);
            _lastFileDiagDir[0] = boxVideoPath.Text;
        }

        private void butBrTimestampPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog(ref boxTimestampPath, initDir:_lastFileDiagDir[1]);
            _lastFileDiagDir[1] = boxTimestampPath.Text;
        }

        private void butBrFFmpegPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog(ref boxFFmpegPath, "ffmpeg.exe|ffmpeg.exe");
        }

        private void butBrFFprobePath_Click(object sender, EventArgs e)
        {
            OpenFileDialog(ref boxFFprobePath, "ffprobe.exe|ffprobe.exe");
        }


        private void boxVideoStart_TextChanged(object sender, EventArgs e)
        {
            labEquVideoStart.Text = GetMatchedTime(boxVideoStart.Text, FPS).Item2;
        }

        private void boxRTAStart_TextChanged(object sender, EventArgs e)
        {
            labEquRTAStart.Text = GetMatchedTime(boxRTAStart.Text, FPS).Item2;
        }

        private void boxStartOffset_TextChanged(object sender, EventArgs e)
        {
            labEquStartOffset.Text = GetMatchedTime(boxStartOffset.Text, FPS).Item2;
        }

        private void boxEndOffset_TextChanged(object sender, EventArgs e)
        {
            labEquEndOffset.Text = GetMatchedTime(boxEndOffset.Text, FPS).Item2;
        }

        private void nudDefFPS_ValueChanged(object sender, EventArgs e)
        {
            boxEndOffset_TextChanged(null, null);
            boxStartOffset_TextChanged(null, null);
            boxRTAStart_TextChanged(null, null);
            boxVideoStart_TextChanged(null, null);
        }

        
        private void boxFFmpegPath_TextChanged(object sender, EventArgs e)
        {
            UpdateDisabled();
        }

        private void boxFFprobePath_TextChanged(object sender, EventArgs e)
        {
            UpdateDisabled();
        }

        private void boxVideoPath_TextChanged(object sender, EventArgs e)
        {
            if (File.Exists(boxVideoPath.Text))
                _lastFileDiagDir[0] = Path.GetDirectoryName(boxVideoPath.Text);

            UpdateDisabled();
        }

        private void boxTimestampPath_TextChanged(object sender, EventArgs e)
        {
            if (File.Exists(boxVideoPath.Text))
                _lastFileDiagDir[1] = Path.GetDirectoryName(boxVideoPath.Text);

            UpdateDisabled();
        }

        private void butHelp_Click(object sender, EventArgs e)
        {
            _help.Show();
        }

        private void butReportBug_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/thisis2838/loading-b-gone/issues");
        }
    }
}
