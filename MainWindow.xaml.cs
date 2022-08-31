using NewTek;
using NewTek.NDI;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using OpenTok;
using System.Threading;
using System.IO;
using System.Text;
using System.Linq;

namespace Vonage_NDI_Receive
{
    public class VonageNDI
    {
        public const string API_KEY = "";
        public const string SESSION_ID = "";
        public const string TOKEN = "";
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
                    Console.WriteLine("CPU unsupported.");
                    Console.WriteLine("\nApplication Ended (press any key to contiunue)");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                else
                {
                    // not sure why, but it's not going to run
                    Console.WriteLine("Cannot run NDI.");
                    Console.WriteLine("\nApplication Ended (press any key to contiunue)");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                // we can't go on
                
            }
            src = Display_sources();
            
            //Nothing is found
            if (src.p_ndi_name == IntPtr.Zero)
            {
                Console.WriteLine("\nApplication Ended (press any key to contiunue)");
                Console.ReadKey();
                Environment.Exit(0);
            }

            //Use the chosen source
            String name = UTF.Utf8ToString(src.p_ndi_name);
            System.Console.WriteLine("Using source:" + name);
            //Connect to Vonage video session
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
                Console.WriteLine("\nApplication Ended (press any key to contiunue)");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        private void Session_Disconnected(object sender, EventArgs e)
        {
            Console.WriteLine("Session disconnected");
            Console.WriteLine("\nApplication Ended (press any key to contiunue)");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private void Session_Error(object sender, Session.ErrorEventArgs e)
        {
            Console.WriteLine("Session error:" + e.ErrorCode, "Error");
            Thread.Sleep(1000);
            Console.WriteLine("\nApplication Ended (press any key to contiunue)");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private void Session_StreamReceived(object sender, Session.StreamEventArgs e)
        {
        }
        private void Session_StreamDropped(object sender, Session.StreamEventArgs e)
        {
        }
        

        protected NDIlib.source_t Display_sources()
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
            
            bool foundSource = true;
            int SourceSizeInBytes = Marshal.SizeOf(typeof(NDIlib.source_t));
            uint NumSources = 0;
            IntPtr SourcesPtr = IntPtr.Zero;

            while (foundSource) //find sources until nothing is found (will return false when no new source is found
            {
                Console.WriteLine("Searching...\n");
                uint search_ms_timeout = 1000; //the duration each search takes in miliseconds
                foundSource = NDIlib.find_wait_for_sources(_findInstancePtr, search_ms_timeout);
                SourcesPtr = NDIlib.find_get_current_sources(_findInstancePtr, ref NumSources);
            }
            Console.WriteLine(NumSources + " NDI Source Found\n");


            if (NumSources > 0)
            {
                for (int i = 0; i < NumSources; i++)
                {
                    IntPtr p = IntPtr.Add(SourcesPtr, (i * SourceSizeInBytes)); // first in the list 0
                    NDIlib.source_t src = (NDIlib.source_t)Marshal.PtrToStructure(p, typeof(NDIlib.source_t));
                    Console.WriteLine(i+1+": "+ UTF.Utf8ToString(src.p_ndi_name));
                }
                while (true)
                {
                    Console.WriteLine("\nEnter the NDI Source number, or press <esc> to quit: ");
                    string console_input = XConsole.CancelableReadLine(out bool isCancelled);

                    if (isCancelled)
                    {
                        Console.WriteLine("\nCancelled");
                        return default;
                    }

                    int choice = 0; //default choice is zero

                    if (Int32.TryParse(console_input, out int numValue))
                    {
                        choice = numValue; //assign numValue if input is integer, use default 0 if not
                    }

                    if (choice > 0 && choice <= NumSources)
                    {
                        Console.WriteLine("Chosen: " + choice);
                        IntPtr p = IntPtr.Add(SourcesPtr, ((choice - 1) * SourceSizeInBytes)); // first in the list 0
                        NDIlib.source_t src = (NDIlib.source_t)Marshal.PtrToStructure(p, typeof(NDIlib.source_t));
                        return src;
                    }
                    else
                    {
                        Console.WriteLine("\nChosen number is not in choices");
                        continue;
                    }
                }
                

            }
            else
            {
                Console.WriteLine("No Sources Found");
                return default;
            }
        }
    }

    //Console command handler "https://stackoverflow.com/a/66495807"
    public static class XConsole
    {
        public static string CancelableReadLine(out bool isCancelled)
        {
            var cancelKey = ConsoleKey.Escape;
            var builder = new StringBuilder();
            var cki = Console.ReadKey(true);
            int index = 0;
            (int left, int top) startPosition;

            while (cki.Key != ConsoleKey.Enter && cki.Key != cancelKey)
            {
                if (cki.Key == ConsoleKey.LeftArrow)
                {
                    if (index < 1)
                    {
                        cki = Console.ReadKey(true);
                        continue;
                    }

                    LeftArrow(ref index, cki);
                }
                else if (cki.Key == ConsoleKey.RightArrow)
                {
                    if (index >= builder.Length)
                    {
                        cki = Console.ReadKey(true);
                        continue;
                    }

                    RightArrow(ref index, cki, builder);
                }
                else if (cki.Key == ConsoleKey.Backspace)
                {
                    if (index < 1)
                    {
                        cki = Console.ReadKey(true);
                        continue;
                    }

                    BackSpace(ref index, cki, builder);
                }
                else if (cki.Key == ConsoleKey.Delete)
                {
                    if (index >= builder.Length)
                    {
                        cki = Console.ReadKey(true);
                        continue;
                    }

                    Delete(ref index, cki, builder);
                }
                else if (cki.Key == ConsoleKey.Tab)
                {
                    cki = Console.ReadKey(true);
                    continue;
                }
                else
                {
                    if (cki.KeyChar == '\0')
                    {
                        cki = Console.ReadKey(true);
                        continue;
                    }

                    Default(ref index, cki, builder);
                }

                cki = Console.ReadKey(true);
            }

            if (cki.Key == cancelKey)
            {
                startPosition = GetStartPosition(index);
                ErasePrint(builder, startPosition);

                isCancelled = true;
                return string.Empty;
            }

            isCancelled = false;

            startPosition = GetStartPosition(index);
            var endPosition = GetEndPosition(startPosition.left, builder.Length);
            var left = 0;
            var top = startPosition.top + endPosition.top + 1;

            Console.SetCursorPosition(left, top);

            var value = builder.ToString();
            return value;
        }

