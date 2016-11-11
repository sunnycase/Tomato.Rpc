using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tomato.Rpc.Core
{
    public interface IHandleRpcPacket<in TArgs>
    {
        void Handle(TArgs args);
    }

    public interface IHandleRpcPacket<in TArgs, out TResult> where TResult : Task
    {
        TResult Handle(TArgs args);
    }
}
