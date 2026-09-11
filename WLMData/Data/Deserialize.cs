using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

namespace WLMData.Data
{
    public static class Deserialize
    {
        /// <summary>
        /// Rebuilds a packet from its serialised bytes. This used to go through BinaryFormatter,
        /// which is no longer available (and was never safe); the packets are protobuf contracts,
        /// so protobuf-net is used instead.
        /// </summary>
        public static T FromBytes<T>(this byte[] inputSource)
        {
            using (MemoryStream memoryStream = new MemoryStream(inputSource))
            {
                memoryStream.Seek(0, SeekOrigin.Begin);

                return ProtoBuf.Serializer.Deserialize<T>(memoryStream);
            }
        }
    }
}
