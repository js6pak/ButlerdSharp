using System;
using System.Reflection;
using StreamJsonRpc;

namespace ButlerdSharp
{
    public static class Extensions
    {
        public static void AddLocalRpcMethodWithParameterObject(this JsonRpc jsonRpc, string? rpcMethodName, Delegate handler)
        {
            jsonRpc.AddLocalRpcMethod(handler.GetMethodInfo(), handler.Target, new JsonRpcMethodAttribute(rpcMethodName) { UseSingleObjectParameterDeserialization = true });
        }
    }
}
