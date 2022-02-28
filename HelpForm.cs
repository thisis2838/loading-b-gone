using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace loading_b_gone_ui
{
    public partial class HelpForm : Form
    {
        public HelpForm()
        {
            InitializeComponent();

            FormClosing += (s, e) =>
            {
                this.Hide();
                e.Cancel = true;
            };

            _loadBarLoc = loadLoad.Location.X;
            _loadSeg2OuterEdgePos = loadSegment2.Location.X + loadSegment2.Size.Width;
            _timeSecUnit = 1 / (double)loadLoad.Size.Width;

            UpdateLoadDemo();

            barCurTime_Scroll(null, null);
            _scrubTicker = new Timer();
            _scrubTicker.Interval = 10;
            _scrubTicker.Tick += (s, e) =>
            {
                if (barCurTime.Value == barCurTime.Maximum)
                {
                    _scrubTimer = false;
                }

                if (_scrubTimer)
                {
                    barCurTime.Value += 1;
                    barCurTime_Scroll(null, null);
                }

                butScrubTimer.Text = _scrubTimer ? "■" : "▶";
            };
            _scrubTicker.Start();
        }

        private int _loadBarLoc = 454;
        private int _loadBarSize = 74;
        private int _loadSeg2OuterEdgePos = 712;
        private double _timeSecUnit = 1 / 74d;

        private void UpdateLoadDemo()
        {
            void centerLab(PictureBox box, Label lab, string name)
            {
                lab.Text = $"{name}\r\n({box.Size.Width * _timeSecUnit:0.000}s)";
                lab.Location = new Point(box.Location.X + (box.Width - lab.Size.Width) / 2, lab.Location.Y);
                lab.Visible = box.Size.Width > 0;
            }

            int loadBarStart = _loadBarLoc + -barStart.Value * 10;
            int loadBarEnd = _loadBarLoc + _loadBarSize + barEnd.Value * 10;

            labStartOffVal.Text = (barStart.Value * _timeSecUnit * 10).ToString("0.000;-0.000") + "s";
            labEndOffVal.Text = (barEnd.Value * _timeSecUnit * 10).ToString("0.000;-0.000") + "s";

            loadLoad.Location = new Point(loadBarStart, loadLoad.Location.Y);
            loadLoad.Size = new Size(loadBarEnd - loadBarStart, loadLoad.Size.Height);

            bool loadVisible = (loadLoad.Size.Width > 0);

            labLoadSegment2.Visible = labLoadLoad.Visible = loadVisible;
            centerLab(loadLoad, labLoadLoad, "Trimmed Segment");

            if (loadVisible)
            {
                loadSegment1.Size = new Size(loadBarStart - loadSegment1.Location.X, loadSegment1.Height);
                centerLab(loadSegment1, labLoadSegment1, "Segment 1");

                loadSegment2.Size = new Size(_loadSeg2OuterEdgePos - loadBarEnd, loadSegment2.Height);
                loadSegment2.Location = new Point(loadBarEnd, loadSegment2.Location.Y);
                centerLab(loadSegment2, labLoadSegment2, "Segment 2");
            }
            else
            {
                loadSegment1.Size = new Size(_loadSeg2OuterEdgePos - loadSegment1.Location.X, loadSegment1.Height);
                centerLab(loadSegment1, labLoadSegment1, "Segment");
            }
        }
        private void barStart_Scroll(object sender, EventArgs e)
        {
            UpdateLoadDemo();
        }
        private void barEnd_Scroll(object sender, EventArgs e)
        {
            UpdateLoadDemo();
        }

        private void butDownFFmpeg_Click(object sender, EventArgs e)
        {
            Process.Start(
                (new Random()).Next(1, 10) > 5 ?
                @"https://www.gyan.dev/ffmpeg/builds/" :
                @"https://github.com/BtbN/FFmpeg-Builds/releases");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/mpc-hc/mpc-hc/releases/tag/1.7.13");
        }


        private const double _demoVideoLength = 10;
        private const double _demoRunStartedAt = 2d;
        private const double _runStartOffset = 1.5d;
        private List<TimeStamp> _demoLoads = new List<TimeStamp>()
        {
            new TimeStamp(1.5, 2.15),
            new TimeStamp(4, 4.785),
            new TimeStamp(5.125, 5.325),
            new TimeStamp(5.425, 5.5178966),
            new TimeStamp(6.425, 7.06),
        };
        private const int _labStateYPos = 63;
        private const int _labStateXPos = 431;
        private Timer _scrubTicker;
        private bool _scrubTimer = false;

        private void barCurTime_Scroll(object sender, EventArgs e)
        {
            string formatTime(double time)
            {
                return TimeSpan.FromSeconds(time).ToString(@"mm\:ss\.fff");
            }
            void setState(string txt)
            {
                labCurState.Text = txt;
                labCurState.Invalidate();
                labCurState.Location = new Point(
                    _labStateXPos - labCurState.Size.Width / 2
                    , _labStateYPos - labCurState.Size.Height / 2);
            }

            double progress = (double)barCurTime.Value / (double)barCurTime.Maximum;
            double curScrub = (_demoVideoLength + (new Random().Next(0, 8) / 1000d)) * progress;
            curScrub = curScrub > 10 ? 10 : curScrub;
            labCurScrub.Text = formatTime(curScrub) + " / " + formatTime(_demoVideoLength);

            double rtaTime = (curScrub > _demoRunStartedAt) ?
                curScrub - _demoRunStartedAt
                : 0;

            if (rtaTime > 0)
            {
                labCurIGT.ForeColor = Color.FromArgb(0, 204, 54);
                double igtTime = rtaTime - _demoLoads.Where(x => x.End < rtaTime).Sum(x => x.Length());
                var curLoad = _demoLoads.Where(x => x.End > rtaTime && x.Start < rtaTime);

                setState("In a run!");

                if (curLoad.Count() > 0)
                {
                    setState("LOAD TIME");
                    igtTime -= rtaTime - curLoad.First().Start;
                }

                labCurIGT.Text = formatTime(igtTime);
                labVideoStart.Text = formatTime(curScrub);
                labRTAStart.Text = formatTime(rtaTime + _runStartOffset);
                labRTAStart.Visible = labVideoStart.Visible = true;
            }
            else
            {
                labCurIGT.ForeColor = Color.FromArgb(255, 255, 255);
                labRTAStart.Text = labVideoStart.Text = labCurIGT.Text = formatTime(0);
                labRTAStart.Visible = labVideoStart.Visible = false;

                setState("Not in a run!");
            }

            labCurRTA.Text = formatTime(rtaTime + _runStartOffset);
        }

        private void butScrubTimer_Click(object sender, EventArgs e)
        {
            _scrubTimer = !_scrubTimer;
            butScrubTimer.Text = _scrubTimer ? "⏸" : "▶";
        }
    }
}
