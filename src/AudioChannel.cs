
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;

namespace AmixSharp;

public class AudioChannel : IDisposable
{
    public readonly int sampleRate;
    public readonly int bits;
    public readonly int channel;
    public readonly string name;
    public WasapiCapture wasapiCapture { get; private set; }
    public WasapiLoopbackCapture wasapiCaptureLoopback { get; private set; }
    public readonly Guid guid;
    public static readonly List<AudioChannel> channels = new List<AudioChannel>();
    public bool WasDisposed { get; private set; }
    public bool IsMicrophone { get; private set; }
    public SoundInSource channelSource;
    public SimpleAudioVolume volumeSource;
    public enum ChannelType
    {
        Audio = 0,
        Microphone = 1
    }
    public AudioChannel(ChannelType channelType, int sampleRate, int bits, int channel, AudioEncoding encoding, string name, DataFlow dataflow, Role role, int latency)
    {
        if (channels.Exists(x => x.name == name))
        {
            throw new Exception("Error : Duplicate device or channel names.");
        }

        this.sampleRate = sampleRate;
        this.bits = bits;
        this.channel = channel;
        this.name = name;

        guid = Guid.NewGuid();
        var device = MMDeviceEnumerator.DefaultAudioEndpoint(channelType == ChannelType.Audio ? dataflow : DataFlow.Capture, role);
        if (device == null) throw new Exception("Device cant be found in the system.");

        var waveFormat = new WaveFormat(sampleRate, bits, channel, encoding);
        IsMicrophone = channelType == ChannelType.Audio ? false : true;

        if (IsMicrophone)
        {
            wasapiCapture = new WasapiCapture(true, AudioClientShareMode.Shared, latency, waveFormat, ThreadPriority.AboveNormal);
            wasapiCapture.Device = device;
            wasapiCapture.Initialize();
            volumeSource = new SimpleAudioVolume(wasapiCapture.Device.BasePtr);
            //NOTE : FillWithZeros is important to keep the Mic owennership alive even tho its shared.
            channelSource = new SoundInSource(wasapiCapture) { FillWithZeros = true };
        }
        else
        {
            wasapiCaptureLoopback = new WasapiLoopbackCapture(latency, waveFormat, ThreadPriority.AboveNormal);
            wasapiCaptureLoopback.Device = device;
            wasapiCaptureLoopback.Initialize();
            volumeSource = new SimpleAudioVolume(wasapiCaptureLoopback.Device.BasePtr);
            channelSource = new SoundInSource(wasapiCaptureLoopback) { FillWithZeros = true };
        }

        channels.Add(this);
    }

    public AudioChannel(ChannelType channelType, int sampleRate, int bits, int channel, AudioEncoding encoding, string name, MMDevice device, int latency)
    {
        if (channels.Exists(x => x.name == name))
        {
            throw new Exception("Error : Duplicate device or channel names.");
        }

        this.sampleRate = sampleRate;
        this.bits = bits;
        this.channel = channel;
        this.name = name;
        guid = Guid.NewGuid();

        if (device == null) throw new Exception("Device cant be found in the system.");

        var waveFormat = new WaveFormat(sampleRate, bits, channel, encoding);
        IsMicrophone = channelType == ChannelType.Audio ? false : true;

        if (IsMicrophone)
        {
            wasapiCapture = new WasapiCapture(true, AudioClientShareMode.Shared, latency, waveFormat, ThreadPriority.AboveNormal);
            wasapiCapture.Device = device;
            wasapiCapture.Initialize();

            volumeSource = new SimpleAudioVolume(wasapiCapture.Device.BasePtr);
            channelSource = new SoundInSource(wasapiCapture) { FillWithZeros = true };
        }
        else
        {
            wasapiCaptureLoopback = new WasapiLoopbackCapture(latency, waveFormat, ThreadPriority.AboveNormal);
            wasapiCaptureLoopback.Device = device;
            wasapiCaptureLoopback.Initialize();

            volumeSource = new SimpleAudioVolume(wasapiCaptureLoopback.Device.BasePtr);
            channelSource = new SoundInSource(wasapiCaptureLoopback) { FillWithZeros = true };
        }

        channels.Add(this);
    }
    public async void PlaySingleAudio(Stream stream)
    {
        if (IsMicrophone) throw new Exception("Audio cant be played on a microphone channel.");

        await Task.Run(async () =>
        {
            stream.Position = 0;
            var waveStream = new WaveFileReader(stream);
            waveStream.Position = 0;

            using (var wasapiOut = new WasapiOut())
            {
                wasapiOut.Initialize(waveStream);
                wasapiOut.Play();

                // Block until playback is complete
                while (wasapiOut.PlaybackState == PlaybackState.Playing || wasapiOut.PlaybackState == PlaybackState.Paused)
                {
                    Thread.Sleep(20);
                }

                wasapiOut.Stop();
            }
        });
    }
    public async void PlaySingleAudio(byte[] buffer)
    {
        if (IsMicrophone) throw new Exception("Audio cant be played on a microphone channel.");

        var stream = new MemoryStream(buffer);
        PlaySingleAudio(stream);
    }
    public void SetChannelVolume(float value)
    {
        volumeSource.SetMasterVolumeNative(value, guid);
    }
    public void SetChannelMute(bool state)
    {
        volumeSource.SetMuteNative(state ? CSCore.Win32.NativeBool.True : CSCore.Win32.NativeBool.False, guid);
    }
    public void Stop()
    {
        wasapiCapture?.Stop();
        wasapiCaptureLoopback?.Stop();
    }
    public void Start()
    {
        wasapiCapture?.Start();
        wasapiCaptureLoopback?.Start();
    }
    public void Dispose()
    {
        if (!WasDisposed)
        {
            WasDisposed = true;

            AudioAPI.Invoke(async () =>
            {
                try
                {
                    wasapiCapture?.Stop();
                    wasapiCaptureLoopback?.Stop();
                    wasapiCaptureLoopback?.Dispose();
                    channelSource?.Dispose();
                    volumeSource?.Dispose();
                    wasapiCapture?.Dispose();
                }
                catch (Exception) { }
            });
        }
    }
}