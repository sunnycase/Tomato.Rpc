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
    public class CalledProxyBuilder
    {
        private static ModuleBuilder _proxyModuleBuilder;
        private const string ProxyAssemblyName = "Tomato.Rpc.DynamicCalledProxyAssembly";
        private const string ProxyModuleName = "Tomato.Rpc.DynamicCalledProxyAssembly.ProxyModule";
        private const string ProxyTypePrefix = "Tomato.Rpc.DynamicCalledProxyAssembly.Proxy.";

        static CalledProxyBuilder()
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(ProxyAssemblyName), AssemblyBuilderAccess.Run);
            _proxyModuleBuilder = assemblyBuilder.DefineDynamicModule(ProxyModuleName);
        }

        private readonly Type _serviceType;
        private TypeBuilder _typeBuilder;
        private FieldBuilder _serviceImpl;
        private FieldBuilder _packetMethodMap;

        public CalledProxyBuilder(Type serviceType)
        {
            if (!serviceType.GetTypeInfo().IsInterface)
                throw new ArgumentException("Must be a interface type.", nameof(serviceType));
            _serviceType = serviceType;
        }

        public static Func<TService, IPacketReceiver> CreateActivator<TService>(Type proxyType)
        {
            return s => (IPacketReceiver)Activator.CreateInstance(proxyType, s);
        }

        public Type BuildType(PacketBuilder packetBuilder)
        {
            _typeBuilder = _proxyModuleBuilder.DefineType($"{ProxyTypePrefix}{_serviceType.FullName}", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed,
                null, new[] { typeof(IPacketReceiver) });
            CreateFields();
            var packetProcessors = (from m in _serviceType.GetRuntimeMethods()
                                    where m.ReturnType == typeof(void)
                                    let packetType = packetBuilder.GetPacketType(m)
                                    select new PacketMethodPair
                                    {
                                        PacketType = packetType,
                                        Method = ImplementProxyMethod(m, packetType)
                                    });
            CreateConstructor(packetProcessors);
            ImplementIMessageReceiver();

            return _typeBuilder.CreateTypeInfo().AsType();
        }

        private void CreateFields()
        {
            _serviceImpl = _typeBuilder.DefineField(nameof(_serviceImpl), _serviceType, FieldAttributes.Private | FieldAttributes.InitOnly);
            _packetMethodMap = _typeBuilder.DefineField(nameof(_packetMethodMap), typeof(Dictionary<Type, Action<object>>), FieldAttributes.Private | FieldAttributes.InitOnly);
        }

        private void CreateConstructor(IEnumerable<PacketMethodPair> packetProcessors)
        {
            var ctor = _typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard, new[] { _serviceType });
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, _serviceImpl);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newobj, typeof(Dictionary<Type, Action<object>>).GetConstructor(new Type[0]));
            il.Emit(OpCodes.Stfld, _packetMethodMap);
            var addMethod = typeof(Dictionary<Type, Action<object>>).GetMethod("Add", new[] { typeof(Type), typeof(Action<object>) });
            foreach (var processor in packetProcessors)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, _packetMethodMap);
                il.Emit(OpCodes.Ldtoken, processor.PacketType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) }));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldftn, processor.Method);
                il.Emit(OpCodes.Newobj, typeof(Action<object>).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                il.Emit(OpCodes.Call, addMethod);
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[] { }));
            il.Emit(OpCodes.Ret);
        }

        private MethodBuilder ImplementProxyMethod(MethodInfo method, Type packetType)
        {
            var @params = method.GetParameters();
            var newMethod = _typeBuilder.DefineMethod(method.ToString(), MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final
                | MethodAttributes.Virtual, method.CallingConvention, typeof(void), new[] { typeof(object) });
            var il = newMethod.GetILGenerator();
            var packet = il.DeclareLocal(packetType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, packetType);
            il.Emit(OpCodes.Stloc, packet);
            
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, _serviceImpl);
            int paramIndex = 1;
            foreach (var param in @params)
            {
                il.Emit(OpCodes.Ldloc, packet);
                il.Emit(OpCodes.Ldfld, packetType.GetField($"Arg{paramIndex - 1}"));
                paramIndex++;
            }
            il.Emit(OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return newMethod;
        }

        private void ImplementIMessageReceiver()
        {
            var oldMethod = typeof(IPacketReceiver).GetMethod(nameof(IPacketReceiver.OnReceive));
            var method = _typeBuilder.DefineMethod(typeof(IPacketReceiver).FullName + "." + nameof(IPacketReceiver.OnReceive),
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.Virtual,
                oldMethod.ReturnType, oldMethod.GetParameters().Select(o => o.ParameterType).ToArray());
            var il = method.GetILGenerator();
            var action = il.DeclareLocal(typeof(Action<object>));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, _packetMethodMap);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(object).GetMethod(nameof(object.GetType), new Type[0]));
            il.Emit(OpCodes.Ldloca, action);
            il.Emit(OpCodes.Call, typeof(Dictionary<Type, Action<object>>).GetMethod("TryGetValue", new[] { typeof(Type), typeof(Action<object>).MakeByRefType() }));
            var falseLabel = il.DefineLabel();
            il.Emit(OpCodes.Brfalse_S, falseLabel);
            il.Emit(OpCodes.Ldloc, action);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(Action<object>).GetMethod("Invoke", new[] { typeof(object) }));
            il.MarkLabel(falseLabel);
            il.Emit(OpCodes.Ret);

            _typeBuilder.DefineMethodOverride(method, oldMethod);
        }

        class PacketMethodPair
        {
            public Type PacketType;
            public MethodBuilder Method;
        }
    }
}
