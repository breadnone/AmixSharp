using System.Diagnostics;
using CSCore;
using CSCore.CoreAudioAPI;

namespace AmixSharp;

public class AudioMixer : IDisposable
{
    public bool Disposed { get; private set; }
    public List<AudioChannel> channelPool => AudioChannel.channels;
    public static readonly List<AudioMixer> mixers = new List<AudioMixer>();
    SubMixer mixer;
    DoubleBufferOut doublebuffer;
    public int outputSampleRate { get; private set; }
    public int sampleRate { get; private set; }
    public int bits { get; private set; }
    public int channel { get; private set; }
    private int latency;
    /// <summary>AudioMixer class.</summary>
    /// <param name="sampleRate">Input sample rate</param>
    /// <param name="bit">Input bit depth.</param>
    /// <param name="channel">Channel count.</param>
    /// <param name="outputSampleRate">Output sample rate. Only 2 options available, they're "44100" and "48000".</param>
    /// <param name="latency">Buffer latency. Default is 100ms</param>
    public AudioMixer(int sampleRate, int bit, int channel, int outputSampleRate, int latency = 100)
    {
        this.latency = latency;
        this.sampleRate = sampleRate;
        this.bits = bits;
        this.channel = channel;
        this.outputSampleRate = outputSampleRate;
        mixers.Add(this);
        AudioAPI.CaptureAudioContext();
        GetAudioDevices(DataFlow.Render);
    }

