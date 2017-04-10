using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MyVideoClient
{
    public class StreamSocketListenerServer
    {
        StreamSocketListener listener;
        public String stringtemp;
        public IBuffer receiverbuf;
        public int receive_buf_flag = 0;
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
                Close();
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
                Close();
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
                        reader.ReadBytes(readByte);
                    }
                    else if (msgtype == 2)
                    {
                        stringtemp = reader.ReadString(actualStringLength);
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
                stringtemp = "Read stream failed with error: " + exception.Message;
            }
        }

        public void Close()
        {

        }
    }

    public class StreamSocketClient
    {
        StreamSocket client;
        HostName hostName;
        public String stringtemp;
        public DataWriter writer = null;
        public int flag_client_start = 0;

        public StreamSocketClient()
        {

        }
        public async Task start(string hostNameString, string servicename)
        {
            if (flag_client_start == 1) return;
            flag_client_start = 1;
            try
            {
                client = new StreamSocket();
                hostName = new HostName(hostNameString);
                await client.ConnectAsync(hostName, servicename);
                writer = new DataWriter(client.OutputStream);
                flag_client_start = 2;
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    //throw;
                }
                flag_client_start = 0;
            }
        }

        public async Task sendmsgString(String sendmsg)
        {
            if (writer == null) return;
            try
            {
                writer.WriteUInt32(writer.MeasureString(sendmsg));
                writer.WriteUInt32(2);
                writer.WriteString(sendmsg);
                await writer.StoreAsync();
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error if fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
                SocketConnectFailed();
                stringtemp = "Send failed with error: " + exception.Message;
            }
        }

        public async Task sendmsgByte(Byte[] sendmsgByte)
        {
            if (writer == null) return;
            try
            {
                writer.WriteUInt32((uint)sendmsgByte.Length);
                writer.WriteUInt32(1);
                writer.WriteBytes(sendmsgByte);
                await writer.StoreAsync();
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error if fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                {
                    throw;
                }
                SocketConnectFailed();
                stringtemp = "Send failed with error: " + exception.Message;
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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DispatcherTimer timer;
        String serverip = "10.168.177.81";
        String clientip = "10.168.176.84";
        public MainPage()
        {
            this.InitializeComponent();
            string localIpName = GetLocalIPv4();
            if (localIpName != "")
            {
                serverip = localIpName;
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;

            InitializeCameraAsync();
        }

        SoftwareBitmapSource sbSource;
        StreamSocketListenerServer streamSocketSrv;
        StreamSocketClient streamSocketClient;
        SoftwareBitmap receviebitMap;
        IBuffer buffer;

        private async void InitializeCameraAsync()
        {
            sbSource = new SoftwareBitmapSource();
            receviebitMap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 240, 180, 0);

            buffer = new Windows.Storage.Streams.Buffer((uint)(240 * 180 * 8));

            /*  stream client  close this */
            imageElement1.Source = sbSource;

            streamSocketSrv = new StreamSocketListenerServer();
            await streamSocketSrv.start(serverip, "22343");

            streamSocketClient = new StreamSocketClient();

            textBox.Text = clientip;
        }


        string GetLocalIPv4()
        {
            string ipname = "";
            var hostName_list = NetworkInformation.GetHostNames();
            foreach (var hostname in hostName_list)
            {
                if (hostname.Type == HostNameType.Ipv4)
                {
                    if (hostname.IPInformation.NetworkAdapter.IanaInterfaceType == 71)
                    {
                        return hostname.RawName;
                    }
                    else if (hostname.IPInformation.NetworkAdapter.IanaInterfaceType == 6)
                    {
                        ipname = hostname.RawName;
                    }
                }
            }
            return ipname;
        }

        int timer_tick_complete_flag = 0;
        int client_send_serverip_flag = 0;
        async private void Timer_Tick(object sender, object e)
        {
            if (timer_tick_complete_flag == 1)
            {
                return;
            }
            timer_tick_complete_flag = 1;
            /*  stream server  */
            if (streamSocketSrv.receive_buf_flag == 1)
            {
                receviebitMap.CopyFromBuffer(streamSocketSrv.receiverbuf);
                await sbSource.SetBitmapAsync(receviebitMap);
                streamSocketSrv.receive_buf_flag = 0;
                client_send_serverip_flag = 0;
            }

            if (streamSocketClient.flag_client_start == 0)
            {
                await streamSocketClient.start(clientip, "22333");
                client_send_serverip_flag = 1;
            }
            else
            {
                if (client_send_serverip_flag == 1)
                {
                    await streamSocketClient.sendmsgString(serverip);
                }
            }
            timer_tick_complete_flag = 0;
        }



        async private void button_Click(object sender, RoutedEventArgs e)
        {
            clientip = textBox.Text;
            await streamSocketClient.start(clientip, "22333");
            client_send_serverip_flag = 1;
            timer.Start();
        }

    }
}
