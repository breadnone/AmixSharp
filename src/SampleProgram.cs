using System.Diagnostics;

namespace AmixSharp;

class Program
{
    static void Main(string[] args)
    {
        //Change to your twitch stream key
        var streamKey = "YourTwitchStreamKey";

        Console.WriteLine("Hello, World!");

        //NOTE THIS IS MEANT TO BE AN EXAMPLE FOR NVIDIA GPUS. 
        //DONT USE THIS EXAMPLE ARGS AS IS. MAKE YOUR OWN FFMPEG ARGS.
        //ITS TO SHOWCASE HOW TO UTILIZE FFMPEG's PIPE FEATURE FROM C#
        //NOTE THE PIPE MUST EXISTS AS INPUT FOR THE AUDIO TO BE FED WITH BYTE STREAM
        var arguments = $"-hide_banner -avioflags direct -err_detect aggressive -fflags discardcorrupt -fflags nobuffer+genpts+fastseek -flags low_delay -loglevel verbose -probesize 32 -init_hw_device d3d11va -hwaccel_device 0 -hwaccel cuda -hwaccel_output_format cuda -rtbufsize 256M " +
                    $"-filter_complex \"ddagrab=output_idx=0:framerate=60:video_size=1920x1080,hwdownload,format=bgra,format=yuv420p,hwupload_cuda,scale_cuda=1920:1080:format=yuv420p:interp_algo=lanczos:force_original_aspect_ratio=decrease,setpts=PTS-STARTPTS[vstream]\" " +
                    $"-itsoffset 0 -thread_queue_size 8192 -f s16le -acodec pcm_s16le -ac 2 -ar 44100 -i pipe:0 " +
                    $"-map \"[vstream]\" -map 0:a -af asetpts=NB_CONSUMED_SAMPLES/SR/TB,aresample=async=1:first_pts=0 " +
                    $"-fps_mode cfr -c:v h264_nvenc -preset p7 -gpu 0 -maxrate 3000k -bufsize 6000k -g {60 * 2} " +
                    $"-c:a aac -b:a 192k -ar 44100 -shortest -reconnect 1 -reconnect_at_eof 1 -reconnect_streamed 1 -reconnect_on_network_error 1 -r 60 -f flv -flvflags no_duration_filesize \"rtmp://live.twitch.tv/app/{streamKey}\"";

        var _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "PathToFFMPEGexe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },

            EnableRaisingEvents = true
        };

        _ffmpegProcess.Exited += ProcessExitedCallback;
        _ffmpegProcess.OutputDataReceived += OutputDataCallback;
        _ffmpegProcess.ErrorDataReceived += OutputDataCallback;

        _ffmpegProcess.Start();
        _ffmpegProcess.BeginOutputReadLine();
        _ffmpegProcess.BeginErrorReadLine();

        //Must be wrapped in CreateMainThread otherwise it will block current thread.
        AudioMixer.CreateMainThread(() =>
        {
            //only 44100 and 4800 PCM for input options, same goes for output
            //OUTPUT will be 16bit pcm.. so on ffmpeg side make sure the pipe input uses " s16le -acodec pcm_s16le -ac 2 -ar YourOutputSampleRateHere(44100 or 48000)"
            var audiomixer = new AudioMixer(48000, 16, 2, outputSampleRate: 44100, 5)
            .CreateChannel(AudioChannel.ChannelType.Audio, CSCore.AudioEncoding.Pcm, "main", CSCore.CoreAudioAPI.DataFlow.Render, CSCore.CoreAudioAPI.Role.Multimedia)
            .CreateChannel(AudioChannel.ChannelType.Microphone, CSCore.AudioEncoding.Pcm, "mic", CSCore.CoreAudioAPI.DataFlow.Capture, CSCore.CoreAudioAPI.Role.Communications);
            audiomixer.Start(_ffmpegProcess);
        });
    }
    static void OutputDataCallback(object sender, EventArgs evt)
    {

    }
    static void ProcessExitedCallback(object sender, EventArgs evt)
    {

    }
}