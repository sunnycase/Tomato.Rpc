using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Tomato.Rpc.Json
{
    public class PacketBuilder
    {
        private static ModuleBuilder _packetModuleBuilder;
        private const string PacketAssemblyName = "Tomato.Rpc.DynamicPacketAssembly";
        private const string PacketModuleName = "Tomato.Rpc.DynamicPacketAssembly.ProxyModule";
        private const string PacketTypePrefix = "Tomato.Rpc.DynamicPacketAssembly.";

        private readonly Type _serviceType;
        private readonly Dictionary<MethodInfo, Type> _packetsMap = new Dictionary<MethodInfo, Type>();

        static PacketBuilder()
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(PacketAssemblyName), AssemblyBuilderAccess.Run);
            _packetModuleBuilder = assemblyBuilder.DefineDynamicModule(PacketModuleName);
        }

        public PacketBuilder(Type serviceType)
        {
            if (!serviceType.GetTypeInfo().IsInterface)
                throw new ArgumentException("Must be a interface type.", nameof(serviceType));
            _serviceType = serviceType;
        }

        public void Build()
        {
            foreach (var method in from m in _serviceType.GetRuntimeMethods()
                                   where m.ReturnType == typeof(void)
                                   select m)
            {
                _packetsMap.Add(method, CreateMethodPacketType(method));
            }
        }

        public Type GetPacketType(MethodInfo method)
        {
            return _packetsMap[method];
        }

        private Type CreateMethodPacketType(MethodInfo method)
        {
            var typeBuilder = _packetModuleBuilder.DefineType($"{PacketTypePrefix}{Uri.EscapeDataString($"{_serviceType.FullName}.{method}")}",
                TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public);
            var @params = method.GetParameters();
            for (int i = 0; i < @params.Length; i++)
            {
                var paramType = @params[i].ParameterType.IsByRef ? @params[i].ParameterType.GetElementType() : @params[i].ParameterType;
                typeBuilder.DefineField($"Arg{i}", paramType, FieldAttributes.Public);
            }
            return typeBuilder.CreateTypeInfo().AsType();
        }

        public class PacketSerializationBinder : SerializationBinder
        {
            private readonly SerializationBinder _default = new DefaultSerializationBinder();
            public override Type BindToType(string assemblyName, string typeName)
            {
                if (assemblyName == PacketBuilder.PacketAssemblyName)
                    return PacketBuilder._packetModuleBuilder.GetType(typeName, true, false);
                return _default.BindToType(assemblyName, typeName);
            }
        }
    }
}
