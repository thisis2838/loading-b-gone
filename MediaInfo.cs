using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static loading_b_gone_ui.Util;

namespace loading_b_gone_ui
{
    class MediaInfo
    {
        public string FPS;
        public string Resolution;

        public MediaInfo(string file, string ffprobe)
        {
            FPS = StartAndGetOutput(ffprobe, $"-v 0 -of csv=p=0 -select_streams v:0 -show_entries stream=r_frame_rate \"{file}\"").Trim('\r', '\n');
            Resolution = StartAndGetOutput(ffprobe, $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 \"{file}\"").Trim('\r', '\n');

            Trace.WriteLine($"\r\nMedia: {file}\r\n\tFPS = {FPS}\r\n\tRes = {Resolution}\n");
        }
    }
}
