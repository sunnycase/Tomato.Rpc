# Tomato.Rpc
=================

简单的 RPC 框架，提供基于 Roslyn 的 Design-Time Proxy 生成。

A simple RPC framework, providing design-time proxy generation based on Roslyn.

Build Status
------------

|   | Core | Design-Time |
|---|:-----:|:-------:|
|[![NuGet Stable](https://img.shields.io/nuget/v/tomato.media.svg)](https://www.nuget.org/packages/Tomato.Rpc.Core)|
[![NuGet Stable](https://img.shields.io/nuget/v/tomato.media.svg)](https://www.nuget.org/packages/Tomato.Rpc.Proxy.DesignTime)|

## Define RPC Interface
Install the [Tomato.Rpc.Core][NuPkg].

Rpc.cs

```csharp

    [RpcPeer]
    interface IRpcServer
    {
        Task<int> Add(int a, int b);
        Task<int> Minus(int a, int b);
        Task SayHello();
        void SayBye(IReadOnlyCollection<int> items);
    }
```
## Generate Proxy
Install the [Tomato.Rpc.Proxy.DesignTime][NuPkg].

Build project.

Then you get Rpc.RpcProxy.cs

```csharp

partial class RpcServerCallingProxy : IRpcServer
partial class RpcServerCalledProxy
```