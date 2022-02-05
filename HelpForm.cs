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

            int loadBarStart = _loadBarLoc + barStart.Value * 10;
            int loadBarEnd = _loadBarLoc + _loadBarSize + barEnd.Value * 10;

            labStartOffVal.Text = (barStart.Value * _timeSecUnit * 10).ToString("0.000;-0.000") + "s";
            labEndOffVal.Text = (barEnd.Value * _timeSecUnit * 10).ToString("0.000;-0.000") + "s";

            loadLoad.Location = new Point(loadBarStart, loadLoad.Location.Y);
            loadLoad.Size = new Size(loadBarEnd - loadBarStart, loadLoad.Size.Height);

            bool loadVisible = (loadLoad.Size.Width > 0);

            labLoadSegment2.Visible = labLoadLoad.Visible = loadVisible;
            centerLab(loadLoad, labLoadLoad, "Load");

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
    }
}
