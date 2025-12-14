
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using CSCore;
using CSCore.Codecs.RAW;
using CSCore.Codecs.WAV;
using CSCore.MediaFoundation;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;

namespace AmixSharp;

/// <summary>A doublebuffer class. Utilizing a silenced wasapiout as channel loopback.</summary>
public class DoubleBufferOut : IDisposable
{
    private readonly SubMixer _mixer;
    private readonly NotificationSource _notificationSource;
    private readonly GainSource _silencedMixerOutput;
    public event EventHandler<DataAvailableEventArgs> DataAvailable;
    public bool IsBusy => wasapiOut != null && wasapiOut.PlaybackState != PlaybackState.Stopped;
    private WasapiOut wasapiOut;
    public int sampleRate { get; private set; }
    public int bits { get; private set; }
    public int channel { get; private set; }
    private byte[] byteBuffer = Array.Empty<byte>();
    private short[] shortBuffer = Array.Empty<short>();
    const float absolute16Bit = 32767.0f;
    public bool IsPaused => wasapiOut.PlaybackState == PlaybackState.Paused;
    public bool IsPlaying => wasapiOut.PlaybackState == PlaybackState.Playing;
    public bool IsStopped => wasapiOut.PlaybackState == PlaybackState.Stopped;
    public int outputSampleRate { get; private set; }
    public DoubleBufferOut(SubMixer mixer, int sampleRate, int bits, int channel, int outputSampleRate)
    {
        this.outputSampleRate = outputSampleRate;
        this.sampleRate = sampleRate;
        this.bits = bits;
        this.channel = channel;
        _mixer = mixer;
        _notificationSource = new NotificationSource(_mixer);

        if (outputSampleRate == 44100)
        {
            _notificationSource.BlockRead += OnBlockRead44100;
        }
        else
        {
            _notificationSource.BlockRead += OnBlockRead48000;
        }

        _silencedMixerOutput = new GainSource(_notificationSource);
        _silencedMixerOutput.Volume = 0.0f;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnBlockReadScalar48000HZ(object sender, BlockReadEventArgs<float> e)
    {
        try
        {
            int sampleCount = e.Data.Length;
            const int BytesPerSample = 2;
            int requiredByteCount = sampleCount * BytesPerSample;

            if (sampleCount > shortBuffer.Length)
            {
                shortBuffer = new short[sampleCount];
            }

            if (requiredByteCount > byteBuffer.Length)
            {
                byteBuffer = new byte[requiredByteCount];
            }

            int i = 0;
            int limit = sampleCount & ~3;

            for (; i < limit; i += 4)
            {
                float s0 = e.Data[i];
                float s1 = e.Data[i + 1];
                float s2 = e.Data[i + 2];
                float s3 = e.Data[i + 3];

                s0 = float.Clamp(s0, -1.0f, 1.0f);
                s1 = float.Clamp(s1, -1.0f, 1.0f);
                s2 = float.Clamp(s2, -1.0f, 1.0f);
                s3 = float.Clamp(s3, -1.0f, 1.0f);

                shortBuffer[i] = (short)(s0 * absolute16Bit);
                shortBuffer[i + 1] = (short)(s1 * absolute16Bit);
                shortBuffer[i + 2] = (short)(s2 * absolute16Bit);
                shortBuffer[i + 3] = (short)(s3 * absolute16Bit);
            }

            for (; i < sampleCount; i++)
            {
                float s = e.Data[i];
                s = float.Clamp(s, -1.0f, 1.0f);
                shortBuffer[i] = (short)(s * absolute16Bit);
            }

            Buffer.BlockCopy(
                shortBuffer,
                0,
                byteBuffer,
                0,
                requiredByteCount
            );

            DataAvailable?.Invoke(this,

                new DataAvailableEventArgs(
                    data: byteBuffer,
                    offset: 0,
                    bytecount: requiredByteCount,
                    format: new WaveFormat(sampleRate, 16, channel, AudioEncoding.Pcm)
                )
            );
        }
        catch (Exception)
        {
            Console.WriteLine("Error : wav conversion failed.");
        }
    }
    private void OnBlockReadScalar44100HZ(object sender, BlockReadEventArgs<float> e)
    {
        // New sample rate constant as requested
        const int NewSampleRate = 44100;
        const int BitsPerSample = 16;
        const int ChannelCount = 2;

        try
        {
            int sampleCount = e.Data.Length;
            const int BytesPerSample = BitsPerSample / 8;
            int requiredByteCount = sampleCount * BytesPerSample;

            if (sampleCount > shortBuffer.Length)
            {
                shortBuffer = new short[sampleCount];
            }

            if (requiredByteCount > byteBuffer.Length)
            {
                byteBuffer = new byte[requiredByteCount];
            }

            int i = 0;
            int limit = sampleCount & ~3;

            for (; i < limit; i += 4)
            {
                float s0 = e.Data[i];
                float s1 = e.Data[i + 1];
                float s2 = e.Data[i + 2];
                float s3 = e.Data[i + 3];

                s0 = float.Clamp(s0, -1.0f, 1.0f);
                s1 = float.Clamp(s1, -1.0f, 1.0f);
                s2 = float.Clamp(s2, -1.0f, 1.0f);
                s3 = float.Clamp(s3, -1.0f, 1.0f);

                shortBuffer[i] = (short)(s0 * absolute16Bit);
                shortBuffer[i + 1] = (short)(s1 * absolute16Bit);
                shortBuffer[i + 2] = (short)(s2 * absolute16Bit);
                shortBuffer[i + 3] = (short)(s3 * absolute16Bit);
            }

            for (; i < sampleCount; i++)
            {
                float s = e.Data[i];
                s = float.Clamp(s, -1.0f, 1.0f);
                shortBuffer[i] = (short)(s * absolute16Bit);
            }

            Buffer.BlockCopy(
                shortBuffer,
                0,
                byteBuffer,
                0,
                requiredByteCount
            );

            DataAvailable?.Invoke(this,
                new DataAvailableEventArgs(
                    data: byteBuffer,
                    offset: 0,
                    bytecount: requiredByteCount,
                    format: new WaveFormat(NewSampleRate, BitsPerSample, ChannelCount, AudioEncoding.Pcm)
                )
            );
        }
        catch (Exception)
        {
            Console.WriteLine("Error : wav conversion failed.");
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnBlockRead48000(object sender, BlockReadEventArgs<float> e)
    {
        const int NewSampleRate = 48000;
        const int BitsPerSample = 16;
        const int ChannelCount = 2;

        if (!Vector128.IsHardwareAccelerated)
        {
            OnBlockReadScalar48000HZ(sender, e);
            return;
        }

        try
        {
            int sampleCount = e.Data.Length;
            const int BytesPerSample = BitsPerSample / 8;
            int requiredByteCount = sampleCount * BytesPerSample;

            if (sampleCount > shortBuffer.Length)
            {
                shortBuffer = new short[sampleCount];
            }

            if (requiredByteCount > byteBuffer.Length)
            {
                byteBuffer = new byte[requiredByteCount];
            }

            const int VectorSize = 4;
            const int one = 1;
            int limit = sampleCount & ~(VectorSize - one);
            var scaleVector = Vector128.Create(absolute16Bit);
            var minVector = Vector128.Create(-one);
            var maxVector = Vector128.Create(one);

            int i = 0;

            for (; i < limit; i += VectorSize)
            {
                ref var sbA = ref e.Data[i];
                Vector128<float> samples = Vector128.LoadUnsafe(ref sbA);
                samples = Vector128.Max(samples, Vector128.ConvertToSingle(minVector));
                samples = Vector128.Min(samples, Vector128.ConvertToSingle(maxVector));
                samples = Vector128.Multiply(samples, scaleVector);
                Vector128<int> intSamples = Vector128.ConvertToInt32(samples);
                Vector128<short> shortSamples = Vector128.Narrow(intSamples, intSamples);
                ref var sbB = ref shortBuffer[i];
                shortSamples.StoreUnsafe(ref sbB);
            }

            for (; i < sampleCount; i++)
            {
                float s = e.Data[i];
                s = float.Clamp(s, -one, one);
                shortBuffer[i] = (short)(s * absolute16Bit);
            }

            Buffer.BlockCopy(
                shortBuffer,
                0,
                byteBuffer,
                0,
                requiredByteCount
            );

            DataAvailable?.Invoke(this,
                new DataAvailableEventArgs(
                    data: byteBuffer,
                    offset: 0,
                    bytecount: requiredByteCount,
                    format: new WaveFormat(NewSampleRate, BitsPerSample, ChannelCount, AudioEncoding.Pcm)
                )
            );
        }
        catch (Exception)
        {
            Console.WriteLine("Error : wav conversion failed.");
        }
    }
    public static byte[] ConvertFloatArrayToAacByteArray(
    float[] inputPcmFloatData,
    int sampleRate,
    int channels,
    int bitrate = 128000)
    {
        var initialInputFormat = new WaveFormat(sampleRate, 32, channels, AudioEncoding.IeeeFloat);
        const int TargetBitsPerSample = 16;

        int byteCount = inputPcmFloatData.Length * sizeof(float);
        byte[] inputBytes = new byte[byteCount];
        Buffer.BlockCopy(inputPcmFloatData, 0, inputBytes, 0, byteCount);

        using (var inputStream = new MemoryStream(inputBytes, 0, byteCount, false, false)) // Explicit size and non-resizable
        using (var rawSource = new RawDataReader(inputStream, initialInputFormat))
        {
            ISampleSource sampleSource = rawSource.ToSampleSource();
            IWaveSource pcmSource = sampleSource.ToWaveSource(TargetBitsPerSample);

            using (var outputStream = new MemoryStream())
            {
                using (var encoder = MediaFoundationEncoder.CreateAACEncoder(
                    pcmSource.WaveFormat,
                    outputStream,
                    bitrate
                ))
                {
                    MediaFoundationEncoder.EncodeWholeSource(encoder, pcmSource);
                }

                return outputStream.ToArray();
            }
        }
    }
    public static void ConvertWavToAac(byte[] bytes, string outputAacFilePath, int bitrate = 128000)
    {
        using (MemoryStream stream = new MemoryStream(bytes))
        {
            stream.Position = 0;

            using (var sourceStream = new WaveFileReader(stream))
            {
                sourceStream.Position = 0;
                IWaveSource waveSource = sourceStream;

                using (var outputStream = File.Create(outputAacFilePath))
                using (var encoder = MediaFoundationEncoder.CreateAACEncoder(
                    waveSource.WaveFormat, // The PCM format of the input data
                    outputStream,          // The destination file stream
                    bitrate                // The target AAC bitrate (e.g., 128 kbps)
                ))
                {
                    MediaFoundationEncoder.EncodeWholeSource(encoder, waveSource);
                }
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnBlockRead44100(object sender, BlockReadEventArgs<float> e)
    {
        const int NewSampleRate = 44100;
        const int BitsPerSample = 16;
        const int ChannelCount = 2;

        if (!Vector128.IsHardwareAccelerated)
        {
            OnBlockReadScalar44100HZ(sender, e);
            return;
        }

        try
        {
            int sampleCount = e.Data.Length;

            const int BytesPerSample = BitsPerSample / 8;
            int requiredByteCount = sampleCount * BytesPerSample;

            if (sampleCount > shortBuffer.Length)
            {
                shortBuffer = new short[sampleCount];
            }

            if (requiredByteCount > byteBuffer.Length)
            {
                byteBuffer = new byte[requiredByteCount];
            }

            const int VectorSize = 4;
            const int one = 1;
            int limit = sampleCount & ~(VectorSize - one);

            var scaleVector = Vector128.Create(absolute16Bit);
            var minVector = Vector128.Create(-1.0f);
            var maxVector = Vector128.Create(1.0f);

            int i = 0;

            for (; i < limit; i += VectorSize)
            {
                ref var sbA = ref e.Data[i];
                Vector128<float> samples = Vector128.LoadUnsafe(ref sbA);

                samples = Vector128.Max(samples, minVector);
                samples = Vector128.Min(samples, maxVector);
                samples = Vector128.Multiply(samples, scaleVector);

                Vector128<int> intSamples = Vector128.ConvertToInt32(samples);
                Vector128<short> shortSamples = Vector128.Narrow(intSamples, intSamples);

                ref var sbB = ref shortBuffer[i];
                shortSamples.StoreUnsafe(ref sbB);
            }

            for (; i < sampleCount; i++)
            {
                float s = e.Data[i];
                s = float.Clamp(s, -one, one);
                shortBuffer[i] = (short)(s * absolute16Bit);
            }

            Buffer.BlockCopy(
                shortBuffer,
                0,
                byteBuffer,
                0,
                requiredByteCount
            );

            // 💡 STEREO CHANGE 3: The WaveFormat now uses ChannelCount = 2
            DataAvailable?.Invoke(this,
                new DataAvailableEventArgs(
                    data: byteBuffer,
                    offset: 0,
                    bytecount: requiredByteCount,
                    format: new WaveFormat(NewSampleRate, BitsPerSample, ChannelCount, AudioEncoding.Pcm)
                )
            );
        }
        catch (Exception)
        {
            Console.WriteLine("Error : wav conversion failed.");
        }
    }
    public void AddSource(ISampleSource source)
    {
        _mixer.AddSource(source);
    }

    public void RemoveSource(ISampleSource source)
    {
        _mixer.RemoveSource(source);
    }

    public void Start()
    {
        if (IsBusy) return;

        wasapiOut = new WasapiOut();
        IWaveSource silencedWaveSource = _silencedMixerOutput.ToWaveSource();
        wasapiOut.Initialize(silencedWaveSource);
        wasapiOut.Play();

        Console.WriteLine("Mixer started silently");
    }
    public void Pause() => wasapiOut?.Pause();
    public void Volume(float vol) => wasapiOut.Volume = vol;
    public void Stop()
    {
        wasapiOut?.Stop();
    }

    public void Dispose()
    {
        try
        {
            Stop();
            wasapiOut?.Dispose();
            wasapiOut = null;
            _mixer.Dispose();

            if (outputSampleRate == 4100)
            {
                _notificationSource.BlockRead -= OnBlockRead44100;
            }
            else
            {
                _notificationSource.BlockRead -= OnBlockRead48000;
            }

            _notificationSource.Dispose();
            _silencedMixerOutput.Dispose();
        }
        catch (Exception)
        {
            Console.WriteLine("Error : May not be on the mainthread when disposing.");
        }
    }
}