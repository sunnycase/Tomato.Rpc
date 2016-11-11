using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tomato.Rpc.Core;

namespace Tomato.Rpc.Core
{
    public interface ICallingProxyDispatcher
    {
        void DoCall(object call);
        Task<object> DoCallAndWaitAnswer(object call);
        void Receive(RpcAnswerPacket packet);
    }
}
