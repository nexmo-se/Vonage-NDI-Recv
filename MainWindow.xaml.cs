using NewTek;
using NewTek.NDI;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using OpenTok;
using System.Threading;
using System.IO;

namespace Vonage_NDI_Receive
{
    public class VonageNDI
    {
        public const string API_KEY = "46789364";
        public const string SESSION_ID = "1_MX40Njc4OTM2NH5-MTYyNTQ1NzI3MDQ4N35QNHlpekdOZE56UTF0NXp2VEwxNHFvTTd-fg";
        public const string TOKEN = "T1==cGFydG5lcl9pZD00Njc4OTM2NCZzaWc9ZTJkM2U4ZDQyYWU4NzcxMzE5ZjIzMzA1YTc2MTZjYmQzZjk2ZGIzYTpzZXNzaW9uX2lkPTFfTVg0ME5qYzRPVE0yTkg1LU1UWXlOVFExTnpJM01EUTROMzVRTkhscGVrZE9aRTU2VVRGME5YcDJWRXd4TkhGdlRUZC1mZyZjcmVhdGVfdGltZT0xNjI1NDcwMDEyJm5vbmNlPTAuOTU5NDU1MzM2OTQyODU0NyZyb2xlPXB1Ymxpc2hlciZleHBpcmVfdGltZT0xNjI1NTU2NDEyJmluaXRpYWxfbGF5b3V0X2NsYXNzX2xpc3Q9";
        NDIVonageVideoCapturer Capturer;
        NDIVonageAudioCapturer NDIAudioDevice;
        Session Session;
        Publisher Publisher;

        NDIlib.source_t src;
        private IntPtr _findInstancePtr = IntPtr.Zero;
        IntPtr _recvInstancePtr = IntPtr.Zero;
        Thread _receiveThread = null;
        bool _exitThread = false;
        
        public VonageNDI()
        {
            // Not required, but "correct". (see the SDK documentation)
            if (!NDIlib.initialize())
            {
                // Cannot run NDI. Most likely because the CPU is not sufficient (see SDK documentation).
                // you can check this directly with a call to NDIlib.is_supported_CPU()
                if (!NDIlib.is_supported_CPU())
                {
                    MessageBox.Show("CPU unsupported.");   
                }
                else
                {
                    // not sure why, but it's not going to run
                    MessageBox.Show("Cannot run NDI.");
                }
                // we can't go on
                
            }

            /* Find the first source on network */
            src = FindFirstSource();
            String name = UTF.Utf8ToString(src.p_ndi_name);
            System.Console.WriteLine("Found source:" + name);
            /* Connect to Vonage video session */
            ConnecToVideoSession();
        }

        protected void StartPublish()
        {
            NDIAudioDevice = new NDIVonageAudioCapturer();
            AudioDevice.SetCustomAudioDevice(Context.Instance,NDIAudioDevice);
            Capturer = new NDIVonageVideoCapturer();
            Publisher = new Publisher.Builder(Context.Instance)
            {
                Renderer = null,
                Capturer = Capturer
            }.Build();
            Session.Publish(Publisher);
        }
        protected void ConnecToVideoSession()
        {
            Session = new Session.Builder(Context.Instance, API_KEY, SESSION_ID).Build();
            Session.Connected += Session_Connected;
            Session.Disconnected += Session_Disconnected;
            Session.Error += Session_Error;
            Session.StreamReceived += Session_StreamReceived;
            Session.StreamDropped += Session_StreamDropped;
            Session.Connect(TOKEN);
        }

        protected void StartNDIReceiver()
        {
            NDIlib.recv_create_v3_t recvDescription = new NDIlib.recv_create_v3_t()
            {
                source_to_connect_to = src,
                color_format = NDIlib.recv_color_format_e.recv_color_format_BGRX_BGRA,
                bandwidth = NDIlib.recv_bandwidth_e.recv_bandwidth_highest,
                allow_video_fields = false,
                p_ndi_recv_name = UTF.StringToUtf8("NDIToVonage")
            };
            _recvInstancePtr = NDIlib.recv_create_v3(ref recvDescription);
            Marshal.FreeHGlobal(recvDescription.p_ndi_recv_name);

            System.Diagnostics.Debug.Assert(_recvInstancePtr != IntPtr.Zero, "Failed to create NDI receive instance.");
            if (_recvInstancePtr != IntPtr.Zero)
            {
                _receiveThread = new Thread(ReceiveThreadProc) { IsBackground = true, Name = "NdiExampleReceiveThread" };
                _receiveThread.Start();
            }

        }

        private byte[] StereoToMono(byte[] input)
        {
            byte[] output = new byte[input.Length / 2];
            int outputIndex = 0;
            for (int n = 0; n < input.Length; n += 4)
            {
                int leftChannel = BitConverter.ToInt16(input, n);
                int rightChannel = BitConverter.ToInt16(input, n + 2);
                int mixed = (leftChannel + rightChannel) / 2;
                byte[] outSample = BitConverter.GetBytes((short)mixed);

                // copy in the first 16 bit sample
                output[outputIndex++] = outSample[0];
                output[outputIndex++] = outSample[1];
            }
            return output;
        }