        private static void LeftArrow(ref int index, ConsoleKeyInfo cki)
        {
            var previousIndex = index;
            index--;

            if (cki.Modifiers == ConsoleModifiers.Control)
            {
                index = 0;

                var startPosition = GetStartPosition(previousIndex);
                Console.SetCursorPosition(startPosition.left, startPosition.top);

                return;
            }

            if (Console.CursorLeft > 0)
                Console.CursorLeft--;
            else
            {
                Console.CursorTop--;
                Console.CursorLeft = Console.BufferWidth - 1;
            }
        }

        private static void RightArrow(ref int index, ConsoleKeyInfo cki, StringBuilder builder)
        {
            var previousIndex = index;
            index++;

            if (cki.Modifiers == ConsoleModifiers.Control)
            {
                index = builder.Length;

                var startPosition = GetStartPosition(previousIndex);
                var endPosition = GetEndPosition(startPosition.left, builder.Length);
                var top = startPosition.top + endPosition.top;
                var left = endPosition.left;

                Console.SetCursorPosition(left, top);

                return;
            }

            if (Console.CursorLeft < Console.BufferWidth - 1)
                Console.CursorLeft++;
            else
            {
                Console.CursorTop++;
                Console.CursorLeft = 0;
            }
        }

        private static void BackSpace(ref int index, ConsoleKeyInfo cki, StringBuilder builder)
        {
            var previousIndex = index;
            index--;

            var startPosition = GetStartPosition(previousIndex);
            ErasePrint(builder, startPosition);

            builder.Remove(index, 1);
            Console.Write(builder.ToString());

            GoBackToCurrentPosition(index, startPosition);
        }

        private static void Delete(ref int index, ConsoleKeyInfo cki, StringBuilder builder)
        {
            var startPosition = GetStartPosition(index);
            ErasePrint(builder, startPosition);

            if (cki.Modifiers == ConsoleModifiers.Control)
            {
                builder.Remove(index, builder.Length - index);
                Console.Write(builder.ToString());

                GoBackToCurrentPosition(index, startPosition);
                return;
            }

            builder.Remove(index, 1);
            Console.Write(builder.ToString());

            GoBackToCurrentPosition(index, startPosition);
        }

        private static void Default(ref int index, ConsoleKeyInfo cki, StringBuilder builder)
        {
            var previousIndex = index;
            index++;

            builder.Insert(previousIndex, cki.KeyChar);

            var startPosition = GetStartPosition(previousIndex);
            Console.SetCursorPosition(startPosition.left, startPosition.top);
            Console.Write(builder.ToString());

            GoBackToCurrentPosition(index, startPosition);
        }

        private static (int left, int top) GetStartPosition(int previousIndex)
        {
            int top;
            int left;

            if (previousIndex <= Console.CursorLeft)
            {
                top = Console.CursorTop;
                left = Console.CursorLeft - previousIndex;
            }
            else
            {
                var decrementValue = previousIndex - Console.CursorLeft;
                var rowsFromStart = decrementValue / Console.BufferWidth;
                top = Console.CursorTop - rowsFromStart;
                left = decrementValue - rowsFromStart * Console.BufferWidth;

                if (left != 0)
                {
                    top--;
                    left = Console.BufferWidth - left;
                }
            }

            return (left, top);
        }

        private static void GoBackToCurrentPosition(int index, (int left, int top) startPosition)
        {
            var rowsToGo = (index + startPosition.left) / Console.BufferWidth;
            var rowIndex = index - rowsToGo * Console.BufferWidth;

            var left = startPosition.left + rowIndex;
            var top = startPosition.top + rowsToGo;

            Console.SetCursorPosition(left, top);
        }

        private static (int left, int top) GetEndPosition(int startColumn, int builderLength)
        {
            var cursorTop = (builderLength + startColumn) / Console.BufferWidth;
            var cursorLeft = startColumn + (builderLength - cursorTop * Console.BufferWidth);

            return (cursorLeft, cursorTop);
        }

        private static void ErasePrint(StringBuilder builder, (int left, int top) startPosition)
        {
            Console.SetCursorPosition(startPosition.left, startPosition.top);
            Console.Write(new string(Enumerable.Range(0, builder.Length).Select(o => ' ').ToArray()));

            Console.SetCursorPosition(startPosition.left, startPosition.top);
        }
    }
}
