﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tomato.Rpc.Core
{
    public interface ICalledProxyDispatcher
    {
        void Receive(RpcPacket packet);
    }
}
