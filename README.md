# loading-b-gone
Loading-B-Gone is a command line utility that takes in Timestamps and generate command for use with
FFmpeg to cut load times from speedrun's recording.

These Timestamp files are generated in real time along with the run.

## Installation

### ASL Companion
- In your Livesplit layout, add a Scriptable Auto Splitter by clicking `+`, then hovering over `Control`
- In the newly opened window, set the Script Path to the path of the provided .asl file. 

### FFmpeg
- Builds of FFmpeg can be downloaded from any of these links: 
  + https://www.gyan.dev/ffmpeg/builds/
  + https://github.com/BtbN/FFmpeg-Builds/releases
- After downloading FFmpeg, copy the path to the folder containing `ffmpeg.exe`.
- Open the tool and enter in the path when prompted.
- Once FFmpeg setup is complete, values will be saved in the `ffmpeg_path.txt` file for future use, and the prompt will only appear again if anything goes wrong.

## How to use

### Generating Timestamps
- The aforementioned ASL script will automatically begin tracking game loads upon starting the timer. 
- Depending on the ASL, destination for the files will vary; for the one designed for Source games included in the download, they will be stored in the `loading-b-gone_timestamps` folder next to Livesplit's .exe file. How it will write this file can be customized in the ASL's settings accessible through the Layout Editor. 

### Modifying Timestamp settings
- Once you've finished your run and a timestamp file has been generated, open it with any text editor and modify the 4 parameters at the top. These are:
  + `run_start_video_timestamp` which is the time in the video recording at which Livesplit begins timing the run	
  + `timer_rta_started_at` which is the RTA time of Livesplit when it is started; or more specifically,	the earliest RTA time of the run seen in the video.
  + `load_begin_offset` which is how much time to add onto loads' start time.
  + `load_end_offset` which is how much time to add onto loads' end time.
- These parameters accept the following values
	 + Decimal values (eg. 1.256). These are taken in as seconds
	 + Formatted timestamp in the form of `hh:mm:ss.ffffff` (eg. 01:23:56.145 for 1 hours, 23 minutes	and 56.145 seconds)
	 + Frame count in the form of a decimal number followed by a lowercase f (eg. 2f for 2 frames; 3.5f	for 3 and one half a frame). 
Note that all of these can be negative. (eg. -2f for negative 2 frames; -58 for negative 58 seconds); and that these values affect every Timestamp in the file.
- For example:
  + `run_start_video_timestamp=52` means the run starts 52 seconds into the recording. 
  + `load_begin_offset=0.01` means to add 0.01 seconds to every load's start time; while `load_begin_offset=0.01f` adds 1/100ths of a frame instead.

### Cutting the video
- After obtaining and configuring the Timestamp file, run the tool and enter in the path to (or drag-and-drop) the video and the Timestamp file.
- From there follow any instruction given by the tool or by FFmpeg.
Note that it is highly recommended you do a preview first to check if the loads are removed correctly.

## For Devlopers

### Anatomy of a Timestamp file
- A Timestamp is a pair of 2 time values, formatted according to C#'s standard TimeSpan format of hh:mm:ss.ffffff, laid out in this form: `<load start time>,<load eng time>`. The values are taken from Livesplit's Real Time timer. 
- A file contains
  + Offset information referenced above, each on its own line. These values are set to 0 by default, and can be omitted from the file.
  + A list of Timestamps placed line by line, in order of when in the run they take place, from top to bottom.
