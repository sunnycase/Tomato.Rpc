using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;
using Tomato.Rpc.Core;

namespace Tomato.Rpc.Json
{
    public class JsonClient<TService> : IMessageClient, IPacketSender
    {
        private readonly IPacketReceiver _proxy;

        public TService Proxy => (TService)_proxy;
        public Action<string> OnSendMessage { get; set; }

        private static JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        public JsonClient(Func<IPacketSender, IPacketReceiver> callingProxyActivator)
        {
            _proxy = callingProxyActivator(this);
        }

        public void OnReceive(string message)
        {
            _proxy.OnReceive(message);
        }

        public void Send(string message)
        {
            OnSendMessage?.Invoke(message);
        }

        void IPacketSender.Send(object message)
        {
            Send(JsonConvert.SerializeObject(new RpcPacket
            {
                Call = message
            }, _serializerSettings));
        }
    }
}
