# 🔊 TimeStretch Mod — Audio Transformer for SPT

## 🧩 What is this mod?

**TimeStretch** is a mod for **SPT** that dynamically modifies the audio of firearms in-game. It allows fine control over how gun sounds are transformed — time-stretching — based on weapon configuration. Adjuste Body Audio Volume all conf F12

This mod is **client-side** and uses **BepInEx** + **Harmony** to hook into Tarkov’s internal audio systems, identify weapons being fired, and replace `AudioClip` objects in memory.

---
## 🛑 Need !!!
- this mod work with bundle update. Execute **ChangeMetaDataBundle.exe**  ``BepInEx\config\BundleUpdate`` Selection 'Y'
---
---
## 📚 Log IF ONLY NEED 📚
-  **debug.cfg** - **batch_log.txt**  ==> ``BepInEx\plugins\TimeStretch`` true = debug mod on
---
## 🎯 Key Features

- 🔁 **Dynamic sound transformation** based on weapon ID and clip name.
- 🎧 **Clip-by-clip replacement** for each weapon using time-stretched audio.
- ⚙️ Configurable **tempo modifiers** (per clip, per weapon).
- 🚀 Performance & Thread Model
- 📦 Auto-hook system only activates on **your player** during firing.
- 💬 Centralized logging system with per-clip and per-weapon diagnostics.
- 🔒 Intelligent detection of modded vs vanilla weapons via a JSONEmbedded.

---

## ⚙️ How It Works (Simplified Flow)

### 1. **Weapon Tracking**
When the local player equips a new weapon, the mod:
- Hooks `Player.set_HandsController` via `PatchTrackEquippedWeapon`
- Detects the current weapon’s `TemplateId`
- Checks if the weapon is marked as "modifiable" (`mod == true` from a JSONEmbedded)
- If valid, starts a background thread to enqueue the weapon for audio processing

### 2. **Clip Discovery & Transformation**
Once a weapon is enqueued:
- `AudioClipModifier.GetTransformableClips()` identifies all `AudioClip` objects in memory matching the weapon's known clip names.
- For each clip:
    - Calculates a `tempo` based on config values
    - Clamps tempo within allowed range
    - Skips clips with 0% change
    - Schedules an async transformation via `AudioClipTransformer.TransformAsync()`

### 3. **Replacement Handling**
- After transformation:
    - The transformed clip is **cached** and **registered** to replace the original
    - clips are NEVER !!!!!! overwritten in memory
    - replaced virtually by mapping names in a lookup dictionary

### 4. **SoundBank Hook**
During gameplay:
- When a weapon fires, `SoundBank.PickClipsByDistance()` is hooked by `PatchPickByDistance`
- If the player is local and a weapon is firing:
    - The mod checks if a transformed clip is available for replacement
    - If found, it replaces the original with the modified version
  
### 5. **PatchBodySoundVolume**

This patch dynamically adjusts the **volume of body-related sounds** (footsteps, gear, jumps, etc.) **only for the local player**, based on the clip prefix and user-configured volume sliders.

It works by:
- Detecting when body sounds are played (`walk_`, `gear_`, etc.)
- Capturing clip names during `PickClipsByDistance`
- Applying custom volume via `BetterSource.BaseVolume` during `SoundBank.Play`
- Ensuring modifications are only applied once per `BetterSource` instance

This patch has **no impact on weapon sounds**, and only filters "movement/gear" sounds for immersive tuning.

---
## 🚀 Performance & Thread Model

The mod is carefully designed to maintain optimal performance and prevent any stutter or lag in-game. It relies on intelligent multithreading and safe memory access patterns.

### 🔄 Thread Usage

| Operation                               | Thread              |
|-----------------------------------------|---------------------|
| `AudioClip` detection in memory         | Main Thread (Unity safe API)  
| Weapon detection (`set_HandsController`) | Main Thread   
| Sound replacement (during fire)         | Main Thread (hooked from Unity)  
| other proces. application traitement    | Background Thread  

Only **non-blocking**, **CPU-light** operations are allowed on the Unity main thread. All heavier processing (transformation, tempo calculation) is performed off-thread using tasks and coroutines.

---
## 🔍 Hook System
```
[set_HandsController]
│               └──> player local
▼
[CurrentPlayer] (PatchTrackEquippedWeapon)
│               └──> player local
▼
[method_59] (PatchFireArmController59)
│                └──> player local
▼
[FireBullet] (PatchFireBulletPlayerTracker)
└──> define IsLocalPlayerSound (ThreadSafe)
▼
[PickClipsByDistance]
└──> use IsLocalPlayerSound
```
---

### 🧠 Caching System

To avoid redundant processing and ensure real-time responsiveness, several cache layers are implemented:

- **`ConcurrentDictionary<(weaponId, AudioClip), AudioClip>`**  
  Maps the original clip to its transformed version per weapon.  
  → Used to determine if a clip has already been processed.

- **`Dictionary<string, string> LocalClipMap`**  
  Local, one-weapon-at-a-time mapping of `originalName → transformedName`.  
  → Used at runtime to quickly find transformed versions based on name.

- **`ConcurrentDictionary<string, AudioClip> AllClipsByName`**  
  Global cache of all `AudioClip` objects loaded in memory, by name.  
  → Used to resolve AudioClips from names without rescanning Unity memory.

- **`HashSet<string> ProcessedWeapons`**  
  Stores already processed weapon IDs to prevent duplicate transformation.

- **`ConcurrentQueue<string> WeaponQueue`**  
  Thread-safe queue of weapons to be transformed by the background worker.

- **`Dictionary<string, bool> HookPermissionByWeaponId`**  
  Determines which weapons are allowed to receive audio hooks (based on `mod == true` from JSONEmbedded).

- **`HashSet<string> LoggedKeys`**  
  Used for deduplicating log entries (to avoid flooding the console with repeat logs).

All shared resources are protected by locks or thread-safe collections (`Concurrent*`, `lock(obj)`) where needed.

---

### 🧹 Reset & Cleanup

On mod reload / change config F12 / session start / session reset, all caches are cleared via:

This ensures no leak or stale data carries across sessions.

---

## 📁 JSONEmbedded Configuration

The mod reads from a JSONEmbedded file (injected by the server or pre-generated) which contains:
```JSONEmbedded
["6183afd850224f204c1da514"] = new FireRateEntry
            {
                ID = "6183afd850224f204c1da514",
                Name = "weapon_fn_mk17_762x51",
                ShortName = "Mk 17",
                FireRate = 600,
                HasFullAuto = false,
                Mod = false,
                Audio = new AudioData
                {
                    Clips = new Dictionary<string, AudioClipInfo>
                    {
                        ["scar_h_indoor_close"] = new AudioClipInfo { PathID = "6334314355034838777" },
                        ["scar_h_indoor_silenced_close"] = new AudioClipInfo { PathID = "-6359496520727020668" },
                        ["scar_h_outdoor_close"] = new AudioClipInfo { PathID = "-6406820538480817740" },
                        ["scar_h_outdoor_silenced_close"] = new AudioClipInfo { PathID = "1095011961334433329" },
                    }
                }
            }
