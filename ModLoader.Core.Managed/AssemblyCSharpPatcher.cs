using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Cecil;

namespace ModLoader.Core.Managed
{
    internal static class AssemblyCSharpPatcher
    {
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern int MessageBoxA(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        private const string MarkerAttributeName = "ModLoaderPatchedAttribute";

        public static void PatchIfNeeded()
        {
            string asmPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Iron Lung_Data",
                "Managed",
                "Assembly-CSharp.dll"
            );

            if (!File.Exists(asmPath))
                return;

            var asm = AssemblyDefinition.ReadAssembly(
                asmPath,
                new ReaderParameters { ReadWrite = true }
            );

            if (asm.CustomAttributes.Any(a => a.AttributeType.Name == MarkerAttributeName))
                return;

            MessageBoxA(
                IntPtr.Zero,
                "Patching Assembly-CSharp.dll!\nMay take a while.\nRestart when done.",
                "ModLoader.Core.Managed",
                0
            );

            File.Copy(asmPath, asmPath + ".backup", overwrite: true);

            foreach (var module in asm.Modules)
            {
                foreach (var type in module.Types)
                {
                    if (!IsGameScript(type))
                        continue;

                    API.Log("ModLoader", $"Patching {type.FullName}");
                    PatchType(type);
                }
            }

            InjectMarker(asm);
            asm.Write();

            MessageBoxA(
                IntPtr.Zero,
                "Patched Assembly-CSharp!\nRestart to ensure mods work.",
                "ModLoader.Core.Managed",
                0
            );
        }

        // ---------------- SAFE FILTER ----------------

        private static bool IsGameScript(TypeDefinition type)
        {
            // Global namespace only (Unity game scripts)
            if (!string.IsNullOrEmpty(type.Namespace))
                return false;

            // Skip compiler / generated junk
            if (type.Name.StartsWith("<"))
                return false;

            // Never touch interfaces or enums
            if (type.IsInterface || type.IsEnum)
                return false;

            // Must be MonoBehaviour or ScriptableObject
            return InheritsFrom(type, "UnityEngine.MonoBehaviour") ||
                   InheritsFrom(type, "UnityEngine.ScriptableObject");
        }

        private static bool InheritsFrom(TypeDefinition type, string baseType)
        {
            while (type != null)
            {
                if (type.BaseType?.FullName == baseType)
                    return true;

                try
                {
                    type = type.BaseType?.Resolve();
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        // ---------------- PATCHING ----------------

        private static void PatchType(TypeDefinition type)
        {
            if (type.IsNotPublic && !type.IsNested)
                type.IsPublic = true;

            foreach (var method in type.Methods)
            {
                if (method.IsPrivate)
                {
                    method.IsPrivate = false;
                    method.IsPublic = true;
                }
            }

            foreach (var prop in type.Properties)
            {
                if (prop.GetMethod?.IsPrivate == true)
                {
                    prop.GetMethod.IsPrivate = false;
                    prop.GetMethod.IsPublic = true;
                }

                if (prop.SetMethod?.IsPrivate == true)
                {
                    prop.SetMethod.IsPrivate = false;
                    prop.SetMethod.IsPublic = true;
                }
            }

            foreach (var nested in type.NestedTypes)
            {
                if (IsGameScript(nested))
                    PatchType(nested);
            }
        }

        // ---------------- MARKER ----------------

        private static void InjectMarker(AssemblyDefinition asm)
        {
            var attrType = asm.MainModule.ImportReference(typeof(ModLoaderPatchedAttribute));
            var ctor = attrType.Resolve()
                .Methods.First(m => m.IsConstructor && m.Parameters.Count == 1);

            var attr = new CustomAttribute(asm.MainModule.ImportReference(ctor));
            attr.ConstructorArguments.Add(
                new CustomAttributeArgument(
                    asm.MainModule.TypeSystem.String,
                    "Iron Lung"
                )
            );

            asm.CustomAttributes.Add(attr);
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    internal class ModLoaderPatchedAttribute : Attribute
    {
        public string Game;
        public ModLoaderPatchedAttribute(string game) => Game = game;
    }
}
