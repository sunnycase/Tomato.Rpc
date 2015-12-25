using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tomato.Rpc.Core
{
    public interface IMessageClient
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
