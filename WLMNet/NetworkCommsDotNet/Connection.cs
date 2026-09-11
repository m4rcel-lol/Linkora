using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetworkCommsDotNet.DPSBase;

namespace NetworkCommsDotNet
{
    /// <summary>Metadata that accompanies an incoming packet.</summary>
    public class PacketHeader
    {
        public string PacketType { get; private set; }
        public int PayloadPacketSize { get; private set; }

        public PacketHeader(string packetType, int payloadPacketSize)
        {
            PacketType = packetType;
            PayloadPacketSize = payloadPacketSize;
        }
    }

    /// <summary>
    /// Identifies a connection endpoint. For outgoing connections this is the address and port the
    /// client dials; for incoming ones it describes the remote peer.
    /// </summary>
    public class ConnectionInfo
    {
        public IPEndPoint RemoteEndPoint { get; private set; }
        public IPEndPoint LocalEndPoint { get; private set; }

        /// <summary>Set once a connection has been created for this info, so lookups can reuse it.</summary>
        internal Connections.Connection AttachedConnection { get; set; }

        public ConnectionInfo(string address, int port)
        {
            IPAddress parsed;
            if (!IPAddress.TryParse(address, out parsed))
            {
                IPAddress[] resolved = Dns.GetHostAddresses(address);
                if (resolved.Length == 0)
                {
                    throw new ArgumentException("Unable to resolve address '" + address + "'.");
                }

                parsed = resolved[0];
            }

            RemoteEndPoint = new IPEndPoint(parsed, port);
        }

        internal ConnectionInfo(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint)
        {
            RemoteEndPoint = remoteEndPoint;
            LocalEndPoint = localEndPoint;
        }

        public override string ToString()
        {
            return RemoteEndPoint == null ? "<unknown>" : RemoteEndPoint.ToString();
        }
    }
}

namespace NetworkCommsDotNet.Connections
{
    public enum ConnectionType
    {
        Undefined = 0,
        TCP = 1
    }

    /// <summary>
    /// A single TCP connection. Frames are read on a dedicated background thread and dispatched to
    /// the handlers registered with <see cref="NetworkComms"/>.
    /// </summary>
    public class Connection
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly object sendLocker = new object();
        private Thread readThread;
        private volatile bool closed;

        public ConnectionInfo ConnectionInfo { get; private set; }

        internal Connection(TcpClient client)
        {
            this.client = client;
            this.client.NoDelay = true;
            stream = client.GetStream();

            ConnectionInfo = new ConnectionInfo(
                client.Client.RemoteEndPoint as IPEndPoint,
                client.Client.LocalEndPoint as IPEndPoint);
            ConnectionInfo.AttachedConnection = this;
        }

        internal void StartReading()
        {
            readThread = new Thread(ReadLoop);
            readThread.IsBackground = true;
            readThread.Name = "WLMNet receive " + ConnectionInfo;
            readThread.Start();
        }

        /// <summary>Serialises <paramref name="packetData"/> and writes it as a single frame.</summary>
        public void SendObject(string packetType, object packetData)
        {
            SendReceiveOptions options = NetworkComms.DefaultSendReceiveOptions;

            byte[] payload = options.DataSerializer.SerialiseDataObject(packetData);
            byte[] body = Wire.BuildBody(packetType, payload);

            foreach (DataProcessor processor in options.DataProcessors)
            {
                body = processor.ForwardProcessDataStream(body, options.Options);
            }

            byte[] frame = new byte[4 + body.Length];
            Buffer.BlockCopy(Wire.Int32ToBytes(body.Length), 0, frame, 0, 4);
            Buffer.BlockCopy(body, 0, frame, 4, body.Length);

            lock (sendLocker)
            {
                if (closed)
                {
                    throw new IOException("Connection has been closed.");
                }

                stream.Write(frame, 0, frame.Length);
                stream.Flush();
            }
        }

        private void ReadLoop()
        {
            try
            {
                byte[] lengthBuffer = new byte[4];

                while (!closed)
                {
                    if (!ReadExactly(lengthBuffer, 4))
                    {
                        break;
                    }

                    int bodyLength = Wire.BytesToInt32(lengthBuffer);
                    if (bodyLength <= 0 || bodyLength > Wire.MaxFrameSize)
                    {
                        break;
                    }

                    byte[] body = new byte[bodyLength];
                    if (!ReadExactly(body, bodyLength))
                    {
                        break;
                    }

                    HandleFrame(body);
                }
            }
            catch
            {
                // Any socket or protocol failure ends the connection; reported via CloseConnection.
            }

            CloseConnection();
        }

        private void HandleFrame(byte[] body)
        {
            SendReceiveOptions options = NetworkComms.DefaultSendReceiveOptions;

            try
            {
                for (int i = options.DataProcessors.Count - 1; i >= 0; i--)
                {
                    body = options.DataProcessors[i].ReverseProcessDataStream(body, options.Options);
                }

                string packetType;
                byte[] payload;
                if (!Wire.TryParseBody(body, out packetType, out payload))
                {
                    return;
                }

                NetworkComms.TriggerIncomingPacketHandler(
                    new PacketHeader(packetType, payload.Length), this, payload, options);
            }
            catch
            {
                // A single malformed or undecryptable packet must not take the connection down.
            }
        }

        private bool ReadExactly(byte[] buffer, int count)
        {
            int read = 0;

            while (read < count)
            {
                int got = stream.Read(buffer, read, count - read);
                if (got <= 0)
                {
                    return false;
                }

                read += got;
            }

            return true;
        }

        /// <summary>Closes the socket and raises the global close handlers exactly once.</summary>
        public void CloseConnection()
        {
            bool raiseHandlers = false;

            lock (sendLocker)
            {
                if (!closed)
                {
                    closed = true;
                    raiseHandlers = true;
                }
            }

            if (!raiseHandlers)
            {
                return;
            }

            try { stream.Close(); } catch { }
            try { client.Close(); } catch { }

            NetworkComms.RemoveConnection(this);
            NetworkComms.TriggerConnectionCloseHandlers(this);
        }

        public bool IsConnected
        {
            get { return !closed; }
        }

        /// <summary>Opens a client connection to the endpoint described by <paramref name="info"/>.</summary>
        internal static Connection Connect(ConnectionInfo info)
        {
            TcpClient client = new TcpClient();
            client.Connect(info.RemoteEndPoint);

            Connection connection = new Connection(client);
            info.AttachedConnection = connection;

            NetworkComms.RegisterConnection(connection);
            connection.StartReading();

            return connection;
        }

        public static void StartListening(ConnectionType connectionType, IPEndPoint localEndPoint)
        {
            NetworkComms.StartListening(localEndPoint);
        }

        public static List<IPEndPoint> ExistingLocalListenEndPoints(ConnectionType connectionType)
        {
            return NetworkComms.ExistingLocalListenEndPoints();
        }
    }
}

namespace NetworkCommsDotNet.Connections.TCP
{
    /// <summary>
    /// Entry point used by the application to obtain a connection for a given
    /// <see cref="ConnectionInfo"/>, creating one on demand for outgoing connections.
    /// </summary>
    public static class TCPConnection
    {
        public static Connection GetConnection(ConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
            {
                throw new ArgumentNullException("connectionInfo");
            }

            Connection existing = connectionInfo.AttachedConnection;
            if (existing != null && existing.IsConnected)
            {
                return existing;
            }

            return Connection.Connect(connectionInfo);
        }
    }
}
