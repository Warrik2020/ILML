// dllmain_debug.cpp
#include <windows.h>
#include <thread>
#include <string>
#include <sstream>

// Minimal mono typedefs
using MonoDomain = void*;
using MonoAssembly = void*;
using MonoImage = void*;
using MonoClass = void*;
using MonoMethod = void*;

// Function pointers
static MonoDomain* (*mono_get_root_domain)();
static void          (*mono_thread_attach)(MonoDomain*);
static MonoAssembly* (*mono_domain_assembly_open)(MonoDomain*, const char*);
static MonoImage* (*mono_assembly_get_image)(MonoAssembly*);
static MonoClass* (*mono_class_from_name)(MonoImage*, const char*, const char*);
static MonoMethod* (*mono_class_get_method_from_name)(MonoClass*, const char*, int);
static void* (*mono_runtime_invoke)(MonoMethod*, void*, void**, void**);

static void DebugMsg(const char* fmt, ...)
{
    char buf[1024];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf_s(buf, sizeof(buf), _TRUNCATE, fmt, ap);
    va_end(ap);
    MessageBoxA(nullptr, buf, "ModLoader Debug", MB_OK | MB_SYSTEMMODAL);
}

static void LoadMonoFunctions()
{
    HMODULE monoModule = GetModuleHandleW(L"mono-2.0-bdwgc.dll");
    if (!monoModule)
    {
        DebugMsg("mono-2.0-bdwgc.dll not loaded yet.");
        return;
    }

    mono_get_root_domain =
        (decltype(mono_get_root_domain))GetProcAddress(monoModule, "mono_get_root_domain");
    mono_thread_attach =
        (decltype(mono_thread_attach))GetProcAddress(monoModule, "mono_thread_attach");
    mono_domain_assembly_open =
        (decltype(mono_domain_assembly_open))GetProcAddress(monoModule, "mono_domain_assembly_open");
    mono_assembly_get_image =
        (decltype(mono_assembly_get_image))GetProcAddress(monoModule, "mono_assembly_get_image");
    mono_class_from_name =
        (decltype(mono_class_from_name))GetProcAddress(monoModule, "mono_class_from_name");
    mono_class_get_method_from_name =
        (decltype(mono_class_get_method_from_name))GetProcAddress(monoModule, "mono_class_get_method_from_name");
    mono_runtime_invoke =
        (decltype(mono_runtime_invoke))GetProcAddress(monoModule, "mono_runtime_invoke");

    //DebugMsg("Loaded mono function pointers.");
}

static void LoaderThread()
{
    //DebugMsg("LoaderThread started.");
    Sleep(3000);

    // Wait for Unity to load mono
    while (!GetModuleHandleW(L"mono-2.0-bdwgc.dll"))
        Sleep(100);

    //DebugMsg("mono-2.0-bdwgc.dll present.");

    LoadMonoFunctions();
    if (!mono_get_root_domain)
    {
        DebugMsg("mono_get_root_domain pointer missing.");
        return;
    }

    // Wait for root domain to exist
    MonoDomain* domain = nullptr;
    int tries = 0;
    while (!(domain = mono_get_root_domain()))
    {
        Sleep(100);
        if (++tries == 200) { DebugMsg("Timed out waiting for mono root domain."); return; }
    }
    //DebugMsg("mono root domain available.");

    // Attach
    mono_thread_attach(domain);
    //DebugMsg("Thread attached to mono domain.");

    // Now attempt to open our managed assembly
    MonoAssembly* modLoaderAssembly = nullptr;
    tries = 0;
    while (!(modLoaderAssembly = mono_domain_assembly_open(domain, "ModLoader.Core.Managed.dll")))
    {
        Sleep(100);
        if (++tries == 100) { DebugMsg("Timed out waiting for ModLoader.Core.Managed.dll (looked in Mono load paths)."); return; }
    }
    //DebugMsg("ModLoader.Core.Managed.dll opened.");

    // Image
    MonoImage* image = mono_assembly_get_image(modLoaderAssembly);
    if (!image) { DebugMsg("mono_assembly_get_image returned NULL."); return; }
    //DebugMsg("Got image from assembly.");

    // Try class lookup (match namespace EXACTLY)
    const char* namespace_name = "ModLoader.Core.Managed"; // adjust if you changed namespace
    const char* class_name = "Entry";

    MonoClass* klass = mono_class_from_name(image, namespace_name, class_name);
    if (!klass) { DebugMsg("mono_class_from_name returned NULL. Check namespace/class name."); return; }
    //DebugMsg("Found class %s.%s", namespace_name, class_name);

    // Try method lookup
    MonoMethod* init = mono_class_get_method_from_name(klass, "Initialize", 0);
    if (!init) { DebugMsg("mono_class_get_method_from_name returned NULL for Initialize()."); return; }
    //DebugMsg("Found Initialize() method.");

    // Finally invoke
    //DebugMsg("Invoking Initialize() now...");
    mono_runtime_invoke(init, nullptr, nullptr, nullptr);
    //DebugMsg("Initialize() returned (if it didn't crash).");
}

// DllMain: spawn thread, no heavy work inside DllMain itself
BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hModule);
        std::thread(LoaderThread).detach();
    }
    return TRUE;
}
