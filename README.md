# Mini Motorways Mod Loader

A mod loader for Mini Motorways that lets you inject custom code into the game at runtime.

### ModLoader (`/test/`)
main thing, It's a DLL that gets loaded when the game starts up and scans the `Mods/` folder for any mod DLLs you've dropped in. When it finds one, it looks for classes implementing the `IMod` interface and calls their `OnLoad()` method.


Mods need to implement:
- `Name`, `Version`, `Author` - basic metadata
- `OnLoad()` - called when the mod is initialized
- `OnUnload()` - called when the game shuts down

### DllPatcher (`/test/DllPatcher/`)
Unity games don't let you run arbitrary code. You need to patch the game's assemblies to actually call your mod loader. That's what this tool does.

It uses Mono.Cecil to inject a call to `ModLoaderEntry.Main()` right at the start of the game's `App.Start()` method. The whole thing is wrapped in a try-catch so if the mod loader fails for some reason, the game itself doesn't crash.

run it like:
```
DllPatcher "/path/to/Mini Motorways_Data/Managed"
```

It'll back up `App.dll` before patching it so you can always restore to vanilla if needed.

### ModManager (`/ModManager/`)
This is actually a mod itself, It adds a "Mods" button to the main menu that lets you:
- See all detected mod DLLs
- Enable/disable mods.
- Apply changes with a game restart

The UI hooks into the game's existing button system so it doesn't look super out of place. Disabled mods get a `.disabled` suffix added to their DLL path, which the loader skips over on next startup.

## Installation
# everything is already done, just goto the releases and download the dlls there
1. Run DllPatcher against your game's Managed folder
2. Copy `ModLoader.dll` to the Managed folder
3. Create a `Mods/` folder inside the game's data folder
4. Drop mod DLLs into subfolders under `Mods/` (e.g., `Mods/MyCoolMod/MyCoolMod.dll`)
5. Launch the game

## Building

you'll need:
- .NET Framework 4.5.2 (to match Unity's runtime)
- References to UnityEngine.dll and other game assemblies from the Managed folder

the projects are set up to reference the assemblies from a local game install. You may need to update the paths in the `.csproj` files.

## For Mod Developers

### Making Your Mod Configurable

If you want users to be able to tweak settings in your mod from the ModManager UI, you need to implement `IConfigurableMod`. The interface pretty simple though looks like ASS! just copy the interface and helper classes from `ModManager/IConfigurableMod.cs` into your own project (you can't reference ModManager.dll directly due to how assemblies work).

Your mod class needs to implement these:
- `GetConfigEntries()` - returns a list of settings your mod exposes
- `OnConfigChanged(string key, object value)` - called when the user saves a setting
- `LoadConfig(Dictionary<string, string> savedValues)` - called on startup with any saved values

i.e.:

```csharp
public class MyMod : IMod, IConfigurableMod
{
    private List<ModConfigEntry> _configEntries = new List<ModConfigEntry>();
    private bool _coolFeatureEnabled = true;
    
    public void Initialize()
    {
        _configEntries.Add(ModConfigEntry.Bool("casablanca", "Enable Cool Feature", true));
    }
    
    public List<ModConfigEntry> GetConfigEntries() => _configEntries;
    
    public void OnConfigChanged(string key, object value)
    {
        if (key == "casablanca") _coolFeatureEnabled = (bool)value;
    }
    
    public void LoadConfig(Dictionary<string, string> savedValues)
    {
        foreach (var entry in _configEntries)
        {
            if (savedValues.TryGetValue(entry.Key, out string saved))
            {
                entry.CurrentValue = bool.Parse(saved);
                OnConfigChanged(entry.Key, entry.CurrentValue);
            }
        }
    }
}
```



ModManager handles all the saving/loading to `ModManager.json` automatically, you just need to react to the callbacks.

## API Reference

### Game Access
get references to game objects through the `Get` static class:

```csharp
using Motorways;

// Core game references
City city = Get.City;                    // Current city/map
MotorwaysGame game = Get.Game;           // Main game instance
ISimulation simulation = game.Simulation; // Simulation system
IScope scope = city.Scope;               // Dependency injection scope
```

### Key Models (via `scope.Get<T>()`)

| Model | Description |
|-------|-------------|
| `ScoreModel` | Access player score (`.Score`, `.AddScore()`) |
| `ClockModel` | Game time (`.Time`, `.Day`, `.ExpansionDay`, `.ExpansionTime`) |
| `DemandModel` | Building spawn system (`.spawnScale`, `.extraDemand`) |
| `CameraView` | Camera control (`.EndSize` for zoom level) |
| `GameBehaviourModel` | Game rules (`.CanGameOver` for godmode) |
| `DestinationModel` | Individual buildings (`.IsUpgraded`, `.demandLevelUpTime`) |

### SimulationConstantsData Fields
Modify via reflection to alter game mechanics:

| Field | Default | Description |
|-------|---------|-------------|
| `MaxOvercrowdTime` | 90 | Seconds before pin timer triggers game over |
| `FailedHouseSpawnCooldown` | varies | Delay between house spawn attempts |
| `FailedDestinationRetryDelay` | varies | Delay between building spawn attempts |
| `MaxFailedBuildingSpawnsBeforeIgnoringWeights` | varies | Spawns before weights ignored |
| `EndlessSpawnRampMultiplier` | 1.0 | Multiplier for spawn rate scaling |

### GameRules Properties
Access via `city.Rules`:

| Property | Type | Description |
|----------|------|-------------|
| `SpawnRampMultiplier` | Fix64 | Controls building spawn speed scaling |
| `CanDestinationsOvercrowd` | bool | Whether pins can fail |

### Example: Accessing Game State

```csharp
public class MyMod : MonoBehaviour
{
    private City _city;
    private ScoreModel _scoreModel;
    
    void Update()
    {
        if (_city == null && Get.City != null)
        {
            _city = Get.City;
            _scoreModel = _city.Scope.Get<ScoreModel>();
        }
        
        if (_scoreModel != null)
        {
            int currentScore = _scoreModel.Score;
            // uhhh something something with score
        }
    }
}
```

### Example: Modifying Game Constants via Reflection

```csharp
// Get SimulationConstantsData
var constType = Type.GetType("Motorways.SimulationConstantsData, App");
var scopeType = _city.Scope.GetType();
var getMethod = scopeType.GetMethods()
    .First(m => m.Name == "Get" && m.IsGenericMethod && m.GetParameters().Length == 0);
var genericGet = getMethod.MakeGenericMethod(constType);
object constants = genericGet.Invoke(_city.Scope, null);

// Modify a field
var field = constType.GetField("MaxOvercrowdTime", BindingFlags.Public | BindingFlags.Instance);
field.SetValue(constants, (Fix64)999999);
```

###  kind of important notes
- use `MonoBehaviour` for mods that need `Update()` loop
- game models use `Fix64` (fixed-point) numbers, cast with `(Fix64)value`
- create a `DontDestroyOnLoad` GameObject to persist across scenes
- check `Get.City != null` before accessing game state
- use `Debug.Log("[YourMod] message")` for console output

## Notes
- tested on Linux (Proton) but should work on Windows too