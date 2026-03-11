# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Purge Line** is a Unity 2022.3.62f3c1 game project using the Universal Render Pipeline (URP) with 2D features. The project is in early development, featuring a custom modular architecture framework.

## Unity Project Info

- **Unity Version**: 2022.3.62f3c1
- **Render Pipeline**: URP (Universal Render Pipeline)
- **Product Name**: Purge Line
- **Default Resolution**: 1920x1080
- **API Compatibility**: .NET Standard 2.1

## Key Dependencies

### Unity Packages
- `com.unity.entities` - ECS (Entity Component System)
- `com.unity.addressables` - Addressable asset management
- `com.unity.feature.2d` - 2D game features
- `com.unity.render-pipelines.universal` - URP renderer
- `com.unity.timeline` - Timeline cinematics
- `com.unity.textmeshpro` - Text rendering
- `com.unity.ugui` - UI system

### Third-Party Plugins
- **QFramework** - Comprehensive Unity framework (CoreKit, UIKit, ResKit, AudioKit, ActionKit, PoolKit, FSMKit, etc.)
- **DOTween** - Animation tweening library
- **ZLogger** - Structured logging
- **ZLinq** - LINQ extensions

## Architecture

### Startup Flow

```
GameBootstrapper.Awake()
  → Creates [GameFramework] GameObject
  → GameFramework.Awake()
    → Creates [SystemManager] GameObject
    → Initializes ZLogger
  → GameFramework.Initialize()
    → Registers all systems (to be implemented)
```

### Core Components

**GameBootstrapper** (`Assets/Scripts/Core/GameBootstrapper.cs`)
- Scene entry point: attach to any GameObject in a scene
- Programatically creates GameFramework and SystemManager
- Ensures singleton pattern

**GameFramework** (`Assets/Scripts/Core/GameFramework.cs`)
- Global singleton managing framework lifecycle
- Integrates ZLogger for logging
- Creates SystemManager
- Entry point for system registration in `Initialize()`

**SystemManager** (`Assets/Scripts/UnitySystemArchitecture/Manager/SystemManager.cs`)
- Core singleton for system management
- Handles system registration, initialization, and lifecycle
- Dispatches Update/LateUpdate/FixedUpdate to systems
- Supports global and per-system pause
- Manages coroutine execution

### System Interface Pattern

Systems implement one or more interfaces in `UnitySystemArchitecture.Core`:

- `ISystem` - Base interface with `OnInit()` and `OnDispose()`
- `IStart` - First-frame initialization with `OnStart()`
- `ITick` - Update callback with `OnTick(deltaTime)`
- `ILateTick` - LateUpdate callback with `OnLateTick(deltaTime)`
- `IFixedTick` - FixedUpdate callback with `OnFixedTick(fixedDeltaTime)`
- `IPausable` - Pause/resume functionality

## Directory Structure

```
Purge Line/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                    # GameFramework and GameBootstrapper
│   │   └── UnitySystemArchitecture/ # Custom system architecture framework
│   │       ├── Core/                # System interfaces
│   │       └── Manager/             # SystemManager
│   ├── Plugins/                     # QFramework, DOTween
│   ├── Scenes/                      # BootScene, SampleScene
│   ├── Resources/                   # Resources folder
│   ├── Packages/                    # NuGet packages
│   └── Settings/                    # Project settings
├── ProjectSettings/                 # Unity project settings
└── Packages/                        # Package manager manifest
```

## Adding New Systems

To add a new system:

1. Create a class implementing `ISystem` and any desired tick interfaces
2. Register it in `GameFramework.Initialize()`:
   ```csharp
   var sm = SystemManager.Instance;
   sm.Register(new MySystem());
   ```
3. Systems are updated in the order they are registered

## Important Notes

- All systems should get dependencies in their `OnStart()` method (not `OnInit()`) to allow for deferred resolution
- Systems are disposed in reverse registration order
- Use `SystemManager.Instance.Get<T>()` to retrieve registered systems
- Pause control: `SetGlobalPause()` for all systems or `SetSystemPause<T>()` for individual systems