        void ReceiveThreadProc()
        {
            BinaryWriter binWriter = new BinaryWriter(File.Open("C:\\audio.raw", FileMode.Create));
            while (!_exitThread && _recvInstancePtr != IntPtr.Zero)
            {
                NDIlib.video_frame_v2_t videoFrame = new NDIlib.video_frame_v2_t();
                NDIlib.audio_frame_v2_t audioFrame = new NDIlib.audio_frame_v2_t();
                NDIlib.metadata_frame_t metadataFrame = new NDIlib.metadata_frame_t();

                switch (NDIlib.recv_capture_v2(_recvInstancePtr, ref videoFrame, ref audioFrame, ref metadataFrame, 1000))
                {
                    // No data
                    case NDIlib.frame_type_e.frame_type_none:
                        // No data received
                        break;
                    case NDIlib.frame_type_e.frame_type_metadata:
                        NDIlib.recv_free_metadata(_recvInstancePtr, ref metadataFrame);
                        break;
                    case NDIlib.frame_type_e.frame_type_audio:
                        //Console.WriteLine("Sample rate:" + audioFrame.sample_rate + ", Channels:" + audioFrame.no_channels + ", Number samples:" + audioFrame.no_samples);
                        if (audioFrame.p_data == IntPtr.Zero || audioFrame.no_samples == 0)
                        {
                            NDIlib.recv_free_audio_v2(_recvInstancePtr, ref audioFrame);
                            break;
                        }
                        int sizeInBytes = (int)audioFrame.no_samples * (int)audioFrame.no_channels * sizeof(float);
                        NDIlib.audio_frame_interleaved_32f_t interleavedFrame = new NDIlib.audio_frame_interleaved_32f_t()
                        {
                            sample_rate = audioFrame.sample_rate,
                            no_channels = audioFrame.no_channels,
                            no_samples = audioFrame.no_samples,
                            timecode = audioFrame.timecode
                        };
                        byte[] audBuffer = new byte[sizeInBytes];
                        GCHandle handle = GCHandle.Alloc(audBuffer, GCHandleType.Pinned);
                        interleavedFrame.p_data = handle.AddrOfPinnedObject();
                        NDIlib.util_audio_to_interleaved_32f_v2(ref audioFrame, ref interleavedFrame);
                        handle.Free();
                        /* convert 32-bit stereo to 16-bit mono */
                        byte[] newArray16Bit = new byte[sizeInBytes / 2];
                        short two;
                        float value;
                        for (int i = 0, j = 0,k=0; i < sizeInBytes; i += 4, j += 2,k+= 1)
                        {
                            value = (BitConverter.ToSingle(audBuffer, i));
                            two = (short)(value * short.MaxValue);

                            newArray16Bit[j] = (byte)(two & 0xFF);
                            newArray16Bit[j + 1] = (byte)((two >> 8) & 0xFF);
                        }

                        byte[] monoBuffer = StereoToMono(newArray16Bit);
                        NDIAudioDevice.sendAudioBuffer(monoBuffer);
                        binWriter.Write(monoBuffer);
                        NDIlib.recv_free_audio_v2(_recvInstancePtr,ref audioFrame);
                        break;
                    case NDIlib.frame_type_e.frame_type_video:
                        if (videoFrame.p_data == IntPtr.Zero)
                        {
                            NDIlib.recv_free_video_v2(_recvInstancePtr, ref videoFrame);
                            break;                        
                        }

                        int yres = (int)videoFrame.yres;
                        int xres = (int)videoFrame.xres;

                        //Console.WriteLine("X:" + xres + ", Y:" + yres);
                        //Console.WriteLine("Framerte:" + (videoFrame.frame_rate_N / videoFrame.frame_rate_D));
                        Capturer.sendFrameBuffer(xres, yres, OpenTok.PixelFormat.FormatArgb32, videoFrame.p_data);
                        NDIlib.recv_free_video_v2(_recvInstancePtr, ref videoFrame);
                        break;
                }
            }
        }
       
        private void Session_Connected(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("Session Connected");
                StartPublish();
                StartNDIReceiver();
            }
            catch (OpenTokException ex)
            {
                Console.WriteLine("OpenTokException " + ex.ToString());
            }
        }

        private void Session_Disconnected(object sender, EventArgs e)
        {
            Console.WriteLine("Session disconnected");
        }

        private void Session_Error(object sender, Session.ErrorEventArgs e)
        {
            MessageBox.Show("Session error:" + e.ErrorCode, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Session_StreamReceived(object sender, Session.StreamEventArgs e)
        {
        }
        private void Session_StreamDropped(object sender, Session.StreamEventArgs e)
        {
        }
            protected NDIlib.source_t FindFirstSource()
        {
            IntPtr extraIpsPtr = IntPtr.Zero;
            IntPtr groupsNamePtr = IntPtr.Zero;
            bool showLocalSources = true;

            NDIlib.find_create_t findDesc = new NDIlib.find_create_t()
            {
                p_groups = groupsNamePtr,
                show_local_sources = showLocalSources,
                p_extra_ips = extraIpsPtr

            };

            _findInstancePtr = NDIlib.find_create_v2(ref findDesc);

            int SourceSizeInBytes = Marshal.SizeOf(typeof(NDIlib.source_t));
            bool foundSource = false;

            while (!foundSource)
            {
                if (NDIlib.find_wait_for_sources(_findInstancePtr, 500))
                {
                    uint NumSources = 0;
                    IntPtr SourcesPtr = NDIlib.find_get_current_sources(_findInstancePtr, ref NumSources);
                    if(NumSources > 0)
                    {
                        IntPtr p = IntPtr.Add(SourcesPtr, (0 * SourceSizeInBytes)); // first in the list 0
                        NDIlib.source_t src = (NDIlib.source_t)Marshal.PtrToStructure(p, typeof(NDIlib.source_t));
                        return src;
                    }
                }
            }
            NDIlib.source_t nullsrc = (NDIlib.source_t)Marshal.PtrToStructure(IntPtr.Zero, typeof(NDIlib.source_t));
            return nullsrc;
        }
    }
}
