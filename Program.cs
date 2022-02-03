using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using static System.Console;
using System.Threading;

namespace loading_b_gone
{
    class Program
    {
        public static string Prompt(string question)
        {
            Write("- " + question);
            if (question.Length > 50)
                WriteLine();
            CursorLeft = 50;
            return ReadLine();
        }

        private static string StartAndGetOutput(string name, string args)
        {
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = name;
            p.StartInfo.Arguments = args;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return output;
        }

        static string Version = "0.1";

        static void Main(string[] args)
        {
            again:
            Clear();
            WriteLine($"\n\n\nLoading-B-Gone\nv.{Version}\nby 2838 - 2022-02-03\n\nPlease report any bugs to https://github.com/thisis2838/loading-b-gone/issues\n\n");
            try
            {
                string ffmpegPath = "";
                if (!(File.Exists("ffmpeg_path.txt") && File.Exists(Path.Combine(ffmpegPath = File.ReadAllText("ffmpeg_path.txt"), "ffmpeg.exe"))))
                {
                    ffmpegPath = Prompt("FFmpeg folder path? (enter download to open a download link)");

                    if (ffmpegPath == "download")
                    {
                        Process.Start(
                            (new Random()).Next(1, 10) > 5 ?
                            @"https://www.gyan.dev/ffmpeg/builds/" :
                            @"https://github.com/BtbN/FFmpeg-Builds/releases");
                        goto again;
                    }

                    if (!Directory.Exists(ffmpegPath))
                        throw new Exception("Provided ffmpeg path does not exist!");

                    if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg.exe")))
                        throw new Exception("Provided ffmpeg path does not include the necessary files (ffmpeg)!");

                    File.WriteAllText("ffmpeg_path.txt", ffmpegPath);
                }

                string file = Prompt("Input video?").Trim('\"');
                if (!File.Exists(file))
                    throw new Exception("Video not found!");
                string timestamps = Prompt("Timestamp file?").Trim('\"');
                if (!File.Exists(timestamps))
                    throw new Exception("Timestamp file not found!");

                WriteLine("\n");

                double rate = -1;
                if (File.Exists(Path.Combine(ffmpegPath, "ffprobe.exe")))
                {
                    string[] fpsRatio = StartAndGetOutput(
                        Path.Combine(ffmpegPath, "ffprobe.exe"),
                        $"-v 0 -of csv=p=0 -select_streams v:0 -show_entries stream=r_frame_rate \"{file}\"").Split('/');
                    if (fpsRatio.Length == 2 && fpsRatio.All(x => x != "0"))
                    {
                        rate = double.Parse(fpsRatio[1]) / double.Parse(fpsRatio[0]);
                        WriteLine($"Video fps is {fpsRatio[0]}/{fpsRatio[1].Trim('\r', '\n')}fps => rate = {rate}\n");
                    }
                }

                double getMatchedTime(string input, double old)
                {
                    Match match = Regex.Match(input, $@"([\-0-9:\.]+)");
                    Match match2 = Regex.Match(input, $@"([\-0-9:\.]+)(?:f{{1}})");
                    double output = old;

                    if (match2.Success)
                    {
                        if (rate == -1)
                        {
                            rate = 1 / double.Parse(Prompt(
                                "Frame values detected in config, however the video's framerate " +
                                "couldn't be determined due to ffprobe.exe not being found or something wrong happened during processing. " +
                                "\nAs such please enter the video's FPS here:"));
                        }
                        output = rate * double.Parse(match2.Groups[1].Value);
                    }
                    else if (match.Success)
                    {
                        if (!TimeSpan.TryParse(match.Groups[1].Value, out TimeSpan tmp))
                        {
                            if (!double.TryParse(match.Groups[1].Value, out output))
                                output = old;
                        }
                        else output = tmp.TotalSeconds;
                    }

                    return output;
                }

                WriteLine("Please enter settings for cutting, enter nothing for \"0\"");
                double videoStart = getMatchedTime(Prompt("Exact time in the video when the run starts"), 0);
                double rtaStart = getMatchedTime(Prompt("Livesplit's RTA Time when run started"), 0);
                double loadBeginOffset = getMatchedTime(Prompt("Time to add onto load's start time"), 0);
                double loadEndOffset = getMatchedTime(Prompt("Time to add onto load's end time"), 0);

                List<(double, double)> tS = new List<(double, double)>();
                var lines = File.ReadAllLines(timestamps).Where(x => !string.IsNullOrWhiteSpace(x));
                foreach (string x in lines)
                {
                    string[] members = x.Split(',');
                    try
                    {
                        tS.Add((TimeSpan.Parse(members[0]).TotalSeconds, TimeSpan.Parse(members[1]).TotalSeconds));
                    }
                    catch { continue; }
                }

                Console.WriteLine($@"

Run starts at {videoStart}s into video {(videoStart == 0 ? "<== Are you sure?" : "")}
Run's RTA time starts at {rtaStart}s

Load start offset = {loadBeginOffset}s
Load end offset   = {loadEndOffset}s

[1] to do a full cut
[2] to do a preview of a few loads taken out (a second before and after each will be shown)

");
                string proceed = Console.ReadLine();
                WriteLine();

                bool shortCut = proceed.Trim() == "2";
                double off = videoStart - rtaStart;
                tS = tS.ConvertAll(x => (x.Item1 + off + loadBeginOffset, x.Item2 + off + loadEndOffset));
                if (tS.Count == 0)
                    throw new Exception("no loads found!");
                StringBuilder sb = new StringBuilder();
                int n = 0;

                const int previews = 10;

                if (!(shortCut && tS.Count > previews + 3))
                {
                    for (int i = 0; i <= tS.Count; i++)
                    {
                        string startStr = i == 0 ? "start=0" : $"start={tS[i - 1].Item2}";
                        string endStr = i == tS.Count ? "" : $":end={tS[i].Item1}";

                        if (i < tS.Count && i > 0)
                        {
                            if (tS[i].Item1 > tS[i].Item2)
                            {
                                WriteLine($"Load {i} has end time earlier than start time! ({tS[i].Item2} < {tS[i].Item1})");
                                continue;
                            }

                            if (tS[i - 1].Item2 > tS[i].Item1)
                            {
                                WriteLine($"Load {i - 1} ends after load {i} starts! ({tS[i - 1].Item2} > {tS[i].Item1})");
                                continue;
                            }
                        }
                            
                        sb.AppendLine($"[0:v]trim={startStr}{endStr},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={startStr}{endStr},asetpts=PTS-STARTPTS[{n}a];");
                        n++;
                    }
                }
                else
                {
                    for (int i = 2; n <= previews * 2 + 1 && i < tS.Count - 2; i++)
                    {
                        if (tS[i + 1].Item1 - tS[i].Item2 < 3 || tS[i].Item1 - tS[i - 1].Item2 < 3)
                        {
                            WriteLine($"Load {i} has less than 3 seconds on either side!");
                            continue;
                        }

                        string str1 = $"start={tS[i].Item1 - 1}:end={tS[i].Item1}";
                        string str2 = $"start={tS[i].Item2}:end={tS[i].Item2 + 1}";

                        sb.AppendLine($"[0:v]trim={str1},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={str1},asetpts=PTS-STARTPTS[{n}a];");
                        n++;
                        sb.AppendLine($"[0:v]trim={str2},setpts=PTS-STARTPTS[{n}v];[0:a]atrim={str2},asetpts=PTS-STARTPTS[{n}a];");
                        n++;
                    }
                }

                if (n == 0)
                    throw new Exception("no usuable segments");
                for (int i = 0; i <= n - 1; i++)
                    sb.Append($"[{i}v][{i}a]");
                sb.AppendLine($"concat=n={n}:v=1:a=1[outv][outa]");

                WriteLine("\nScript:\n" + sb.ToString());
                File.WriteAllText("params.txt", sb.ToString().Replace(Environment.NewLine, ""));
                string fileName = Path.GetFileName(file);
                string launchParams =
                    $" -i \"{file}\" -filter_complex_script \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "params.txt")}\" -map \"[outv]\" -map \"[outa]\"" +
                    $" \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)}\"";
                WriteLine($"Launch params:\n{launchParams}\n\n");
                var proc = Process.Start(Path.Combine(ffmpegPath, "ffmpeg.exe"), launchParams);
                proc.WaitForExit();

                WriteLine($"Outputted to {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)}");
                WriteLine("Done");
                ReadLine();
                goto again;
            }
            catch (Exception e)
            {
                WriteLine(e);

                WriteLine("\n\nEnter to continue");
                ReadLine();
                goto again;
            }

        }
    }
}
