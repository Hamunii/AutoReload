# AutoReload

A BepInEx 5 plugin that automatically reloads plugins by watching `BepInEx/plugins/` for file changes.

## Implementing Support

To make your plugin work with reloading, it must implement the `OnDestroy` method in the main plugin class to clean up after itself. An example of a good plugin:

```cs
[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    void Awake()
    {
        // Apply all hooks
        MonoDetourManager.InvokeHookInitializers(typeof(Plugin).Assembly);
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    void OnDestroy()
    {
        // Dispose all hooks
        DefaultMonoDetourManager.Instance.Dispose();
        Log.LogInfo($"Plugin {Name} unloaded!");
    }
}
```

> [!TIP]  
> The above plugin uses [Hamunii.BepInEx.AutoPlugin](<https://github.com/Hamunii/BepInEx.AutoPlugin>) for the `[BepInAutoPlugin]` attribute.

## Credits

This is a hard fork of [BepInEx.Debug ScriptEngine](<https://github.com/BepInEx/BepInEx.Debug#scriptengine>) which dramatically changes how the plugin works.

Main changes are:

- `scripts/` directory is gone
  - assemblies are reloaded from `plugins/`
  - `LoadOnStart` option is gone
- `FileSystemWatcher` option is gone, always enabled
  - `AutoReloadDelay` option is gone
  - `ReloadKey` option is gone
  - `IncludeSubdirectories` option is gone, always enabled
  - Only "file changed" and "file renamed" events are listened to
- `DumpAssemblies` option is gone, always enabled
  - This is because you can get debug symbols this way
