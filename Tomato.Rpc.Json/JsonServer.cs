using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tomato.Rpc.Core;

namespace Tomato.Rpc.Json
{
    public class JsonServer<TService> : IMessageClient
    {
        private readonly Dictionary<Type, Action<object>> _deles = new Dictionary<Type, Action<object>>();
        private readonly IPacketReceiver _proxy;

        private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        public JsonServer(Func<TService, IPacketReceiver> calledProxyActivator, TService service)
        {
            _proxy = calledProxyActivator(service);
        }

        public JsonServer(Func<TService, IPacketReceiver> calledProxyActivator, TService service, SerializationBinder serializationBinder)
        {
            _serializerSettings.Binder = serializationBinder;
            _proxy = calledProxyActivator(service);
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
