using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ModLoader.Core.Managed
{
    // ----------------- Hook Attribute -----------------
    [AttributeUsage(AttributeTargets.Method)]
    public class HookAttribute : Attribute
    {
        public Type TargetType;
        public string TargetMethod;

        public HookAttribute(Type targetType, string targetMethod)
        {
            TargetType = targetType;
            TargetMethod = targetMethod;
        }
    }

    // ----------------- Hook Registry -----------------
    internal static class HookRegistry
    {
        // Store the original MethodInfo
        private static readonly Dictionary<(Type, string), MethodInfo> originals =
            new Dictionary<(Type, string), MethodInfo>();

        public static void RegisterOriginal(MethodInfo target, MethodInfo original)
        {
            originals[(target.DeclaringType, target.Name)] = original;
        }

        public static MethodInfo GetOriginal(Type type, string methodName)
        {
            originals.TryGetValue((type, methodName), out var method);
            return method;
        }
    }

    // ----------------- Hook Installer -----------------
    public static class HookInstaller
    {
        public static void RegisterHooksFromAssembly(Assembly asm)
        {
            foreach (var type in asm.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var attr = (HookAttribute)method.GetCustomAttributes(typeof(HookAttribute), false).FirstOrDefault();
                    if (attr == null) continue;

                    InstallHook(method, attr);
                }
            }
        }

        private static void InstallHook(MethodInfo hookMethod, HookAttribute attr)
        {
            if (attr.TargetType == null || string.IsNullOrEmpty(attr.TargetMethod))
            {
                API.Log("ModLoader", "[Hook] Invalid HookAttribute!");
                return;
            }

            var targetMethod = attr.TargetType.GetMethod(attr.TargetMethod,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                API.Log("ModLoader", $"[Hook] Could not find method {attr.TargetMethod} on {attr.TargetType}");
                return;
            }

            // Save original method
            HookRegistry.RegisterOriginal(targetMethod, targetMethod);

            // Replace the method at runtime (Mono only safe version)
            RuntimeHook(targetMethod, hookMethod);

            API.Log("ModLoader", $"[Hook] Patched {attr.TargetType}.{attr.TargetMethod}");
        }

        private static void RuntimeHook(MethodInfo target, MethodInfo replacement)
        {
            // Mono / IL2CPP compatible: simple swap using MethodHandle + PrepareMethod
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
            RuntimeHelpers.PrepareMethod(replacement.MethodHandle);

            // Swap method bodies (only works in pure Mono / Unity Editor)
            unsafe
            {
                if (IntPtr.Size == 8)
                {
                    ulong* inj = (ulong*)replacement.MethodHandle.GetFunctionPointer().ToPointer();
                    ulong* tar = (ulong*)target.MethodHandle.GetFunctionPointer().ToPointer();
                    *tar = *inj;
                }
                else
                {
                    uint* inj = (uint*)replacement.MethodHandle.GetFunctionPointer().ToPointer();
                    uint* tar = (uint*)target.MethodHandle.GetFunctionPointer().ToPointer();
                    *tar = *inj;
                }
            }
        }
    }

    // ----------------- Original Caller -----------------
    public static class Hooks
    {
        public static void Original(object self, string methodName, params object[] args)
        {
            if (self == null || string.IsNullOrEmpty(methodName))
                return;

            var type = self.GetType();
            var original = HookRegistry.GetOriginal(type, methodName);
            if (original == null)
                return;

            original.Invoke(self, args);
        }

        public static TReturn Original<TReturn>(object self, string methodName, params object[] args)
        {
            if (self == null || string.IsNullOrEmpty(methodName))
                return default;

            var type = self.GetType();
            var original = HookRegistry.GetOriginal(type, methodName);
            if (original == null)
                return default;

            return (TReturn)original.Invoke(self, args);
        }
    }
}
