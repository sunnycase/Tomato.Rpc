using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tomato.Rpc.Core;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Tomato.Rpc
{
    public class CalledProxyDispatcher<TProxy> : ICalledProxyDispatcher
    {
        private TProxy _proxy;
        private readonly Func<RpcAnswerPacket, Task> _onSendPacket;

        private static readonly IReadOnlyDictionary<Type, IRpcHandlerInvoker> _invokers;

        static CalledProxyDispatcher()
        {
            var ifs = typeof(TProxy).GetTypeInfo().ImplementedInterfaces;
            _invokers = (from i in ifs
                         where i.IsConstructedGenericType &&
                         (i.GetGenericTypeDefinition() == typeof(IHandleRpcPacket<>) || i.GetGenericTypeDefinition() == typeof(IHandleRpcPacket<,>))
                         select new
                         {
                             Type = i.GenericTypeArguments[0],
                             Invoker = CreateInvoker(i.GenericTypeArguments)
                         }).ToDictionary(o => o.Type, o => o.Invoker);
        }

        public CalledProxyDispatcher(TProxy proxy, Func<RpcAnswerPacket, Task> onSendPacket)
        {
            if (proxy == null)
                throw new ArgumentNullException(nameof(proxy));
            if (onSendPacket == null)
                throw new ArgumentNullException(nameof(onSendPacket));
            _proxy = proxy;
            _onSendPacket = onSendPacket;
        }

        public async void Receive(RpcPacket packet)
        {
            IRpcHandlerInvoker invoker;
            if (_invokers.TryGetValue(packet.Call.GetType(), out invoker))
                await invoker.Invoke(_proxy, packet, SendAnswerPacket);
            else
                throw new ArgumentException("Unrecognized packet.");
        }

        private async void SendAnswerPacket(RpcAnswerPacket packet)
        {
            await _onSendPacket(packet);
        }

        private static IRpcHandlerInvoker CreateInvoker(Type[] genericTypeArguments)
        {
            var argType = genericTypeArguments[0];
            object invoker;
            if (genericTypeArguments.Length == 1)
                invoker = Activator.CreateInstance(typeof(RpcHandlerInvoker<>).MakeGenericType(typeof(TProxy), argType));
            else if(genericTypeArguments[1] == typeof(Task))
                invoker = Activator.CreateInstance(typeof(RpcHandlerTaskInvoker<>).MakeGenericType(typeof(TProxy), argType));
            else
            {
                var resultType = genericTypeArguments[1].GenericTypeArguments[0];
                var invokerType = typeof(RpcHandlerInvoker<,>).MakeGenericType(typeof(TProxy), argType, resultType);
                invoker = Activator.CreateInstance(invokerType);
            }
            return (IRpcHandlerInvoker)invoker;
        }
        
        private interface IRpcHandlerInvoker
        {
            Task Invoke(TProxy proxy, RpcPacket packet, Action<RpcAnswerPacket> onSendPacket);
        }

        private class RpcHandlerInvoker<T> : IRpcHandlerInvoker
        {
            public Task Invoke(TProxy proxy, RpcPacket packet, Action<RpcAnswerPacket> onSendPacket)
            {
                ((IHandleRpcPacket<T>)proxy).Handle((T)packet.Call);
                return Task.FromResult<object>(null);
            }
        }

        private class RpcHandlerInvoker<T, TResult> : IRpcHandlerInvoker
        {
            public async Task Invoke(TProxy proxy, RpcPacket packet, Action<RpcAnswerPacket> onSendPacket)
            {
                var result = await ((IHandleRpcPacket<T, Task<TResult>>)proxy).Handle((T)packet.Call);
                onSendPacket(new RpcAnswerPacket
                {
                    CallId = packet.CallId,
                    Return = result
                });
            }
        }

        private class RpcHandlerTaskInvoker<T> : IRpcHandlerInvoker
        {
            public async Task Invoke(TProxy proxy, RpcPacket packet, Action<RpcAnswerPacket> onSendPacket)
            {
                await ((IHandleRpcPacket<T, Task>)proxy).Handle((T)packet.Call);
                onSendPacket(new RpcAnswerPacket
                {
                    CallId = packet.CallId,
                    Return = null
                });
            }
        }
    }
}
