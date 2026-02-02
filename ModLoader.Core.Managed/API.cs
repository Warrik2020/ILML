using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModLoader.Core.Managed
{
    public static class API
    {
        internal static void Log(string modName, string msg)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logLine = $"[{timestamp}] [{modName}] {msg}\n";
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modloader.log"), logLine);
        }

        public static void Log(object mod, string msg)
        {
            Log(mod.GetType().Name, msg);
        }

        // --- Main thread executor ---
        // Enqueue an action to run on Unity main thread (executed in ModBootstrapper.Update)
        private static readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

        public static void RunOnMainThread(Action a)
        {
            if (a == null) return;
            mainThreadQueue.Enqueue(a);
        }

        // internal: called by bootstrapper
        internal static void DrainMainThreadQueue()
        {
            // run all queued actions
            while (mainThreadQueue.TryDequeue(out var a))
            {
                try { a(); }
                catch (Exception e) { Log("ModLoader", $"Exception in main-thread action: {e}"); }
            }
        }

        // --- Update callbacks (alternate to IMod.OnUpdate) ---
        private static readonly List<Action> updateCallbacks = new List<Action>();
        private static readonly object updateLock = new object();

        public static void RegisterUpdate(Action callback)
        {
            if (callback == null) return;
            lock (updateLock) { if (!updateCallbacks.Contains(callback)) updateCallbacks.Add(callback); }
        }

        public static void UnregisterUpdate(Action callback)
        {
            if (callback == null) return;
            lock (updateLock) { updateCallbacks.Remove(callback); }
        }

        internal static void RunUpdateCallbacks()
        {
            lock (updateLock)
            {
                foreach (var cb in updateCallbacks.ToArray())
                {
                    try { cb(); }
                    catch (Exception e) { Log("ModLoader", $"Exception in update callback: {e}"); }
                }
            }
        }

        // --- Unity helpers ---
        public static GameObject SpawnPrimitive(PrimitiveType type, Vector3 pos, Quaternion rot)
        {
            GameObject go = null;
            RunOnMainThread(() => { go = GameObject.CreatePrimitive(type); go.transform.position = pos; go.transform.rotation = rot; });
            // We return null here because creation is async to main thread; caller should use a callback instead.
            return go;
        }

        public static GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            GameObject go = null;
            RunOnMainThread(() => { go = UnityEngine.Object.Instantiate(prefab, position, rotation) as GameObject; });
            return go;
        }

        public static T FindObject<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectOfType<T>();
        }

        public static GameObject FindByName(string name)
        {
            return GameObject.Find(name);
        }

        // --- Reflection helpers for Assembly-CSharp types ---
        public static Type GetTypeFromGame(string fullTypeName)
        {
            // try Assembly-CSharp first
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (asm != null)
            {
                var t = asm.GetType(fullTypeName, false, false);
                if (t != null) return t;
            }
            // fallback search
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(fullTypeName, false, false);
                if (t != null) return t;
            }
            return null;
        }

        public static object GetPrivateField(object instance, string fieldName)
        {
            if (instance == null) return null;
            var t = instance.GetType();
            var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f?.GetValue(instance);
        }

        public static bool SetPrivateField(object instance, string fieldName, object value)
        {
            if (instance == null) return false;
            var t = instance.GetType();
            var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return false;
            f.SetValue(instance, value);
            return true;
        }

        public static object CallPrivateMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null) return null;
            var t = instance.GetType();
            var m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return m?.Invoke(instance, args);
        }

        // --- Scene Load Hooks ---
        private static readonly List<Action<Scene, LoadSceneMode>> sceneLoadedCallbacks = new List<Action<Scene, LoadSceneMode>>();

        /// <summary>
        /// Register a callback to run when any scene is loaded.
        /// The Scene parameter contains name, buildIndex, etc.
        /// </summary>
        public static void OnSceneLoaded(Action<Scene> callback)
        {
            if (callback == null) return;

            if (sceneLoadedCallbacks.Count == 0)
            {
                // First registration, subscribe to Unity event
                SceneManager.sceneLoaded += SceneLoadedHandler;
            }

            sceneLoadedCallbacks.Add((scene, mode) => callback(scene));
        }

        private static void SceneLoadedHandler(Scene scene, LoadSceneMode mode)
        {
            foreach (var cb in sceneLoadedCallbacks)
            {
                try { cb(scene, mode); }
                catch (Exception e) { Log("ModLoader", $"Exception in scene loaded callback: {e}"); }
            }
        }
    }
}
