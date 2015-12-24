using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tomato.Rpc.Json
{
    public class JsonServer<TService> : IMessageClient
    {
        private static Type _proxyType;
        private readonly Dictionary<Type, Action<object>> _deles = new Dictionary<Type, Action<object>>();
        private readonly IPacketReceiver _proxy;

        private static JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Binder = new PacketBuilder.PacketSerializationBinder()
        };

        public JsonServer(TService service, PacketBuilder packetBuilder)
        {
            if (_proxyType == null)
                _proxyType = new CalledProxyBuilder(typeof(TService)).Build(packetBuilder);
            
            _proxy = (IPacketReceiver)Activator.CreateInstance(_proxyType, service);
        }

        public void OnReceive(string message)
        {
            var packet = JsonConvert.DeserializeObject<RpcPacket>(message, _serializerSettings);
            _proxy.OnReceive(packet.Call);
        }

        public void Send(string message)
        {

        }
    }
}
