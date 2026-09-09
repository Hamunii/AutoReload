# AutoReload

A BepInEx 5 plugin that automatically reloads plugins by watching `BepInEx/plugins/` for file changes.

> [!WARNING]  
> I have been informed that the `FileSystemWatcher` may work terribly on Windows. This plugin relies on `FileSystemWatcher` working properly. If you are having issues with it, try running under Linux.
>
> Also, if BepInEx is not configured to hide plugin manager gameObject, this plugin will not work properly. Set `HideManagerGameObject` to `true` in `BepInEx.cfg` if this plugin is not working. This setting is enabled by default in BepInExPack for PEAK.

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
> The above plugin uses [Hamunii.BepInEx.AutoPlugin](<https://github.com/Hamunii/BepInEx.AutoPlugin>) for the `[BepInAutoPlugin]` attribute, and [MonoDetour](<https://github.com/MonoDetour/MonoDetour>) for hooking.

## Limitations

### Fixable Limitations

- If mod A and B are both reloaded, and A depends on B, A will reference the first ever loaded version of B.
  - AutoReload would need to detect that A depends B, and rewrite A's references to B to reference the latest B assembly before A is reloaded.
  - Currently this can be worked around by using [ILRepack](<https://github.com/gluck/il-repack>) or [ILRepack.Lib.MSBuild.Task](<https://github.com/ravibpatel/ILRepack.Lib.MSBuild.Task>).
- Reloaded plugins don't retain any state.

### Runtime Limitations

- Old assemblies are never actually unloaded because it's impossible.

## Alternatives

- <https://github.com/xiaoxiao921/UnityHotReload/>
  - UnityHotReload preserves the existing runtime state by redirecting all active references to the original type definitions.
- <https://github.com/BepInEx/BepInEx.Debug#scriptengine>
  - AutoReload is a hard fork of this. For comparison, see the Credits below.

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
