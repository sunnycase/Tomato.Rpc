using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tomato.Rpc.Core;

namespace Tomato.Rpc
{
    public class CallingProxyDispatcher : ICallingProxyDispatcher
    {
        private readonly Func<RpcPacket, Task> _onSendPacket;
        private int _nextCallId = 0;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<RpcAnswerPacket>> _answerWaiters = new ConcurrentDictionary<int, TaskCompletionSource<RpcAnswerPacket>>();

        public TimeSpan Timeout { get; set; } = System.Threading.Timeout.InfiniteTimeSpan;

        public CallingProxyDispatcher(Func<RpcPacket, Task> onSendPacket)
        {
            if (onSendPacket == null)
                throw new ArgumentNullException(nameof(onSendPacket));
            _onSendPacket = onSendPacket;
        }

        public async void DoCall(object call)
        {
            await _onSendPacket(ConstructPacket(call));
        }

        public async Task<object> DoCallAndWaitAnswer(object call)
        {
            var packet = ConstructPacket(call);
            var task = RegisterAnswerWaiter(packet.CallId);
            await _onSendPacket(packet);
            return (await task).Return;
        }

        private RpcPacket ConstructPacket(object call)
        {
            var callId = Interlocked.Increment(ref _nextCallId);
            var packet = new RpcPacket
            {
                Call = call,
                CallId = callId
            };
            return packet;
        }

        private Task<RpcAnswerPacket> RegisterAnswerWaiter(int callId)
        {
            var tcs = new TaskCompletionSource<RpcAnswerPacket>();
            if (!_answerWaiters.TryAdd(callId, tcs))
                throw new InvalidOperationException("Cannot Register answer waiter.");

            var timeout = Timeout;
            if (timeout != System.Threading.Timeout.InfiniteTimeSpan)
                return Task.WhenAny(tcs.Task, Task.Run(async () =>
                {
                    await Task.Delay(timeout);
                    TaskCompletionSource<RpcAnswerPacket> theTcs;
                    if (_answerWaiters.TryRemove(callId, out theTcs))
                        throw new TimeoutException();
                    else
                        return await tcs.Task;
                })).Unwrap();
            return tcs.Task;
        }

        public void Receive(RpcAnswerPacket packet)
        {
            TaskCompletionSource<RpcAnswerPacket> tcs;
            if (_answerWaiters.TryRemove(packet.CallId, out tcs))
                tcs.SetResult(packet);
        }
    }
}
