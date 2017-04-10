using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MyVideoServer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            InitializeCameraAsync();
            InitSocket();
        }

        MediaCapture MyMediaCapture;
        VideoFrame videoFrame;
        VideoFrame previewFrame;
        IBuffer buffer;

        DispatcherTimer timer;
        StreamSocketListenerServer streamSocketSrv;
        StreamSocketClient streamSocketClient;

        private async void InitializeCameraAsync()
        {
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            DeviceInformation cameraDevice = allVideoDevices.FirstOrDefault();
            var mediaInitSettings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };
            MyMediaCapture = new MediaCapture();

            try
            {
                await MyMediaCapture.InitializeAsync(mediaInitSettings);
            }
            catch (UnauthorizedAccessException)
            {

            }

            PreviewControl.Height = 180;
            PreviewControl.Width = 240;
            PreviewControl.Source = MyMediaCapture;

            await MyMediaCapture.StartPreviewAsync();
            videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, 240, 180, 0);
            buffer = new Windows.Storage.Streams.Buffer((uint)(240 * 180 * 8));
        }


        int timer_tick_complete_flag = 0;
        async private void Timer_Tick(object sender, object e)
        {
            if (timer_tick_complete_flag == 1)
            {
                return;
            }
            timer_tick_complete_flag = 1;

            /*  stream client */
            try
            {
                if (streamSocketClient.flag_client_start == 0)
                {
                    if (streamSocketSrv.receive_client_ip == 1)
                    {
                        await streamSocketClient.start(streamSocketSrv.stringtemp, "22343");
                    }
                }
                else
                {

                    if (MyMediaCapture.CameraStreamState == CameraStreamState.Streaming)
                    {
                        previewFrame = await MyMediaCapture.GetPreviewFrameAsync(videoFrame);
                        previewFrame.SoftwareBitmap.CopyToBuffer(buffer);
                        await streamSocketClient.sendBuffer(buffer);
                    }

                }
            }
            catch (Exception)
            {

            }

            //if (streamSocketSrv.receive_byte_flag == 1)
            //{
            //    if (streamSocketSrv.readByte.Length == 5)
            //    {

            //        if ((streamSocketSrv.readByte[0] == 0xff) && (streamSocketSrv.readByte[4] == 0xff))
            //        {
            //            //SendcommandtoTTL(streamSocketSrv.readByte);
            //        }
            //    }
            //    streamSocketSrv.receive_byte_flag = 0;
            //}
            timer_tick_complete_flag = 0;
        }

        private async void InitSocket()
        {
            streamSocketSrv = new StreamSocketListenerServer();
            await streamSocketSrv.start("22333");

            streamSocketClient = new StreamSocketClient();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            timer.Start();
        }
    }


    public class StreamSocketListenerServer
    {
        StreamSocketListener listener;
        public String stringtemp;
        public IBuffer receiverbuf;
        public int receive_buf_flag = 0;
        public int receive_byte_flag = 0;
        public int receive_client_ip = 0;
        public byte[] readByte;

        public StreamSocketListenerServer()
        {
            listener = new StreamSocketListener();
            listener.ConnectionReceived += OnConnection;
        }
        public async Task start(string servicename)
        {
            try
            {
                await listener.BindServiceNameAsync(servicename);
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
            }
        }

        public async Task start(string hostname, string servicename)
        {
            try
            {
                HostName hostName = new HostName(hostname);
                await listener.BindEndpointAsync(hostName, servicename);
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
            }
        }
        private async void OnConnection(
            StreamSocketListener sender,
            StreamSocketListenerConnectionReceivedEventArgs args)
        {
            DataReader reader = new DataReader(args.Socket.InputStream);
            try
            {
                while (true)
                {
                    // Read first 4 bytes (length of the subsequent string).
                    uint sizeFieldCount = await reader.LoadAsync(sizeof(uint));
                    if (sizeFieldCount != sizeof(uint))
                    {
                        // The underlying socket was closed before we were able to read the whole data.
                        return;
                    }

                    uint sizeFieldCount1 = await reader.LoadAsync(sizeof(uint));
                    if (sizeFieldCount1 != sizeof(uint))
                    {
                        // The underlying socket was closed before we were able to read the whole data.
                        return;
                    }

                    // Read the string.
                    uint stringLength = reader.ReadUInt32();
                    uint msgtype = reader.ReadUInt32();
                    uint actualStringLength = await reader.LoadAsync(stringLength);
                    if (stringLength != actualStringLength)
                    {
                        // The underlying socket was closed before we were able to read the whole data.
                        return;
                    }

                    // Display the string on the screen. The event is invoked on a non-UI thread, so we need to marshal
                    // the text back to the UI thread.

                    if (msgtype == 1)
                    {
                        readByte = new byte[actualStringLength];
                        reader.ReadBytes(readByte);
                        receive_byte_flag = 1;
                    }
                    else if (msgtype == 2)
                    {
                        stringtemp = reader.ReadString(actualStringLength);
                        receive_client_ip = 1;
                    }
                    else if (msgtype == 3)
                    {
                        receiverbuf = reader.ReadBuffer(actualStringLength);
                        receive_buf_flag = 1;
                    }
                }
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
            }
        }
    }


    public class StreamSocketClient
    {
        StreamSocket client;
        HostName hostName;
        public DataWriter writer = null;
        public int flag_client_start = 0;

        public StreamSocketClient()
        {

        }
        public async Task start(string hostNameString, string servicename)
        {
            if (flag_client_start == 1)
            {
                return;
            }

            flag_client_start = 1;
            try
            {
                client = new StreamSocket();
                hostName = new HostName(hostNameString);
                await client.ConnectAsync(hostName, servicename);
                writer = new DataWriter(client.OutputStream);
                flag_client_start = 2;
            }
            catch (Exception)
            {
                flag_client_start = 0;
            }
        }


        public async Task sendmsgString(String sendmsg)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.WriteUInt32(writer.MeasureString(sendmsg));
                writer.WriteUInt32(2);
                writer.WriteString(sendmsg);
                await writer.StoreAsync();
            }
            catch (Exception)
            {
                // If this is an unknown status it means that the error if fatal and retry will likely fail.
                SocketConnectFailed();
            }
        }

        public async Task sendmsgByte(Byte[] sendmsgByte)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.WriteUInt32((uint)sendmsgByte.Length);
                writer.WriteUInt32(1);
                writer.WriteBytes(sendmsgByte);
                await writer.StoreAsync();
            }
            catch (Exception)
            {
                SocketConnectFailed();
            }
        }

        public async Task sendBuffer(IBuffer sendmsgbuffer)
        {
            if (writer == null)
            {
                return;
            }
            try
            {
                writer.WriteUInt32(sendmsgbuffer.Length);
                writer.WriteUInt32(3);
                writer.WriteBuffer(sendmsgbuffer);
                await writer.StoreAsync();
            }
            catch (Exception)
            {
                SocketConnectFailed();
            }
        }

        public void SocketConnectFailed()
        {
            writer.Dispose();
            writer = null;
            client.Dispose();
            client = null;
            flag_client_start = 0;
        }
    }


}
