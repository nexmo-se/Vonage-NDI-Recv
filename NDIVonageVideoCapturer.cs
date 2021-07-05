using System;
using System.Drawing;
using OpenTok;

namespace Vonage_NDI_Receive
{
    public class NDIVonageVideoCapturer : OpenTok.IVideoCapturer
    {
        IVideoFrameConsumer frameConsumer;
        const int FPS = 30;
        int width;
        int height;
        public void Init(IVideoFrameConsumer frameConsumer)
        {
            this.frameConsumer = frameConsumer;
        }

        public void Start()
        {
            
        }

        public void Stop()
        {
        }

        public void Destroy()
        {
            

        }

        public void sendFrame(Bitmap image)
        {
            VideoFrame frame = VideoFrame.CreateYuv420pFrameFromBitmap(image);
            frameConsumer.Consume(frame);
            image.Dispose();
        }

        public void sendFrameBuffer(int width, int height, PixelFormat format, IntPtr buffer)
        {
            VideoFrame frame = VideoFrame.CreateFrameFromBuffer(format,width,height,buffer);
            frameConsumer.Consume(frame);
        }
        public VideoCaptureSettings GetCaptureSettings()
        {
            VideoCaptureSettings settings = new VideoCaptureSettings();
            /*settings.Width = width;
            settings.Height = height;*/
            settings.Fps = FPS;
            settings.MirrorOnLocalRender = false;
            settings.PixelFormat = PixelFormat.FormatYuv420p;
            return settings;
        }
    }
}
