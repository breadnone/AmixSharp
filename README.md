# AmixSharp

Wasapi AudioMixer for Ffmpeg in C#  

1. Uses CScore library for the audio  
2. Double buffering via WasapiOut for the mixing  
3. Output buffer is 16bit raw pcm (the pipe input on the ffmpeg side must use compatible codec e.g: -f s16le -acodec pcm_s16le -ac 2 -ar 44100 -i pipe:0)
4. Uses intrinsics for the conversion from raw float32 buffer to byte[]..
