using OpenTok;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Vonage_NDI_Receive
{
    class NDIVonageAudioCapturer : IAudioDevice
    {
        bool renderStarted = true;
        int numberOfChannels = 1;
        int sampleRate = 48000;
        private AudioDevice.AudioBus audioBus;

        public NDIVonageAudioCapturer()
        {
            
        }

        public void sendAudioBuffer(byte[] buffer)
        {
            if (audioBus == null)
                return;
            int count = (buffer.Length / 2) / numberOfChannels;
            IntPtr pointer = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, pointer, buffer.Length);
            audioBus.WriteCaptureData(pointer, count);
            Marshal.FreeHGlobal(pointer);
        }
        public void DestroyAudio()
        {
            Console.WriteLine("Destroying Audio");
            DestroyAudioCapturer();
            DestroyAudioRenderer();
            Console.WriteLine("Audio Destroyed");
        }

        public void DestroyAudioCapturer()
        {
           
        }

        public void DestroyAudioRenderer()
        {
            
        }

        public AudioDeviceSettings GetAudioCapturerSettings()
        {
            Console.WriteLine("Creating new audio capturer settings");
            AudioDeviceSettings capturerSettings = new AudioDeviceSettings();
            capturerSettings.NumChannels = numberOfChannels;
            capturerSettings.SamplingRate = sampleRate;

            Console.WriteLine($"Capturer Sampling Rate: {capturerSettings.SamplingRate} - {capturerSettings.NumChannels} ch");
            return capturerSettings;
        }

        public AudioDeviceSettings GetAudioRendererSettings()
        {

            Console.WriteLine("Creating new audio renderer settings");
            AudioDeviceSettings rendererSettings = new AudioDeviceSettings();
            rendererSettings.NumChannels = numberOfChannels;
            rendererSettings.SamplingRate = sampleRate;

            Console.WriteLine($"Renderer Sampling Rate: {rendererSettings.SamplingRate} - {rendererSettings.NumChannels} ch");
            return rendererSettings;
        }

        public int GetEstimatedAudioCaptureDelay()
        {
            return 0;
        }

        public int GetEstimatedAudioRenderDelay()
        {
            return 0;
        }

        public void InitAudio(AudioDevice.AudioBus audioBus)
        {
            this.audioBus = audioBus;
        }

        public void InitAudioCapturer()
        {
           
        }

        public void InitAudioRenderer()
        {
           
        }

        public bool IsAudioCapturerInitialized()
        {
            Console.WriteLine($"Checking Audio Capturer Initialized [{renderStarted}]");
            return renderStarted;
        }

        public bool IsAudioCapturerStarted()
        {
            Console.WriteLine($"Checking Audio Capturer Started [{renderStarted}]");
            return renderStarted;
        }

        public bool IsAudioRendererInitialized()
        {
            return false;
        }

        public bool IsAudioRendererStarted()
        {
            return false;
        }

        public void StartAudioCapturer()
        {
            
        }

        public void StartAudioRenderer()
        {
            
        }

        public void StopAudioCapturer()
        {
           
        }

        public void StopAudioRenderer()
        {
           
        }
    }
}