using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tomato.Rpc.Core
{
    public sealed class RpcPacket
    {
        public int CallId { get; set; }
        public object Call { get; set; }
    }

    public sealed class RpcAnswerPacket
    {
        public int CallId { get; set; }
        public object Return { get; set; }
    }
}
