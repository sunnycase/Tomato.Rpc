using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tomato.Rpc.Test
{
    class Program
    {
        static CallingProxyDispatcher _callingDispatcher;
        static CalledProxyDispatcher<Rpc.RpcServerCalledProxy> _calledDispatcher;

        static void Main(string[] args)
        {
            _callingDispatcher = new CallingProxyDispatcher(p =>
            {
                _calledDispatcher.Receive(p);
                return Task.FromResult<object>(null);
            });
            _calledDispatcher = new CalledProxyDispatcher<Rpc.RpcServerCalledProxy>(new Rpc.RpcServerCalledProxy(new RpcServer()), p =>
            {
                _callingDispatcher.Receive(p);
                return Task.FromResult<object>(null);
            });

            var service = new Rpc.RpcServerCallingProxy(_callingDispatcher);
            Console.WriteLine($"Add(1, 2) = {service.Add(1, 2).Result}");
            Console.WriteLine($"Minus(3, 2) = {service.Minus(3, 2).Result}");
            service.SayHello();
            service.SayBye();

            Console.Read();
        }
    }

    class RpcServer : IRpcServer
    {
        public Task<int> Add(int a, int b)
        {
            return Task.FromResult<int>(a + b);
        }

        public Task<int> Minus(int a, int b)
        {
            return Task.FromResult<int>(a - b);
        }

        public Task SayHello()
        {
            Console.WriteLine("Hello");
            return Task.FromResult<object>(null);
        }

        public void SayBye()
        {
            Console.WriteLine("Bye");
        }
    }

    [RpcPeer]
    interface IRpcServer
    {
        Task<int> Add(int a, int b);
        Task<int> Minus(int a, int b);
        Task SayHello();
        void SayBye();
    }
}
