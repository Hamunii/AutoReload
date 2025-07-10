using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;

namespace AutoReload;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    static ConfigEntry<bool> QuietMode { get; set; } = null!;
    static readonly string dumpedAssembliesPath = Path.Combine(
        Paths.BepInExRootPath,
        "AutoReloadDumpedAssemblies"
    );
    static DefaultAssemblyResolver defaultResolver = null!;
    static FileSystemWatcher fileSystemWatcher = null!;
    static Dictionary<string, string> pathToId = [];

    private void Awake()
    {
        defaultResolver = new DefaultAssemblyResolver();
        defaultResolver.AddSearchDirectory(Paths.PluginPath);
        defaultResolver.AddSearchDirectory(Paths.ManagedPath);
        defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

        QuietMode = Config.Bind(
            "General",
            "QuietMode",
            false,
            new ConfigDescription("Disable all logging except for error messages.")
        );

        if (Directory.Exists(dumpedAssembliesPath))
            Directory.Delete(dumpedAssembliesPath, true);

        StartFileSystemWatcher();

        Logger.LogInfo($"Plugin {Name} is loaded!");
    }

    private void StartFileSystemWatcher()
    {
        fileSystemWatcher = new(Paths.PluginPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite,
            Filter = "*.dll",
        };
        fileSystemWatcher.Changed += FileChangedEventHandler;
        fileSystemWatcher.Created += FileChangedEventHandler;
        fileSystemWatcher.EnableRaisingEvents = true;
    }

    private void FileChangedEventHandler(object sender, FileSystemEventArgs args)
    {
        if (!QuietMode.Value)
            Logger.LogInfo($"File '{Path.GetFileName(args.Name)}' changed.");

        LoadPlugin(args.FullPath);
    }

    void UnloadPlugin(string path)
    {
        if (!pathToId.TryGetValue(path, out var id))
            return;

        if (!Chainloader.PluginInfos.TryGetValue(id, out var plugin))
            return;

        Chainloader.PluginInfos.Remove(id);
        Destroy(plugin.Instance);
    }

    void LoadPlugin(string path)
    {
        if (!File.Exists(path))
        {
            Logger.LogWarning($"File at '{path}' has been deleted.");
            return;
        }

        using var dll = AssemblyDefinition.ReadAssembly(
            path,
            new ReaderParameters { AssemblyResolver = defaultResolver, ReadSymbols = true }
        );
        dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

        // Dump assembly & load it from disk.
        if (!Directory.Exists(dumpedAssembliesPath))
            Directory.CreateDirectory(dumpedAssembliesPath);

        string assemblyDumpPath = Path.Combine(
            dumpedAssembliesPath,
            dll.Name.Name + Path.GetExtension(dll.MainModule.Name)
        );

        using (FileStream outFileStream = new(assemblyDumpPath, FileMode.Create))
        {
            dll.Write(outFileStream, new WriterParameters() { WriteSymbols = true });
        }

        Assembly ass = Assembly.LoadFile(assemblyDumpPath);
        if (!QuietMode.Value)
            Logger.Log(LogLevel.Info, $"Loaded dumped Assembly from '{assemblyDumpPath}'");

        foreach (Type type in GetTypesSafe(ass))
        {
            try
            {
                if (!typeof(BaseUnityPlugin).IsAssignableFrom(type))
                    continue;

                var metadata = MetadataHelper.GetMetadata(type);
                if (metadata == null)
                    continue;

                pathToId[path] = metadata.GUID;
                UnloadPlugin(path);

                var typeDefinition = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                var pluginInfo = Chainloader.ToPluginInfo(typeDefinition);

                StartCoroutine(
                    DelayAction(() =>
                    {
                        if (!QuietMode.Value)
                        {
                            Logger.Log(
                                LogLevel.Info,
                                $"Loading [{metadata.Name} {metadata.Version}]"
                            );
                        }

                        try
                        {
                            // Need to add to PluginInfos first because BaseUnityPlugin constructor (called by AddComponent below)
                            // looks in PluginInfos for an existing PluginInfo and uses it instead of creating a new one.
                            Chainloader.PluginInfos[metadata.GUID] = pluginInfo;

                            var instance = (BaseUnityPlugin)gameObject.AddComponent(type);

                            // Fill in properties that are normally set by Chainloader
                            var tv = Traverse.Create(pluginInfo);
                            tv.Property<BaseUnityPlugin>(nameof(pluginInfo.Instance)).Value =
                                instance;
                            // Loading the assembly from memory causes Location to be lost
                            tv.Property<string>(nameof(pluginInfo.Location)).Value = path;
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(
                                $"Failed to load plugin {metadata.GUID} because of exception: {e}"
                            );
                            Chainloader.PluginInfos.Remove(metadata.GUID);
                        }
                    })
                );
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to load plugin {type.Name} because of exception: {e}");
            }
        }
    }

    private IEnumerable<Type> GetTypesSafe(Assembly ass)
    {
        try
        {
            return ass.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(x => x is not null);
        }
    }

    private IEnumerator DelayAction(Action action)
    {
        yield return null;
        action();
    }
}
