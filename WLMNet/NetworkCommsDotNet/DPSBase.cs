using System;
using System.Collections.Generic;

namespace NetworkCommsDotNet.DPSBase
{
    /// <summary>
    /// Marker interface kept for source compatibility with NetworkComms.Net. Packet types in
    /// WLMData implement this, but the wire format is driven by the configured
    /// <see cref="DataSerializer"/> rather than these methods.
    /// </summary>
    public interface IExplicitlySerialize
    {
        void Serialize(System.IO.Stream outputStream);
        void Deserialize(System.IO.Stream inputStream);
    }

    /// <summary>Base class for objects that turn packet payloads into bytes and back.</summary>
    public abstract class DataSerializer
    {
        public abstract byte[] SerialiseDataObject(object objectToSerialise);
        public abstract T DeserialiseDataObject<T>(byte[] receivedObjectBytes);
    }

    /// <summary>Base class for transforms applied to a serialised payload, e.g. encryption.</summary>
    public abstract class DataProcessor
    {
        public abstract byte[] ForwardProcessDataStream(byte[] input, Dictionary<string, string> options);
        public abstract byte[] ReverseProcessDataStream(byte[] input, Dictionary<string, string> options);
    }

    /// <summary>Serialiser backed by protobuf-net, matching the original application's choice.</summary>
    public sealed class ProtobufSerializer : DataSerializer
    {
        public override byte[] SerialiseDataObject(object objectToSerialise)
        {
            return Wire.PayloadSerializer.Serialize(objectToSerialise);
        }

        public override T DeserialiseDataObject<T>(byte[] receivedObjectBytes)
        {
            return Wire.PayloadSerializer.Deserialize<T>(receivedObjectBytes);
        }
    }

    /// <summary>
    /// Creates and caches the serialiser / processor singletons requested by the application.
    /// </summary>
    public static class DPSManager
    {
        private static readonly Dictionary<Type, object> instances = new Dictionary<Type, object>();
        private static readonly object instancesLocker = new object();

        private static T GetInstance<T>() where T : class, new()
        {
            lock (instancesLocker)
            {
                object existing;
                if (!instances.TryGetValue(typeof(T), out existing))
                {
                    existing = new T();
                    instances[typeof(T)] = existing;
                }

                return (T)existing;
            }
        }

        public static T GetDataSerializer<T>() where T : DataSerializer, new()
        {
            return GetInstance<T>();
        }

        public static T GetDataProcessor<T>() where T : DataProcessor, new()
        {
            return GetInstance<T>();
        }
    }

    /// <summary>
    /// The serialiser, processor chain and processor options used for a send or receive.
    /// </summary>
    public class SendReceiveOptions
    {
        public DataSerializer DataSerializer { get; private set; }
        public List<DataProcessor> DataProcessors { get; private set; }
        public Dictionary<string, string> Options { get; private set; }

        public SendReceiveOptions()
            : this(new ProtobufSerializer(), new List<DataProcessor>(), new Dictionary<string, string>())
        {
        }

        public SendReceiveOptions(DataSerializer dataSerializer, List<DataProcessor> dataProcessors,
            Dictionary<string, string> options)
        {
            DataSerializer = dataSerializer ?? new ProtobufSerializer();
            DataProcessors = dataProcessors ?? new List<DataProcessor>();
            Options = options ?? new Dictionary<string, string>();
        }
    }
}
