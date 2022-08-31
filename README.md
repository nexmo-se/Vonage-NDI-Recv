# Vonage-NDI-Recv
Receive NDI Video/Audio and publish to Vonage Video session (selectable NDI source)

## Prerequisites

- NDI 5 SDK for Windows - https://www.ndi.tv/sdk/
- NDI Tools. I used the **NDI Scan Converter** to act as a NDI Sender - https://ndi.tv/tools/
- Vonage Video Account
- Vonage Video SDK for Windows (OpenTok.Client v2.23.2)

### NDI Setup

- Unpack the Windows SDK
- Build the NDI .NET Lib Project - C:\Program Files\NewTek\NDI 5 SDK\Examples\C#\NDILibDotNet2

## How to Run the sample

- This sample is a WPF application but built to run as Console Application with no UI
- Goto Vonage Video Playground and get Session ID, Token and ApiKey
- In MainWindow.xaml.cs provide the credentials you got in previous step
- Now start the **NDI Scan Converter** or any other NDI source. You can have Multiple NDI sources
- Compile and run the application
- Application scans the network for NDI sources.
- You are then given a choice of which NDI source to connect
- Application will then use the chosen NDI source to connect to the video session and start publishing video / audio received.

## NOTE

- Since this is a sample there are some assumptions
- Audio received is assumed to be 32-bit, 48000Hz, Stereo. Application converts this to 16-bit, 48000Hz, Mono before sending to Video session.
- Vonage supports maximum resolution of 720p. But some NDI sources may send 2K, 4K quality. This will use unnecessary CPU/Memory for scaling down the video. If possible send max 720p quality
- You can have multiple instance of this application for multiple NDI in
