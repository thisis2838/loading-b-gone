using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace loading_b_gone_ui
{
    static class Util
    {
        public static void OpenFileDialog(ref TextBox box, string filter = "", string initDir = "")
        {
            OpenFileDialog diag = new OpenFileDialog();
            diag.Filter = filter;

            try { initDir = Path.GetDirectoryName(initDir); }
            catch { initDir = ""; }
            diag.InitialDirectory = initDir;

            if (diag.ShowDialog() == DialogResult.OK)
                box.Text = diag.FileName;
        }

        public static string SaveFileDialog(string filter = "", string initDir = "")
        {
            SaveFileDialog diag = new SaveFileDialog();
            diag.Filter = filter;

            try { initDir = Path.GetDirectoryName(initDir); }
            catch { initDir = ""; }
            diag.InitialDirectory = initDir;

            if (diag.ShowDialog() == DialogResult.OK)
                return diag.FileName;

            return "";
        }

        public static (double, string) GetMatchedTime(string input, double FPS = 30)
        {
            Match match = Regex.Match(input, $@"^(-?[0-9:\.]+)$");
            Match match2 = Regex.Match(input, $@"^(-?[0-9\.]+)(?:f{{1}})");

            if (match2.Success)
            {
                if(double.TryParse(match2.Groups[1].Value, out Double val))
                    return (val / FPS, $"{val} frame{(Math.Abs(val) == 1 ? "" : "s")} => {val / FPS:0.000}s");
            }
            else if (match.Success)
            {
                if (!double.TryParse(match.Groups[1].Value, out double val))
                {
                    if (!TimeSpan.TryParse(match.Groups[1].Value, out TimeSpan tmp))
                        return (0, "=> 0s");
                    else return (tmp.TotalSeconds, $"=> {tmp.TotalSeconds:0.###}s");
                }
                else return (val, $"=> {val:0.000}s");
            }

            return (0, "=> 0s");
        }

        public static string StartAndGetOutput(string name, string args)
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

        public static void ParseInput(this NumericUpDown nud, string input)
        {
            if (decimal.TryParse(input, out decimal tmp) && nud.Minimum <= tmp && nud.Maximum >= tmp)
                nud.Value = tmp;
            else nud.Value = nud.Minimum;
        }
    }

    public class TextBoxListener : TraceListener
    {
        private TextBoxBase output;

        public TextBoxListener(TextBoxBase output)
        {
            this.Name = "Trace";
            this.output = output;
        }

        public override void Write(string message)
        {
            Action append = delegate () 
            {
                output.AppendText($"[{DateTime.Now}] {message}");
            };

            if (output.InvokeRequired)
                output.BeginInvoke(append);
            else
                append();
        }

        public override void WriteLine(string message)
        {
            Write(message + Environment.NewLine);
        }
    }
}
