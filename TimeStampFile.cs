using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loading_b_gone_ui
{
    public struct TimeStamp
    {
        public double Start;
        public double End;

        public TimeStamp(double start, double end)
        {
            Start = start;
            End = end;
        }
    }

    class TimeStampFile
    {
        public List<TimeStamp> TimeStamps;

        public TimeStampFile(string file)
        {
            TimeStamps = new List<TimeStamp>();

            var lines = File.ReadAllLines(file).Where(x => !string.IsNullOrWhiteSpace(x));
            foreach (string x in lines)
            {
                string[] members = x.Split(',');
                try
                {
                    TimeStamps.Add(new TimeStamp(TimeSpan.Parse(members[0]).TotalSeconds, TimeSpan.Parse(members[1]).TotalSeconds));
                }
                catch { continue; }
            }
        }

        public string MakeTrimScript(double videoStart, double RTAStart, double startOff, double endOff)
        {
            StringBuilder sb = new StringBuilder();

            double runOff = videoStart - RTAStart;
            var newStamps = TimeStamps.ConvertAll(x => new TimeStamp(x.Start + runOff + startOff, x.End + runOff + endOff));

            int n = 0;
            for (int i = 0; i <= newStamps.Count; i++)
            {
                string startStr = i == 0 ? "start=0" : $"start={newStamps[i - 1].End}";
                string endStr = i == newStamps.Count ? "" : $":end={newStamps[i].Start}";

                if (i < newStamps.Count && i > 0)
                {
                    if (newStamps[i].Start > newStamps[i].End)
                    {
                        Debug.WriteLine($"Load {i} has end time earlier than start time! ({newStamps[i].End} < {newStamps[i].Start})");
                        continue;
                    }

                    if (newStamps[i - 1].End > newStamps[i].Start)
                    {
                        Debug.WriteLine($"Load {i - 1} ends after load {i} starts! ({newStamps[i - 1].End} > {newStamps[i].Start})");
                        continue;
                    }
                }

                sb.Append($"[0:v]trim={startStr}{endStr},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={startStr}{endStr},asetpts=PTS-STARTPTS[{n}a];");
                n++;
            }

            for (int i = 0; i <= n - 1; i++)
                sb.Append($"[{i}v][{i}a]");
            sb.AppendLine($"concat=n={n}:v=1:a=1[outv][outa]");

            return sb.ToString();
        }

        public string MakePreviewScript(double videoStart, double RTAStart, double startOff, double endOff, int previews)
        {
            StringBuilder sb = new StringBuilder();

            const double gapLen = 0.25d; // length of black screen in between previews.

            double runOff = videoStart - RTAStart;
            var newStamps = TimeStamps.ConvertAll(x => new TimeStamp(x.Start + runOff + startOff, x.End + runOff + endOff));

            int n = 0, m = 0;
            for (int i = 2; n <= previews * 2 + 1 && i < newStamps.Count - 2; i++)
            {
                if (!(newStamps.Count - i < previews) // too few to leave out
                    && (newStamps[i + 1].Start - newStamps[i].End < 3 || newStamps[i].Start - newStamps[i - 1].End < 3))
                {
                    Debug.WriteLine($"Load {i} has less than 3 seconds on either side!");
                    continue;
                }

                string str1 = $"start={newStamps[i].Start - 1}:end={newStamps[i].Start}";
                string str2 = $"start={newStamps[i].End}:end={newStamps[i].End + 1}";

                sb.Append($"[0:v]trim={str1},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={str1},asetpts=PTS-STARTPTS[{n}a];");
                n++;
                sb.Append($"[0:v]trim={str2},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={str2},asetpts=PTS-STARTPTS[{n}a];" +
                    $"[2]trim=duration={gapLen}[b{m}v];[1]atrim=duration={gapLen}[b{m}a];");
                m++;
                n++;
            }

            for (int i = 0; i <= n - 1; i++)
                sb.Append($"[{i}v][{i}a]{((i + 1) % 2 == 0 ? $"[b{i >> 1}v][b{i >> 1}a]" : "")}");
            sb.AppendLine($"concat=n={n + m}:v=1:a=1[outv][outa]");

            return sb.ToString();
        }
    }
}
