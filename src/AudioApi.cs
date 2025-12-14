
namespace AmixSharp;

public static class AudioAPI
{
    static SynchronizationContext audioSyncContext;
    public static void CaptureAudioContext()
    {
        if (audioSyncContext == null)
            audioSyncContext = SynchronizationContext.Current;
    }
    /// <summary>Creates new AudioMixer class.</summary>
    /// <param name="outputSampleRate">The options are : "44100" and "48000"</param>
    public static AudioMixer CreateMixer(int sampleRate, int bits, int channel, int outputSampleRate) => new AudioMixer(sampleRate, bits, channel, outputSampleRate);
    public static void Stop<T>(this T mixer) where T : AudioMixer => mixer.Stop();
    public static void Start<T>(this T mixer) where T : AudioMixer => mixer.Start();
    public static T CreateChannel<T>(this T mixer, AudioChannel.ChannelType type, int sampleRate, int bits, int channel) where T
    : AudioMixer => mixer.CreateChannel(type, sampleRate, bits, channel);
    public static void Invoke(Action action)
    {
        audioSyncContext.Post(
            state =>
            {
                // This code block now executes on Thread B
                action?.Invoke();
            },
            null); // state is usually null for simple Actions
    }
    public static void InvokeSync(Action action)
    {
        audioSyncContext.Send(
            state =>
            {
                // This code block now executes on Thread B
                action?.Invoke();
            },
            null); // state is usually null for simple Actions
    }
}