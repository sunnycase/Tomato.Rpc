using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tomato.Rpc.Test.Rpc
{
    partial class RpcServerCallingProxy : IRpcServer
    {
        Tomato.Rpc.Core.ICallingProxyDispatcher Dispatcher
        {
            get;
        }

        public RpcServerCallingProxy(Tomato.Rpc.Core.ICallingProxyDispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public async Task<int> Add(int a, int b)
        {
            return (int)await Dispatcher.DoCallAndWaitAnswer(new Packets.Add__System_Int32__System_Int32{Arg0 = a, Arg1 = b});
        }

        public async Task<int> Minus(int a, int b)
        {
            return (int)await Dispatcher.DoCallAndWaitAnswer(new Packets.Minus__System_Int32__System_Int32{Arg0 = a, Arg1 = b});
        }

        public async Task SayHello()
        {
            await Dispatcher.DoCallAndWaitAnswer(new Packets.SayHello__{});
        }

        public void SayBye()
        {
            Dispatcher.DoCall(new Packets.SayBye__{});
        }
    }

    partial class RpcServerCalledProxy : Tomato.Rpc.Core.IHandleRpcPacket<Packets.Add__System_Int32__System_Int32, Task<int>>, Tomato.Rpc.Core.IHandleRpcPacket<Packets.Minus__System_Int32__System_Int32, Task<int>>, Tomato.Rpc.Core.IHandleRpcPacket<Packets.SayHello__, Task>, Tomato.Rpc.Core.IHandleRpcPacket<Packets.SayBye__>
    {
        IRpcServer Service
        {
            get;
        }

        public RpcServerCalledProxy(IRpcServer service)
        {
            Service = service;
        }

        public Task<int> Handle(Packets.Add__System_Int32__System_Int32 args)
        {
            return Service.Add(args.Arg0, args.Arg1);
        }

        public Task<int> Handle(Packets.Minus__System_Int32__System_Int32 args)
        {
            return Service.Minus(args.Arg0, args.Arg1);
        }

        public Task Handle(Packets.SayHello__ args)
        {
            return Service.SayHello();
        }

        public void Handle(Packets.SayBye__ args)
        {
            Service.SayBye();
        }
    }

    namespace Packets
    {
        sealed class Add__System_Int32__System_Int32
        {
            public int Arg0;
            public int Arg1;
        }

        sealed class Minus__System_Int32__System_Int32
        {
            public int Arg0;
            public int Arg1;
        }

        sealed class SayHello__
        {
        }

        sealed class SayBye__
        {
        }
    }
}