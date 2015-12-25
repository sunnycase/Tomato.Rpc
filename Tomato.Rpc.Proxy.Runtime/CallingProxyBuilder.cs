using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Tomato.Rpc.Core;

namespace Tomato.Rpc.Proxy.Runtime
{
    public class CallingProxyBuilder
    {
        private static ModuleBuilder _proxyModuleBuilder;
        private const string ProxyAssemblyName = "Tomato.Rpc.DynamicCaliingProxyAssembly";
        private const string ProxyModuleName = "Tomato.Rpc.DynamicCaliingProxyAssembly.ProxyModule";
        private const string ProxyTypePrefix = "Tomato.Rpc.DynamicCaliingProxyAssembly.Proxy.";

        static CallingProxyBuilder()
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(ProxyAssemblyName), AssemblyBuilderAccess.Run);
            _proxyModuleBuilder = assemblyBuilder.DefineDynamicModule(ProxyModuleName);
        }

        private readonly Type _serviceType;
        private TypeBuilder _typeBuilder;
        private FieldBuilder _packetSender;

        public CallingProxyBuilder(Type serviceType)
        {
            if (!serviceType.GetTypeInfo().IsInterface)
                throw new ArgumentException("Must be a interface type.", nameof(serviceType));
            _serviceType = serviceType;
        }

        public static Func<IPacketSender, IPacketReceiver> CreateActivator<TService>(Type proxyType)
        {
            return s => (IPacketReceiver)Activator.CreateInstance(proxyType, s);
        }

        public Type Build(PacketBuilder packetBuilder)
        {
            _typeBuilder = _proxyModuleBuilder.DefineType($"{ProxyTypePrefix}{_serviceType.FullName}", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed,
                null, new[] { _serviceType, typeof(IPacketReceiver) });
            CreateMessageSenderField();
            CreateConstructor();
            var methods = (from m in _serviceType.GetRuntimeMethods()
                           where m.ReturnType == typeof(void)
                           select m);
            foreach (var m in methods)
                ImplementProxyMethod(m, packetBuilder.GetPacketType(m));
            ImplementIMessageReceiver();

            return _typeBuilder.CreateTypeInfo().AsType();
        }

        private void CreateMessageSenderField()
        {
            _packetSender = _typeBuilder.DefineField("_packetSender", typeof(IPacketSender), FieldAttributes.Private | FieldAttributes.InitOnly);
        }

        private void CreateConstructor()
        {
            var ctor = _typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard, new[] { typeof(IPacketSender) });
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, _packetSender);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[] { }));
            il.Emit(OpCodes.Ret);
        }

        private void ImplementProxyMethod(MethodInfo method, Type packetType)
        {
            var @params = method.GetParameters();
            var newMethod = _typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final
                | MethodAttributes.Virtual, method.CallingConvention, method.ReturnType, @params.Select(o => o.ParameterType).ToArray());
            var il = newMethod.GetILGenerator();
            var packet = il.DeclareLocal(packetType);
            il.Emit(OpCodes.Newobj, packetType.GetConstructor(new Type[0]));
            il.Emit(OpCodes.Stloc, packet);
            int paramIndex = 1;
            foreach (var param in @params)
            {
                il.Emit(OpCodes.Ldloc, packet);
                il.Emit(OpCodes.Ldarg, paramIndex);
                il.Emit(OpCodes.Stfld, packetType.GetField($"Arg{paramIndex - 1}"));
                paramIndex++;
            }
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, _packetSender);
            il.Emit(OpCodes.Ldloc, packet);
            il.Emit(OpCodes.Call, typeof(IPacketSender).GetMethod(nameof(IPacketSender.Send)));
            il.Emit(OpCodes.Ret);
        }

        private void ImplementIMessageReceiver()
        {
            var oldMethod = typeof(IPacketReceiver).GetMethod(nameof(IPacketReceiver.OnReceive));
            var method = _typeBuilder.DefineMethod(typeof(IPacketReceiver).FullName + nameof(IPacketReceiver.OnReceive),
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.Virtual,
                oldMethod.ReturnType, oldMethod.GetParameters().Select(o => o.ParameterType).ToArray());
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ret);

            _typeBuilder.DefineMethodOverride(method, oldMethod);
        }
    }
}
