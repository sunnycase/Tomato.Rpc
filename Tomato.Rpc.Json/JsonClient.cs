using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json;

namespace Tomato.Rpc.Json
{
    public class JsonClient<TService> : IMessageClient, IPacketSender
    {
        private static Type _proxyType;
        private readonly IPacketReceiver _proxy;

        public TService Proxy => (TService)_proxy;
        public Action<string> OnSendMessage { get; set; }

        private static JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        public JsonClient(PacketBuilder packetBuilder)
        {
            if (_proxyType == null)
                _proxyType = new CallingProxyBuilder(typeof(TService)).Build(packetBuilder);
            _proxy = (IPacketReceiver)Activator.CreateInstance(_proxyType, this);
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

    interface IMessageClient
    {
        void OnReceive(string message);

        void Send(string message);
    }

    public interface IPacketReceiver
    {
        void OnReceive(object packet);
    }

    public interface IPacketSender
    {
        void Send(object message);
    }

    public sealed class CallingPacket
    {
        public string Name { get; set; }
        public object[] Params { get; set; }
    }
}