    private void InitSoundMixer(Process process, int outputSampleRate)
    {
        if (channelPool.Count == 0) throw new Exception("Error : Channels have not been created yet.");

        if (process.HasExited)
        {
            Console.WriteLine("FFMPeg process was exited. Aborting running audio mixer");
        }

        var chn = channelPool[0];
        AudioEncoding encoding = chn.IsMicrophone ? chn.wasapiCapture.WaveFormat.WaveFormatTag : chn.wasapiCaptureLoopback.WaveFormat.WaveFormatTag;
        mixer = new SubMixer(chn.sampleRate, chn.bits, chn.channel, encoding) { FillWithZeros = false, DivideResult = false };
        doublebuffer = new DoubleBufferOut(mixer, chn.sampleRate, chn.bits, chn.channel, outputSampleRate);
        doublebuffer.Start();

        foreach (var itm in AudioChannel.channels)
        {
            doublebuffer.AddSource(itm.channelSource.ToStereo().ToSampleSource());
        }

        doublebuffer.DataAvailable += (x, y) =>
        {
            try
            {
                var stream = process.StandardInput.BaseStream;

                if (!process.HasExited && stream.CanWrite)
                {
                    stream.Write(y.Data, y.Offset, y.ByteCount);
                    //stream.Flush();
                }
            }
            catch (Exception)
            {
                StopAndDisposeAudioMixers();
                Console.WriteLine("Warning : Invalid stream while terminating, its safe to ignore this.");
            }
        };
    }
    public void SetGlobalMute(bool state)
    {
        foreach (var itm in channelPool)
        {
            itm.SetChannelMute(state);
        }
    }
    public void SetGlobalVolume(float value)
    {
        foreach (var itm in channelPool)
        {
            itm.SetChannelVolume(value);
        }
    }
    public void SetChannelVolume(string name, float value)
    {
        var tmp = GetAudioChannelOrThrow(name);
        var channel = channelPool[tmp];
        channel.SetChannelVolume(value);
    }
    public void SetChannelMute(string name, bool state)
    {
        var tmp = GetAudioChannelOrThrow(name);
        var channel = channelPool[tmp];
        channel.SetChannelMute(state ? CSCore.Win32.NativeBool.True : CSCore.Win32.NativeBool.False);
    }
    /// <summary>Returns the index of the channel. If nothing found -1 will be returned.</summary>
    public int GetAudioChannel(string name)
    {
        for (int i = 0; i < channelPool.Count; i++)
        {
            if (channelPool[i].name == name)
            {
                return i;
            }
        }

        return -1;
    }
    /// <summary>Returns the index of the channel. If nothing found -1 will be returned.</summary>
    public int GetAudioChannelOrThrow(string name)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name) || name.Equals(Environment.NewLine)) throw new Exception("Error : Name can't be empty.");

        var tmp = GetAudioChannel(name);

        if (tmp > -1)
        {
            return tmp;
        }

        throw new Exception("Channel couldn't be found.");
    }

    /// <summary>Creates new channel using the default detected by the operating system.</summary>
    public AudioMixer CreateChannel(AudioChannel.ChannelType channelType, AudioEncoding encoding, string name, DataFlow dataflow, Role role)
    {
        var au = new AudioChannel(channelType, sampleRate, bits, channel, encoding, name, dataflow, role, latency);
        return this;
    }
    /// <summary>Create channel with custom device. (get the devices 1st via GetAudioDevices)</summary>
    /// <param name="channelType">Channel type.</param>
    /// <param name="sampleRate"></param>
    /// <param name="bits">Audio bit.</param>
    /// <param name="channels">Audio channel.</param>
    /// <param name="name">Channel name.</param>
    /// <param name="device">Get the MMDevice via GetAudioDevices.</param>
    public AudioMixer CreateChannelWithDevice(AudioChannel.ChannelType channelType, int sampleRate, int bits, int channels, AudioEncoding encoding, string name, MMDevice device)
    {
        var au = new AudioChannel(channelType, sampleRate, bits, channels, encoding, name, device, latency);
        return this;
    }
    /// <summary>Getting physical audio devices found in the system.</summary>
    /// <param name="flow">DataFlow.Render = from output device e.g: as heard on the speakers. DataFlow.Capture = specific physical audio device capture.</param>
    /// <returns>The device found in the system.</returns>
    public (string friendlyName, MMDevice device)[] GetAudioDevices(DataFlow flow)
    {
        var device = MMDeviceEnumerator.EnumerateDevices(flow);
        var lis = new List<(string friendlyName, MMDevice device)>();

        foreach (var itm in device)
        {
            Console.WriteLine(itm.FriendlyName);
            lis.Add((itm.FriendlyName, itm));
        }

        return lis.ToArray();
    }
    public void SwitchToChannel(string name)
    {
        AudioChannel channel = null;

        for (int i = 0; i < channelPool.Count; i++)
        {
            if (channelPool[i].wasapiCapture != null && name.Equals(channelPool[i].name))
            {
                channel = channelPool[i];
            }
            else
            {
                channelPool[i].wasapiCapture.Stop();
            }
        }

        channel?.wasapiCapture.Start();
        return;
    }
    /// <summary>Resets the stopped channels and play them all.</summary>
    public void ResetChannelSwitch()
    {
        for (int i = 0; i < channelPool.Count; i++)
        {
            if (channelPool[i].IsMicrophone)
                channelPool[i].wasapiCapture.Start();
            else
                channelPool[i].wasapiCaptureLoopback.Start();
        }

        return;
    }
    public static void CreateMainThread(Action action) => Task.Factory.StartNew(action, TaskCreationOptions.LongRunning);
    public void Start(Process ffmpegProcess)
    {
        if (ffmpegProcess.HasExited || channelPool.Count == 0 || (doublebuffer != null && doublebuffer.IsBusy)) return;

        InitSoundMixer(ffmpegProcess, outputSampleRate);

        for (int i = 0; i < channelPool.Count; i++)
        {
            var itm = channelPool[i];
            itm.Start();
        }
    }
    public void StopCaptureAll()
    {
        Console.WriteLine("Disposing audio channels.");
        Dispose();
    }
    public static void StopAndDisposeAudioMixers()
    {
        try
        {
            foreach (var itm in mixers)
            {
                itm?.Dispose();
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception) { }
    }
    public void Stop()
    {
        foreach (var itm in channelPool)
        {
            itm?.Stop();
        }
    }
    public void Dispose()
    {
        try
        {
            if (!Disposed)
            {
                Disposed = true;

                for (int i = 0; i < channelPool.Count; i++)
                {
                    channelPool[i].Dispose();
                }
            }

            doublebuffer?.Dispose();
            mixer.Dispose();
        }
        catch (Exception) { }

    }
}

