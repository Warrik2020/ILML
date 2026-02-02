# ILML
ILML is a Mod Loader for the game Iron Lung.

# ILML ModLoader.Core.Managed

A high-level managed API for ILML that provides:

* Logging utilities
* Main-thread execution helpers
* Update hooks
* Unity object and scene utilities
* Reflection helpers
* Runtime method hooking support

This module is intended to be used by mods loaded into Unity games via ILML.

---

## 🔹 Table of Contents

1. [Installation](#installation)
2. [Core API](#core-api)

   * Logging
   * Main-Thread Execution
   * Update Callbacks
   * Unity Helpers
   * Reflection Helpers
   * Scene Load Hooks
3. [Runtime Hooking System](#runtime-hooking-system)

   * HookAttribute
   * HookInstaller
   * Calling Original Methods
4. [Examples](#examples)

---

## 🚀 Installation

Include the **ModLoader.Core.Managed** assembly in your mod project references.

At runtime, the ModLoader bootstrapper will call:

```csharp
HookInstaller.RegisterHooksFromAssembly(yourModAssembly);
```

This scans your assembly for hooks and installs them.

---

## 🔹 Core API

### 🪵 Logging

Simple logging to `modloader.log` in the game directory:

```csharp
API.Log("MyMod", "Loaded successfully!");
API.Log(this, "Something happened!");
```

Logs include timestamps and mod names for easier debugging.

---

### 🧵 Main-Thread Execution

Unity requires most engine calls on the main thread.

Queue actions to execute on the next frame:

```csharp
API.RunOnMainThread(() => {
    // Unity API calls here
});
```

This is processed in the ModLoader’s update loop.

---

### 🔁 Update Callbacks

Mods can register per-frame callbacks:

```csharp
API.RegisterUpdate(MyUpdate);
API.UnregisterUpdate(MyUpdate);

void MyUpdate() {
    // called every frame
}
```

---

### 🧱 Unity Helpers

Spawn basic game objects:

```csharp
var cube = API.SpawnPrimitive(PrimitiveType.Cube, position, rotation);
var instance = API.SpawnPrefab(myPrefab, pos, rot);
```

Find objects:

```csharp
var camera = API.FindObject<Camera>();
var objByName = API.FindByName("Player");
```

> **Note:** Spawning happens on the main thread; returned objects may be null until created.

---

### 🔍 Reflection Helpers

Access private fields or call private methods:

```csharp
var value = API.GetPrivateField(instance, "_health");
API.SetPrivateField(instance, "_health", 100);
API.CallPrivateMethod(instance, "ResetState");
```

Resolve types from game assemblies:

```csharp
Type t = API.GetTypeFromGame("Namespace.ClassName");
```

---

### 🎬 Scene Load Hooks

Run a callback when a new scene loads:

```csharp
API.OnSceneLoaded(() => {
    // Scene loaded logic
});
```

The callback gets invoked with the loaded `Scene` object.

---

## 🪝 Runtime Hooking System

This system lets mods replace or augment game methods using simple attributes.

---

### 🧷 HookAttribute

Define a static method with:

```csharp
[Hook(typeof(TargetClass), "MethodName")]
public static void MyHook(...) {
    // your hook logic
}
```

This instructs the mod loader to patch `TargetClass.MethodName` at runtime.

---

### 🔧 HookInstaller

Scans an assembly for methods marked with `[Hook]` and installs them.

Example:

```csharp
HookInstaller.RegisterHooksFromAssembly(Assembly.GetExecutingAssembly());
```

It looks for any method with the `HookAttribute` and replaces the target function body with your hook.

⚠️ If the target or method doesn’t exist, an error is logged.

---

### 🧠 Calling the Original Method

Inside a hook you might want to invoke the original implementation:

```csharp
Hooks.Original(self, "MethodName", arg1, arg2);
```

Or with a return value:

```csharp
return Hooks.Original<ReturnType>(self, "MethodName", arg);
```

This uses an internal registry of saved original method pointers.

---

## 🛠 Example: Hooking and Logging

```csharp
[Hook(typeof(PlayerController), "TakeDamage")]
public static void OnTakeDamage(PlayerController self, int amount) {
    API.Log("MyMod", $"Player taking {amount} damage");
    
    // Call original
    Hooks.Original(self, "TakeDamage", amount);
}
```

---

## Note
Don't use hooks yet, they are severely broken
