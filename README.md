# 🔊 TimeStretch Audio fire rate mod for SPT

## 🧩 What is this mod?

TimeStretch is a client-side mod for Escape From Tarkov - SPT that dynamically transforms firearm sounds based on their fire rate.

But TimeStretch goes beyond simple audio transformation:

- It introduces a **new Overclock system**, adding a custom firing mode to your weapons.
- You can **adjust the fire rate (RPM)** dynamically during gameplay.
- Weapon sounds are **automatically resynchronized** to match the new cadence.

This mod uses **BepInEx** and **Harmony** to hook into Tarkov's internal audio systems to:

- Identify when your weapon fires
- Replace in-memory `AudioClip` objects without modifying game files
- Inject a **native Overclock mode** into automatic weapons


## 📚 Log IF ONLY NEED 📚
-  **debug.cfg** - **TimeStretch_log.txt**  ==> ``BepInEx\plugins\TimeStretch`` true = debug mod on
---
## 🎯 Key Features

- 🔥 Overclock system allowing dynamic fire rate adjustment (RPM) during gameplay.
- 🔁 Dynamic sound transformation based on weapon ID and clip name.
- 🎧 Clip-by-clip replacement for each weapon using time-stretched audio.
- ⚙️ Configurable tempo modifiers (per clip, per weapon).
- 🚀 Optimized performance and threading model to prevent FPS drops.
- 📦 Auto-hook system that activates only for the local player when firing.
- 💬 Centralized logging with diagnostics per clip and per weapon.
- 🔒 Intelligent detection of modded vs vanilla weapons through JSONEmbedded.

---
## 🔥 Overclock / Transformation

### **Dynamic Overclock Mode Handling**
- A new firing mode "OverClock" (internal ID 8) is injected into weapons supporting full-auto.
- When switching to Overclock mode, the weapon's fire rate is dynamically updated.
- If the player increases or decreases the RPM (using key bindings), the fire rate is immediately applied and stored per weapon.

### **Dynamic Audio Synchronization**
- After a fire rate change, a delayed audio transformation is started to match the new RPM.
- Each weapon’s `AudioClip` is stretched or compressed according to the customized tempo based on the Overclock fire rate.

### **HUD and Interface Integration**
- Displays "OverClock" when in Overclock mode.
- A notification is shown every time the RPM is increased or decreased, showing the new Overclock value.

### **Clean Reset on Weapon Unequip**
- When unequipping a weapon :
  - The Overclock animation system is stopped.
  - The Fire Mode and AudioClip caches are cleaned.
  - No residual transformations remain when switching weapons.

### **Weapon Tracking**
When the local player equips a new weapon, the mod:
- Hooks `Player.set_HandsController` via `PatchTrackEquippedWeapon`
- Detects the current weapon’s `TemplateId`
- Checks if the weapon is marked as "modifiable" (`mod == true` from a JSONEmbedded)
- If valid, starts a background thread to enqueue the weapon for audio processing

### **Clip Discovery & Transformation**
Once a weapon is enqueued:
- `AudioClipModifier.GetTransformableClips()` identifies all `AudioClip` objects in memory matching the weapon's known clip names.
- For each clip:
    - Calculates a `tempo` based on config values
    - Clamps tempo within allowed range
    - Skips clips with 0% change
    - Schedules an async transformation via `AudioClipTransformer.TransformAsync()`

### **Replacement Handling**
- After transformation:
    - The transformed clip is **cached** and **registered** to replace the original
    - clips are NEVER !!!!!! overwritten in memory
    - replaced virtually by mapping names in a lookup dictionary

### **SoundBank Hook**
During gameplay:
- If the player is local and a weapon is firing:
    - The mod checks if a transformed clip is available for replacement
    - If found, it replaces the original with the modified version


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
Cache System Overview

1. **Global AudioClip Cache**:
- Transformed clips stored separately for normal fire and Overclock mode.
- Local mappings (clip name → transformed name) for fast resolution.
- Thread-safe dictionaries for all loaded AudioClips.

2. **Weapon Tracking**:
- Tracks equipped weapons and their fire rates (normal and overclocked).
- Manages queues of weapons needing audio transformation.
- Ensures no redundant processing with HashSets (ProcessedWeapons, etc.).

3. **Overclock Mode Caches**:
- Dedicated structures for Overclock clip mappings and transformed clips.
- Fire rate per weapon saved and updated dynamically.

4. **Hook Permissions**:
- Per-weapon permission system to enable/disable hooks dynamically.
- Prevents non-modifiable weapons from being processed.

5. **Logging Control**:
- Prevents console spam by logging unique keys only once.

6. **Cache Reset and Cleanup**:
- Clear individual caches (clips, mappings, weapons) separately.
- Full global reset on map unload or mod shutdown (ClearAllCache).

7. **Background Coroutine (Optional Debug)**:
- Monitors live transformed clips in memory if enabled for debugging.

---

### 🧹 Reset & Cleanup

On mod reload / change config F12 / session start / session reset / Hand empty hook, all caches are cleared via:

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