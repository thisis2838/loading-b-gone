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

        public double Length() => End - Start;

        public override string ToString()
        {
            return $"{Start} -> {End}";
        }
    }

    class TimeStampFile
    {
        public List<TimeStamp> TimeStamps;

        public TimeStampFile()
        {
            TimeStamps = new List<TimeStamp>();
        }

        public bool Parse(string file)
        {
            int fails = 0;
            var lines = File.ReadAllLines(file)
                .Where(x => !string.IsNullOrWhiteSpace(x)
                && !(x.Length >= 2 && (x.Substring(0, 2) == "//")));

            foreach (string x in lines)
            {
                string[] members = x.Split(',');
                try
                {
                    TimeStamps.Add(new TimeStamp(TimeSpan.Parse(members[0]).TotalSeconds, TimeSpan.Parse(members[1]).TotalSeconds));
                }
                catch
                {
                    if (fails++ > 10)
                    {
                        Trace.WriteLine("Too many fails while trying to parse Timestamp file, aborting!");
                        TimeStamps = new List<TimeStamp>();
                        return false;
                    }
                    continue;
                }
            }

            return true;
        }

        private List<TimeStamp> EnumerateTrimmingStamps(double videoStart, double RTAStart, double startOff, double endOff)
        {
            List<TimeStamp> trimStamps = new List<TimeStamp>();
            TimeStamp current = new TimeStamp(0, 0);

            double runOff = videoStart - RTAStart;
            var newStamps = TimeStamps.ConvertAll(x => new TimeStamp(x.Start + runOff + startOff, x.End + runOff + endOff));
            for (int i = 0; i <= newStamps.Count; i++)
            {
                double startTime = i == 0 ? 0 : newStamps[i - 1].End;
                double endTime = i == newStamps.Count ? 0 : newStamps[i].Start;

                if (i < newStamps.Count && i > 0)
                {
                    if (newStamps[i].Start > newStamps[i].End)
                    {
                        Trace.WriteLine($"Load {i} has end time earlier than start time! ({newStamps[i].End:0.000} < {newStamps[i].Start:0.000})");
                        continue;
                    }

                    if (newStamps[i - 1].End > newStamps[i].Start)
                    {
                        Trace.WriteLine($"Load {i - 1} ends after load {i} starts! ({newStamps[i - 1].End:0.000} > {newStamps[i].Start:0.000})");
                        continue;
                    }
                }

                trimStamps.Add(new TimeStamp(startTime, endTime));
            }

            return trimStamps;
        }

        private double AverageSegmentDistance(List<TimeStamp> stamps)
        {
            if (stamps.Count <= 1)
                return 3;

            double totalDist = 0;
            int i = 1;
            for (; i < stamps.Count - 2; i++)
                totalDist += stamps[i + 1].End - stamps[i].Start;

            return totalDist / (i - 1);
        }

        public string MakeTrimScript(double videoStart, double RTAStart, double startOff, double endOff)
        {
            StringBuilder sb = new StringBuilder();

            var newStamps = EnumerateTrimmingStamps(videoStart, RTAStart, startOff, endOff);

            int n = 0;
            for (int i = 0; i <= newStamps.Count; i++)
            {
                string startStr = i == newStamps.Count ? $"start={newStamps[i - 1].Start}" : $"start={newStamps[i].Start}";
                string endStr = i == newStamps.Count ? "" : $":end={newStamps[i].End}";

                sb.Append($"[0:v]trim={startStr}{endStr},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={startStr}{endStr},asetpts=PTS-STARTPTS[{n}a];");
                n++;
            }

            for (int i = 0; i <= n - 1; i++)
                sb.Append($"[{i}v][{i}a]");
            sb.AppendLine($"concat=n={n}:v=1:a=1[outv][outa]");

            if (n == 0)
                Trace.WriteLine("No cuts were made!!");

            return sb.ToString();
        }

        public string MakePreviewScript(double videoStart, double RTAStart, double startOff, double endOff, int previews)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sbTmp = new StringBuilder();
            StringBuilder tail = new StringBuilder();
            StringBuilder tailTmp = new StringBuilder();

            const double gapLen = 0.25d; // length of black screen in between previews.

            var newStamps = EnumerateTrimmingStamps(videoStart, RTAStart, startOff, endOff);

            double delta = AverageSegmentDistance(newStamps);
            Trace.WriteLine($"Average distance between segments = {delta}s");
            delta = (delta > 3 ? 3 : delta);
            double margin = delta / 2 > 1 ? 1 : delta / 2;

            int n = 0, m = 0, nTemp = 0;
            void appendSeg(string startEndStr)
            {
                sbTmp.Append($"[0:v]trim={startEndStr},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={startEndStr},asetpts=PTS-STARTPTS[{n}a];");
                tailTmp.Append($"[{n}v][{n}a]");
                nTemp++;
                n++;
            }
            void appendBlack()
            {
                sbTmp.Append($"[2]trim=duration={gapLen}[b{m}v];[1]atrim=duration={gapLen}[b{m}a];");
                tailTmp.Append($"[b{m}v][b{m}a]");
                m++;

                nTemp = 0;

                sb.Append(sbTmp.ToString());
                tail.Append(tailTmp.ToString());

                sbTmp.Clear();
                tailTmp.Clear();
            }

            int startIndex = -1;
            for (int i = 0; n <= previews * 2 + 1 && i < newStamps.Count - 1; i++)
            {
                if (startIndex == -1)
                {
                    if (newStamps[i].Length() < margin)
                        continue;

                    appendSeg($"start={(newStamps[i].End > delta ? newStamps[i].End - margin : 0)}:end={newStamps[i].End}");
                    startIndex = i;
                    continue;
                }

                if (newStamps[i].Length() >= delta)
                {
                    startIndex = -1;
                    appendSeg($"start={newStamps[i].Start}:end={newStamps[i].Start + margin}");
                    appendBlack();
                }
                else
                    appendSeg($"start={newStamps[i].Start}:end={newStamps[i].End}");
            }

            if (n - nTemp == 0)
            {
                Trace.WriteLine("Short cut couldn't be made!");
                appendBlack();
            }

            sb.Append(tail.ToString());
            sb.Append($"concat=n={n - nTemp + m}:v=1:a=1[outv][outa]");

            return sb.ToString();
        }
    }
}
