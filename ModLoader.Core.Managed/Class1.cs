using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace ModLoader.Core.Managed
{
    // ----------------- Entry -----------------
    public static class Entry
    {
        public static void Initialize()
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modloader.log"), "");

            API.Log("ModLoader", "Initialized");

            AssemblyCSharpPatcher.PatchIfNeeded();
            
            ModBootstrapper.Create();
        }
    }

    // ----------------- Mod Interface -----------------
    public interface IMod
    {
        string Name { get; }
        string Version { get; }

        void OnLoad();
        void OnUpdate();
        void OnUnload();
    }

    // ----------------- Bootstrapper -----------------
    public class ModBootstrapper : MonoBehaviour
    {
        public static void Create()
        {
            var go = new GameObject("ModLoader");
            DontDestroyOnLoad(go);
            go.AddComponent<ModBootstrapper>();
        }

        void Start() => ModManager.LoadMods();
        void Update()
        {
            // Run any actions queued to run on the Unity main thread
            API.DrainMainThreadQueue();

            // Run registered update callbacks
            API.RunUpdateCallbacks();

            // Update mods
            ModManager.UpdateMods();
        }
        void OnDestroy() => ModManager.UnloadMods();
    }

    // ----------------- Mod Manager -----------------
    internal static class ModManager
    {
        private static readonly List<IMod> mods = new List<IMod>();

        public static void LoadMods()
        {
            string modsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods");
            Directory.CreateDirectory(modsDir);

            foreach (var dll in Directory.GetFiles(modsDir, "*.dll"))
            {
                try
                {
                    var asm = Assembly.Load(File.ReadAllBytes(dll));

                    // --- REGISTER HOOKS IN THIS ASSEMBLY ---
                    HookInstaller.RegisterHooksFromAssembly(asm);

                    // --- LOAD MODS ---
                    foreach (var type in asm.GetTypes())
                    {
                        if (!typeof(IMod).IsAssignableFrom(type) || type.IsAbstract)
                            continue;

                        var mod = (IMod)Activator.CreateInstance(type);
                        mod.OnLoad();
                        mods.Add(mod);

                        API.Log(mod.Name, $"Loaded {mod.Version}");
                    }
                }
                catch (Exception e)
                {
                    API.Log(Path.GetFileName(dll), $"Failed to load mod: {e}");
                }
            }
        }

        public static void UpdateMods()
        {
            foreach (var mod in mods)
                mod.OnUpdate();
        }

        public static void UnloadMods()
        {
            foreach (var mod in mods)
                mod.OnUnload();
        }
    }
}
