using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Tomato.Rpc.Json;
using Tomato.Rpc.Test.Interface;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace Tomato.Rpc.Test.UWP
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

#if false
            var packetBuilder = new PacketBuilder(typeof(IService));
            packetBuilder.Build();
            var callingBuilder = new CallingProxyBuilder(typeof(IService));
            var client = new JsonClient<IService>(CallingProxyBuilder.CreateActivator<IService>(callingBuilder.Build(packetBuilder)));
            var calledBuilder = new CalledProxyBuilder(typeof(IService));
            var server = new JsonServer<IService>(CalledProxyBuilder.CreateActivator<IService>(calledBuilder.BuildType(packetBuilder)), new Service(), new PacketBuilder.PacketSerializationBinder());
#else
            var client = new JsonClient<ITest>(s => new Tomato.Rpc.Client.RpcCallingProxy(s));
            var server = new JsonServer<ITest>(s => new Tomato.Rpc.Server.RpcCalledProxy(s), new Test());
#endif
            client.OnSendMessage = m => server.OnReceive(m);
            client.Proxy.Add(1, 2);
        }

        class Test : ITest
        {
            public void Add(int a, int b)
            {
                var dialog = new MessageDialog($"Add: {a}, {b} ");
                dialog.ShowAsync();
            }
        }

        public interface IService
        {
            void Add(int a, int b);
        }

        class Service : IService
        {
            public void Add(int a, int b)
            {
                var dialog = new MessageDialog($"Add: {a}, {b} ");
                dialog.ShowAsync();
            }
        }
    }
}
