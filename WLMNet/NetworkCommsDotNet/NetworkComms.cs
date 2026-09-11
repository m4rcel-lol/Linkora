using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.DPSBase;

namespace NetworkCommsDotNet
{
    /// <summary>Signature of a typed incoming packet handler.</summary>
    public delegate void PacketHandlerCallBackDelegate<T>(PacketHeader packetHeader, Connection connection, T incomingObject);

    /// <summary>
    /// Process wide hub holding the packet handlers, the connection close handlers, the active
    /// connections and any TCP listeners.
    /// </summary>
    public static class NetworkComms
    {
        /// <summary>Delegate raised when a connection is established or torn down.</summary>
        public delegate void ConnectionEstablishShutdownDelegate(Connection connection);

        private static readonly object locker = new object();

        private static readonly Dictionary<string, List<Action<PacketHeader, Connection, byte[], SendReceiveOptions>>> packetHandlers =
            new Dictionary<string, List<Action<PacketHeader, Connection, byte[], SendReceiveOptions>>>();

        private static readonly List<ConnectionEstablishShutdownDelegate> closeHandlers =
            new List<ConnectionEstablishShutdownDelegate>();

        private static readonly List<Connection> connections = new List<Connection>();
        private static readonly List<TcpListener> listeners = new List<TcpListener>();

        private static SendReceiveOptions defaultSendReceiveOptions = new SendReceiveOptions();

        public static SendReceiveOptions DefaultSendReceiveOptions
        {
            get { return defaultSendReceiveOptions; }
            set { defaultSendReceiveOptions = value ?? new SendReceiveOptions(); }
        }

        #region Packet handlers

        public static void AppendGlobalIncomingPacketHandler<T>(string packetTypeStr,
            PacketHandlerCallBackDelegate<T> packetHandlerDelegate)
        {
            if (packetHandlerDelegate == null)
            {
                return;
            }

            Action<PacketHeader, Connection, byte[], SendReceiveOptions> dispatcher =
                (header, connection, payload, options) =>
                {
                    T incomingObject = options.DataSerializer.DeserialiseDataObject<T>(payload);
                    packetHandlerDelegate(header, connection, incomingObject);
                };

            lock (locker)
            {
                List<Action<PacketHeader, Connection, byte[], SendReceiveOptions>> existing;
                if (!packetHandlers.TryGetValue(packetTypeStr, out existing))
                {
                    existing = new List<Action<PacketHeader, Connection, byte[], SendReceiveOptions>>();
                    packetHandlers[packetTypeStr] = existing;
                }

                existing.Add(dispatcher);
            }
        }

        public static void RemoveGlobalIncomingPacketHandler(string packetTypeStr)
        {
            lock (locker)
            {
                packetHandlers.Remove(packetTypeStr);
            }
        }

        internal static void TriggerIncomingPacketHandler(PacketHeader header, Connection connection,
            byte[] payload, SendReceiveOptions options)
        {
            List<Action<PacketHeader, Connection, byte[], SendReceiveOptions>> handlers;

            lock (locker)
            {
                if (!packetHandlers.TryGetValue(header.PacketType, out handlers))
                {
                    return;
                }

                handlers = new List<Action<PacketHeader, Connection, byte[], SendReceiveOptions>>(handlers);
            }

            foreach (Action<PacketHeader, Connection, byte[], SendReceiveOptions> handler in handlers)
            {
                try
                {
                    handler(header, connection, payload, options);
                }
                catch
                {
                    // A failing handler must not stop the remaining handlers or the read loop.
                }
            }
        }

        #endregion

        #region Connection close handlers

        public static void AppendGlobalConnectionCloseHandler(ConnectionEstablishShutdownDelegate handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (locker)
            {
                if (!closeHandlers.Contains(handler))
                {
                    closeHandlers.Add(handler);
                }
            }
        }

        public static void RemoveGlobalConnectionCloseHandler(ConnectionEstablishShutdownDelegate handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (locker)
            {
                closeHandlers.Remove(handler);
            }
        }

        internal static void TriggerConnectionCloseHandlers(Connection connection)
        {
            List<ConnectionEstablishShutdownDelegate> handlers;

            lock (locker)
            {
                handlers = new List<ConnectionEstablishShutdownDelegate>(closeHandlers);
            }

            foreach (ConnectionEstablishShutdownDelegate handler in handlers)
            {
                try
                {
                    handler(connection);
                }
                catch
                {
                    // Ignore handler failures during teardown.
                }
            }
        }

        #endregion

        #region Connections and listeners

        internal static void RegisterConnection(Connection connection)
        {
            lock (locker)
            {
                if (!connections.Contains(connection))
                {
                    connections.Add(connection);
                }
            }
        }

        internal static void RemoveConnection(Connection connection)
        {
            lock (locker)
            {
                connections.Remove(connection);
            }
        }

        internal static void StartListening(IPEndPoint localEndPoint)
        {
            TcpListener listener = new TcpListener(localEndPoint);
            listener.Start();

            lock (locker)
            {
                listeners.Add(listener);
            }

            Thread acceptThread = new Thread(() => AcceptLoop(listener));
            acceptThread.IsBackground = true;
            acceptThread.Name = "WLMNet listen " + localEndPoint;
            acceptThread.Start();
        }

        private static void AcceptLoop(TcpListener listener)
        {
            while (true)
            {
                TcpClient client;

                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch
                {
                    // Listener stopped, most likely by Shutdown().
                    return;
                }

                try
                {
                    Connection connection = new Connection(client);

                    RegisterConnection(connection);
                    connection.StartReading();
                }
                catch
                {
                    try { client.Close(); } catch { }
                }
            }
        }

        internal static List<IPEndPoint> ExistingLocalListenEndPoints()
        {
            List<IPEndPoint> endPoints = new List<IPEndPoint>();

            lock (locker)
            {
                foreach (TcpListener listener in listeners)
                {
                    endPoints.Add((IPEndPoint)listener.LocalEndpoint);
                }
            }

            return endPoints;
        }

        /// <summary>
        /// Closes every open connection. Packet and close handler registrations are preserved, which
        /// matches how the application re-uses them across sign in attempts.
        /// </summary>
        public static void Shutdown(int threadShutdownTimeoutMS = 1000)
        {
            List<Connection> openConnections;

            lock (locker)
            {
                openConnections = new List<Connection>(connections);
            }

            foreach (Connection connection in openConnections)
            {
                try
                {
                    connection.CloseConnection();
                }
                catch
                {
                    // Best effort teardown.
                }
            }
        }

        /// <summary>Stops all listeners, used when the server process is shutting down.</summary>
        public static void StopListening()
        {
            List<TcpListener> openListeners;

            lock (locker)
            {
                openListeners = new List<TcpListener>(listeners);
                listeners.Clear();
            }

            foreach (TcpListener listener in openListeners)
            {
                try { listener.Stop(); } catch { }
            }
        }

        #endregion
    }
}
