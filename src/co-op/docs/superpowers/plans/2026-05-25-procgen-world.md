# Procedural World Generation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate a deterministic, multiplayer-synced Phase-1 location (terrain + nature via MicroWorld) with gameplay anchors and player spawn on the generated Base zone.

**Architecture:** MicroWorld owns terrain + nature, run locally on each machine from a shared seed (host→client via SyncVar). Our code owns seed-sync, deterministic gameplay-anchor placement on the finished terrain, and a reusable server-authoritative networked spawn service with DI. Visual scatter is non-networked; gameplay objects (player now, collectables later) are server-spawned NetworkObjects.

**Tech Stack:** Unity 6 (URP), Zenject, UniTask, FishNet, MicroWorld (`Assets/Plugins/MicroWorld`).

**Spec:** `docs/superpowers/specs/2026-05-25-procgen-world-design.md`

**Commits:** Project owner commits manually (see `CLAUDE.md`). The `Commit` steps are review checkpoints — stop, surface the diff, let the owner commit. Do not run `git commit` automatically.

**Testing note:** Pure-C# classes (`DeterministicRandom`, `AnchorPlacer`) are TDD'd in EditMode. Unity/FishNet/MicroWorld-coupled classes are verified manually in the editor (steps say how).

---

## Task 1: WorldGenConfig + provider wiring + ConfigPaths

**Files:**
- Create: `Assets/Scripts/Data/Configs/WorldGenConfig.cs`
- Modify: `Assets/Scripts/Data/Paths/ConfigPaths.cs`
- Modify: `Assets/Scripts/Infrastructure/Providers/Configs/IConfigDataProvider.cs`
- Modify: `Assets/Scripts/Infrastructure/Providers/Configs/ConfigDataProvider.cs`

- [ ] **Step 1: Create WorldGenConfig**

```csharp
using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/World Gen Config", fileName = "WorldGenConfig")]
    public sealed class WorldGenConfig : ScriptableObject
    {
        [Header("Seed")]
        [Tooltip("If true, host uses FixedSeed every game (debug). If false, host picks a random non-zero seed.")]
        public bool UseFixedSeed = false;
        public int FixedSeed = 12345;

        [Header("Anchors")]
        [Tooltip("Number of component spawn anchors placed across the terrain.")]
        public int ComponentAnchorCount = 6;
        [Tooltip("Minimum world-space distance between Base and Source anchors (meters).")]
        public float MinBaseSourceDistance = 60f;
        [Tooltip("Minimum distance between any two component anchors (meters).")]
        public float MinComponentSpacing = 12f;
        [Tooltip("Player spawn slots arranged in a circle of this radius around the Base anchor (meters).")]
        public float BaseSpawnRadius = 3f;
        [Tooltip("Max slope (degrees) a placed anchor tolerates before re-sampling.")]
        [Range(0f, 89f)] public float MaxAnchorSlope = 25f;
    }
}
```

- [ ] **Step 2: Add path constant**

In `ConfigPaths.cs`, add inside the class:
```csharp
public const string WORLD_CONFIG_PATH = "Configs/World/WorldGenConfig";
```

- [ ] **Step 3: Add Movement-style accessor to IConfigDataProvider**

In `IConfigDataProvider.cs`, add to the interface (alongside `Network`, `Movement`):
```csharp
WorldGenConfig World { get; }
```

- [ ] **Step 4: Implement in ConfigDataProvider**

In `ConfigDataProvider.cs`: add the property and load it in `LoadAsync` (mirror the existing `Movement` loading). Add field/property:
```csharp
public WorldGenConfig World { get; private set; }
```
Extend the `UniTask.WhenAll` tuple in `LoadAsync` to also load it:
```csharp
var (windows, network, movement, world) = await UniTask.WhenAll(
    LoadOneAsync<WindowsConfig>(ConfigPaths.WINDOWS_CONFIG_PATH, ct),
    LoadOneAsync<NetworkConfig>(ConfigPaths.NETWORK_CONFIG_PATH, ct),
    LoadOneAsync<MovementConfig>(ConfigPaths.MOVEMENT_CONFIG_PATH, ct),
    LoadOneAsync<WorldGenConfig>(ConfigPaths.WORLD_CONFIG_PATH, ct));

Windows = windows; Network = network; Movement = movement; World = world;
```
Add `World` to the load Debug.Log summary line.

- [ ] **Step 5: Verify compile + commit checkpoint**

In Unity Console: no compile errors. Then surface diff for commit:
```
feat(config): add WorldGenConfig + provider accessor
```

---

## Task 2: DeterministicRandom (TDD)

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/World/DeterministicRandom.cs`
- Test: `Assets/Tests/EditMode/DeterministicRandomTests.cs` (asmdef created in Task 16; if running tests now, do Task 16 first)

- [ ] **Step 1: Write the failing test**

```csharp
using Infrastructure.Services.World;
using NUnit.Framework;

namespace CoOp.Tests.EditMode
{
    public class DeterministicRandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new DeterministicRandom(42);
            var b = new DeterministicRandom(42);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"diverged at {i}");
        }

        [Test]
        public void DifferentSeed_ProducesDifferentSequence()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);
            Assert.AreNotEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void NextFloat_InUnitRange()
        {
            var r = new DeterministicRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                var v = r.NextFloat();
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void Mix_IsStableAndDistinct()
        {
            Assert.AreEqual(DeterministicRandom.Mix(10, 1), DeterministicRandom.Mix(10, 1));
            Assert.AreNotEqual(DeterministicRandom.Mix(10, 1), DeterministicRandom.Mix(10, 2));
        }
    }
}
```

- [ ] **Step 2: Run test, verify it fails**

Run via Unity Test Runner (Window → General → Test Runner → EditMode). Expected: compile error / type `DeterministicRandom` not found.

- [ ] **Step 3: Implement DeterministicRandom**

```csharp
namespace Infrastructure.Services.World
{
    /// <summary>
    /// Seeded xorshift64 RNG. Integer state, bit-exact reproducibility across machines on the
    /// same platform. Never use UnityEngine.Random for world generation (global, non-deterministic).
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) => _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;

        public uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return (uint)(_state >> 32);
        }

        /// <summary>Uniform float in [0,1].</summary>
        public float NextFloat() => NextUInt() / (float)uint.MaxValue;

        public float Range(float min, float max) => min + NextFloat() * (max - min);

        /// <summary>splitmix64 mix — derive a stable, distinct sub-seed from (seed, salt).</summary>
        public static ulong Mix(int seed, int salt)
        {
            ulong z = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + (ulong)(uint)salt + 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

Test Runner → run `DeterministicRandomTests`. Expected: all green.

- [ ] **Step 5: Commit checkpoint**

```
feat(world): deterministic seeded RNG
```

---

## Task 3: WorldGeneratedSignal + declaration

**Files:**
- Create: `Assets/Scripts/Signals/WorldSignals.cs`
- Modify: `Assets/Scripts/Infrastructure/Installers/ProjectInstaller.cs` (BindSignals)

- [ ] **Step 1: Create the signal**

```csharp
namespace Signals
{
    public readonly struct WorldGeneratedSignal
    {
        public readonly int Seed;
        public WorldGeneratedSignal(int seed) => Seed = seed;
    }
}
```

- [ ] **Step 2: Declare it**

In `ProjectInstaller.BindSignals()`, add:
```csharp
Container.DeclareSignal<WorldGeneratedSignal>();
```

- [ ] **Step 3: Verify compile + commit checkpoint**

```
feat(world): WorldGeneratedSignal
```

---

## Task 4: NetworkSpawnService

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/Spawn/INetworkSpawnService.cs`
- Create: `Assets/Scripts/Infrastructure/Services/Spawn/NetworkSpawnService.cs`

- [ ] **Step 1: Interface**

```csharp
using FishNet.Connection;
using UnityEngine;

namespace Infrastructure.Services.Spawn
{
    /// <summary>
    /// Server-only. Instantiates a networked prefab through the scene DI container (so [Inject]
    /// fields and GameObjectContext run) and then network-spawns it via FishNet so every client
    /// receives it. The single place that combines DI + ServerManager.Spawn.
    /// </summary>
    public interface INetworkSpawnService
    {
        GameObject SpawnNetworked(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnection owner = null);
        void Despawn(GameObject instance);
    }
}
```

- [ ] **Step 2: Implementation**

```csharp
using FishNet.Connection;
using Infrastructure.Services.Network;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Spawn
{
    public sealed class NetworkSpawnService : INetworkSpawnService
    {
        private readonly INetworkService _network;
        private readonly DiContainer _sceneContainer;

        public NetworkSpawnService(INetworkService network, DiContainer sceneContainer)
        {
            _network = network;
            _sceneContainer = sceneContainer;
        }

        public GameObject SpawnNetworked(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnection owner = null)
        {
            if (_network == null || !_network.IsServer)
            {
                Debug.LogWarning("[NetworkSpawnService] SpawnNetworked called on non-server. Ignored.");
                return null;
            }
            if (prefab == null)
            {
                Debug.LogError("[NetworkSpawnService] Prefab is null.");
                return null;
            }

            // InstantiatePrefab via the scene container → DI injection (and GameObjectContext.Awake) runs
            // before the network spawn message goes out.
            var go = _sceneContainer.InstantiatePrefab(prefab, position, rotation, null);
            if (go == null) return null;

            _network.NetworkManager.ServerManager.Spawn(go, owner);
            return go;
        }

        public void Despawn(GameObject instance)
        {
            if (_network == null || !_network.IsServer || instance == null) return;
            _network.NetworkManager.ServerManager.Despawn(instance);
        }
    }
}
```

- [ ] **Step 3: Verify compile + commit checkpoint**

```
feat(spawn): reusable server-side networked spawn service with DI
```

---

## Task 5: Refactor PlayerSpawnService onto NetworkSpawnService

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Services/Spawn/PlayerSpawnService.cs`

- [ ] **Step 1: Inject INetworkSpawnService and replace the instantiate+spawn block**

Change the constructor to also take `INetworkSpawnService _spawner`. In `SpawnPlayerAsync`, replace the manual `_sceneContainer.InstantiatePrefab(...)` + `_network.NetworkManager.ServerManager.Spawn(go, conn)` pair with:
```csharp
var go = _spawner.SpawnNetworked(_playerPrefab, pos, rot, conn);
if (go == null)
{
    _signalBus.Fire(new SpawnFailedSignal(conn?.ClientId ?? -1, "NetworkSpawnService returned null"));
    return null;
}
var pn = go.GetComponent<PlayerNetwork>();
```
Keep the existing `PlayerNetwork`-null check, the `_spawnedByClientId` bookkeeping, the `await UniTask.Yield(...)`, and the `SpawnFailedSignal` paths. Remove the now-unused direct `DiContainer` field only if it is no longer referenced elsewhere in the class (it is used solely for instantiation — safe to drop from the constructor once the spawn goes through the service).

- [ ] **Step 2: Verify compile**

Unity Console: no errors. `GameSceneInstaller` binding for `PlayerSpawnService` will be updated in Task 15 to satisfy the new constructor param (it resolves `INetworkSpawnService` automatically once bound).

- [ ] **Step 3: Commit checkpoint**

```
refactor(spawn): PlayerSpawnService uses NetworkSpawnService
```

---

## Task 6: Anchor data types

**Files:**
- Create: `Assets/Scripts/Gameplay/World/AnchorType.cs`
- Create: `Assets/Scripts/Gameplay/World/WorldAnchor.cs`
- Create: `Assets/Scripts/Infrastructure/Services/World/WorldGenerationResult.cs`

- [ ] **Step 1: AnchorType enum**

```csharp
namespace Gameplay.World
{
    public enum AnchorType
    {
        Base,
        Source,
        ComponentSpawn,
    }
}
```

- [ ] **Step 2: WorldAnchor marker**

```csharp
using UnityEngine;

namespace Gameplay.World
{
    /// <summary>Marker placed at a computed gameplay position. Visual gizmo only; no logic.</summary>
    public sealed class WorldAnchor : MonoBehaviour
    {
        public AnchorType Type;

        private void OnDrawGizmos()
        {
            Gizmos.color = Type switch
            {
                AnchorType.Base => Color.cyan,
                AnchorType.Source => Color.red,
                _ => Color.yellow,
            };
            Gizmos.DrawWireSphere(transform.position, 1.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }
    }
}
```

- [ ] **Step 3: WorldGenerationResult**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Infrastructure.Services.World
{
    public readonly struct AnchorPoint
    {
        public readonly Vector3 Position;
        public AnchorPoint(Vector3 position) => Position = position;
    }

    public sealed class WorldGenerationResult
    {
        public AnchorPoint Base { get; }
        public AnchorPoint Source { get; }
        public IReadOnlyList<AnchorPoint> ComponentAnchors { get; }

        public WorldGenerationResult(AnchorPoint baseAnchor, AnchorPoint source, IReadOnlyList<AnchorPoint> componentAnchors)
        {
            Base = baseAnchor;
            Source = source;
            ComponentAnchors = componentAnchors;
        }
    }
}
```

- [ ] **Step 4: Verify compile + commit checkpoint**

```
feat(world): anchor data types
```

---

## Task 7: AnchorPlacer (TDD)

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/World/IAnchorPlacer.cs`
- Create: `Assets/Scripts/Infrastructure/Services/World/AnchorPlacer.cs`
- Test: `Assets/Tests/EditMode/AnchorPlacerTests.cs`

The placer works on an abstract `ITerrainSampler` so it is testable without a real Unity Terrain.

- [ ] **Step 1: Terrain sampler abstraction (in AnchorPlacer.cs file, above the class)**

```csharp
public interface ITerrainSampler
{
    /// <summary>World-space size of the terrain on X/Z (meters).</summary>
    Vector2 WorldSize { get; }
    /// <summary>Surface height (world Y) at world XZ.</summary>
    float SampleHeight(float worldX, float worldZ);
    /// <summary>Surface steepness (degrees) at world XZ.</summary>
    float SampleSteepness(float worldX, float worldZ);
}
```

- [ ] **Step 2: Write the failing test (with a fake sampler)**

```csharp
using System.Collections.Generic;
using Data.Configs;
using Infrastructure.Services.World;
using NUnit.Framework;
using UnityEngine;

namespace CoOp.Tests.EditMode
{
    public class AnchorPlacerTests
    {
        private sealed class FlatSampler : ITerrainSampler
        {
            public Vector2 WorldSize => new(200f, 200f);
            public float SampleHeight(float x, float z) => 0f;
            public float SampleSteepness(float x, float z) => 0f;
        }

        private static WorldGenConfig Config()
        {
            var c = ScriptableObject.CreateInstance<WorldGenConfig>();
            c.ComponentAnchorCount = 6;
            c.MinBaseSourceDistance = 60f;
            c.MinComponentSpacing = 12f;
            c.MaxAnchorSlope = 25f;
            return c;
        }

        [Test]
        public void SameSeed_SameAnchors()
        {
            var placer = new AnchorPlacer();
            var r1 = placer.Place(123, new FlatSampler(), Config());
            var r2 = placer.Place(123, new FlatSampler(), Config());
            Assert.AreEqual(r1.Base.Position, r2.Base.Position);
            Assert.AreEqual(r1.Source.Position, r2.Source.Position);
            Assert.AreEqual(r1.ComponentAnchors.Count, r2.ComponentAnchors.Count);
            for (int i = 0; i < r1.ComponentAnchors.Count; i++)
                Assert.AreEqual(r1.ComponentAnchors[i].Position, r2.ComponentAnchors[i].Position);
        }

        [Test]
        public void BaseAndSource_RespectMinDistance()
        {
            var cfg = Config();
            var r = new AnchorPlacer().Place(99, new FlatSampler(), cfg);
            var d = Vector3.Distance(r.Base.Position, r.Source.Position);
            Assert.GreaterOrEqual(d, cfg.MinBaseSourceDistance);
        }

        [Test]
        public void ComponentAnchors_CountMatchesConfig_AndWithinBounds()
        {
            var cfg = Config();
            var sampler = new FlatSampler();
            var r = new AnchorPlacer().Place(7, sampler, cfg);
            Assert.AreEqual(cfg.ComponentAnchorCount, r.ComponentAnchors.Count);
            foreach (var a in r.ComponentAnchors)
            {
                Assert.GreaterOrEqual(a.Position.x, 0f);
                Assert.LessOrEqual(a.Position.x, sampler.WorldSize.x);
                Assert.GreaterOrEqual(a.Position.z, 0f);
                Assert.LessOrEqual(a.Position.z, sampler.WorldSize.y);
            }
        }
    }
}
```

- [ ] **Step 3: Run test, verify it fails**

Test Runner EditMode. Expected: `AnchorPlacer` / `IAnchorPlacer` not found.

- [ ] **Step 4: Interface**

```csharp
using Data.Configs;

namespace Infrastructure.Services.World
{
    public interface IAnchorPlacer
    {
        WorldGenerationResult Place(int seed, ITerrainSampler sampler, WorldGenConfig config);
    }
}
```

- [ ] **Step 5: Implementation**

```csharp
using System.Collections.Generic;
using Data.Configs;
using UnityEngine;

namespace Infrastructure.Services.World
{
    public interface ITerrainSampler
    {
        Vector2 WorldSize { get; }
        float SampleHeight(float worldX, float worldZ);
        float SampleSteepness(float worldX, float worldZ);
    }

    public sealed class AnchorPlacer : IAnchorPlacer
    {
        private const int SaltBase = 101;
        private const int SaltSource = 202;
        private const int SaltComponents = 303;
        private const int MaxSampleAttempts = 40;

        public WorldGenerationResult Place(int seed, ITerrainSampler sampler, WorldGenConfig config)
        {
            var size = sampler.WorldSize;

            // Base: near one edge, biased to a seed-chosen side.
            var baseRng = new DeterministicRandom(DeterministicRandom.Mix(seed, SaltBase));
            var basePos = SampleFlat(baseRng, sampler, config,
                xMin: size.x * 0.10f, xMax: size.x * 0.30f,
                zMin: size.y * 0.10f, zMax: size.y * 0.90f);

            // Source: opposite side, enforce min distance from Base.
            var srcRng = new DeterministicRandom(DeterministicRandom.Mix(seed, SaltSource));
            Vector3 srcPos = default;
            for (int i = 0; i < MaxSampleAttempts; i++)
            {
                srcPos = SampleFlat(srcRng, sampler, config,
                    xMin: size.x * 0.70f, xMax: size.x * 0.90f,
                    zMin: size.y * 0.10f, zMax: size.y * 0.90f);
                if (Vector3.Distance(basePos, srcPos) >= config.MinBaseSourceDistance) break;
            }

            // Component anchors: scattered, min spacing, increasing distance from Base preferred.
            var compRng = new DeterministicRandom(DeterministicRandom.Mix(seed, SaltComponents));
            var comps = new List<AnchorPoint>(config.ComponentAnchorCount);
            int placed = 0, guard = 0;
            while (placed < config.ComponentAnchorCount && guard < config.ComponentAnchorCount * MaxSampleAttempts)
            {
                guard++;
                var p = SampleFlat(compRng, sampler, config,
                    xMin: size.x * 0.05f, xMax: size.x * 0.95f,
                    zMin: size.y * 0.05f, zMax: size.y * 0.95f);
                bool ok = true;
                foreach (var c in comps)
                    if (Vector3.Distance(c.Position, p) < config.MinComponentSpacing) { ok = false; break; }
                if (!ok) continue;
                comps.Add(new AnchorPoint(p));
                placed++;
            }

            return new WorldGenerationResult(new AnchorPoint(basePos), new AnchorPoint(srcPos), comps);
        }

        private static Vector3 SampleFlat(DeterministicRandom rng, ITerrainSampler sampler, WorldGenConfig config,
            float xMin, float xMax, float zMin, float zMax)
        {
            Vector3 best = default;
            float bestSlope = float.MaxValue;
            for (int i = 0; i < MaxSampleAttempts; i++)
            {
                float x = rng.Range(xMin, xMax);
                float z = rng.Range(zMin, zMax);
                float slope = sampler.SampleSteepness(x, z);
                var p = new Vector3(x, sampler.SampleHeight(x, z), z);
                if (slope <= config.MaxAnchorSlope) return p;
                if (slope < bestSlope) { bestSlope = slope; best = p; }
            }
            return best; // fallback: flattest sampled point
        }
    }
}
```

- [ ] **Step 6: Run tests, verify pass**

Test Runner → `AnchorPlacerTests` all green.

- [ ] **Step 7: Commit checkpoint**

```
feat(world): deterministic anchor placer (TDD)
```

---

## Task 8: MicroWorldRunner

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/World/IMicroWorldRunner.cs`
- Create: `Assets/Scripts/Infrastructure/Services/World/MicroWorldRunner.cs`

- [ ] **Step 1: Interface**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Infrastructure.Services.World
{
    public interface IMicroWorldRunner
    {
        /// <summary>Sets the MicroWorld seed and runs its async build, completing when the terrain is built.</summary>
        UniTask BuildAsync(int seed, CancellationToken ct = default);

        /// <summary>The active Unity Terrain produced by MicroWorld (null until first build completes).</summary>
        UnityEngine.Terrain ActiveTerrain { get; }
    }
}
```

- [ ] **Step 2: Implementation**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using MicroWorldNS;
using UnityEngine;

namespace Infrastructure.Services.World
{
    public sealed class MicroWorldRunner : IMicroWorldRunner
    {
        private readonly MicroWorld _microWorld;

        public MicroWorldRunner(MicroWorld microWorld) => _microWorld = microWorld;

        public Terrain ActiveTerrain => Terrain.activeTerrain;

        public UniTask BuildAsync(int seed, CancellationToken ct = default)
        {
            if (_microWorld == null)
            {
                Debug.LogError("[MicroWorldRunner] MicroWorld reference is null. Assign the MicroWorld object in GameSceneInstaller.");
                return UniTask.CompletedTask;
            }

            _microWorld.Seed = seed;
            var tcs = new UniTaskCompletionSource();
            _microWorld.BuildAsync(onCompleted: _ => tcs.TrySetResult(), activateAfterBuilt: true);
            return tcs.Task.AttachExternalCancellation(ct);
        }
    }
}
```

> Note: the MicroWorld root type is `MicroWorldNS.MicroWorld` (namespace confirmed from `Assets/Plugins/MicroWorld/Scripts/Core/MicroWorld.cs`). If the editor reports a different namespace, adjust the `using`.

- [ ] **Step 3: Verify compile + commit checkpoint**

```
feat(world): MicroWorld runner wrapper
```

---

## Task 9: WorldSeedProvider

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/World/IWorldSeedProvider.cs`
- Create: `Assets/Scripts/Infrastructure/Services/World/WorldSeedProvider.cs`

- [ ] **Step 1: Interface**

```csharp
namespace Infrastructure.Services.World
{
    public interface IWorldSeedProvider
    {
        int Seed { get; }
        bool HasSeed { get; }
        void SetSeed(int seed);
    }
}
```

- [ ] **Step 2: Implementation**

```csharp
namespace Infrastructure.Services.World
{
    public sealed class WorldSeedProvider : IWorldSeedProvider
    {
        public int Seed { get; private set; }
        public bool HasSeed => Seed != 0;
        public void SetSeed(int seed) => Seed = seed;
    }
}
```

- [ ] **Step 3: Verify compile + commit checkpoint**

```
feat(world): world seed provider
```

---

## Task 10: WorldGenerationService

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/World/IWorldGenerationService.cs`
- Create: `Assets/Scripts/Infrastructure/Services/World/WorldGenerationService.cs`
- Create: `Assets/Scripts/Infrastructure/Services/World/TerrainSamplerAdapter.cs`

- [ ] **Step 1: Terrain sampler adapter (wraps a Unity Terrain into ITerrainSampler)**

```csharp
using UnityEngine;

namespace Infrastructure.Services.World
{
    public sealed class TerrainSamplerAdapter : ITerrainSampler
    {
        private readonly Terrain _terrain;
        private readonly TerrainData _data;

        public TerrainSamplerAdapter(Terrain terrain)
        {
            _terrain = terrain;
            _data = terrain.terrainData;
        }

        public Vector2 WorldSize => new(_data.size.x, _data.size.z);

        public float SampleHeight(float worldX, float worldZ)
        {
            var origin = _terrain.transform.position;
            return _terrain.SampleHeight(new Vector3(origin.x + worldX, 0f, origin.z + worldZ)) + origin.y;
        }

        public float SampleSteepness(float worldX, float worldZ)
        {
            // GetSteepness wants normalized [0,1] coords across the terrain.
            float nx = Mathf.Clamp01(worldX / _data.size.x);
            float nz = Mathf.Clamp01(worldZ / _data.size.z);
            return _data.GetSteepness(nx, nz);
        }
    }
}
```

- [ ] **Step 2: Interface**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Infrastructure.Services.World
{
    public interface IWorldGenerationService
    {
        WorldGenerationResult Result { get; }
        bool IsReady { get; }
        UniTask GenerateAsync(int seed, CancellationToken ct = default);
    }
}
```

- [ ] **Step 3: Implementation**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Configs;
using Infrastructure.Providers.Configs;
using Signals;
using UnityEngine;

namespace Infrastructure.Services.World
{
    public sealed class WorldGenerationService : IWorldGenerationService
    {
        private readonly IMicroWorldRunner _runner;
        private readonly IAnchorPlacer _anchorPlacer;
        private readonly IConfigDataProvider _configs;
        private readonly SignalBus _signalBus;

        public WorldGenerationResult Result { get; private set; }
        public bool IsReady => Result != null;

        public WorldGenerationService(IMicroWorldRunner runner, IAnchorPlacer anchorPlacer,
            IConfigDataProvider configs, SignalBus signalBus)
        {
            _runner = runner;
            _anchorPlacer = anchorPlacer;
            _configs = configs;
            _signalBus = signalBus;
        }

        public async UniTask GenerateAsync(int seed, CancellationToken ct = default)
        {
            await _runner.BuildAsync(seed, ct);

            var terrain = _runner.ActiveTerrain;
            if (terrain == null)
            {
                Debug.LogError("[WorldGenerationService] No active terrain after MicroWorld build.");
                return;
            }

            var sampler = new TerrainSamplerAdapter(terrain);
            Result = _anchorPlacer.Place(seed, sampler, _configs.World);

            _signalBus.Fire(new WorldGeneratedSignal(seed));
        }
    }
}
```

- [ ] **Step 4: Verify compile + commit checkpoint**

```
feat(world): generation orchestrator (MicroWorld build -> anchors -> signal)
```

---

## Task 11: WorldNetworkController

**Files:**
- Create: `Assets/Scripts/Gameplay/World/WorldNetworkController.cs`

- [ ] **Step 1: Implementation**

```csharp
using System;
using Cysharp.Threading.Tasks;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.World;
using UnityEngine;
using Zenject;

namespace Gameplay.World
{
    /// <summary>
    /// Scene NetworkObject. Server picks the world seed and replicates it; both machines run the
    /// deterministic generation locally from that seed. Injected directly by the Game SceneContext.
    /// </summary>
    public class WorldNetworkController : NetworkBehaviour
    {
        [Inject] private IWorldGenerationService _generation;
        [Inject] private IWorldSeedProvider _seedProvider;
        [Inject] private IConfigDataProvider _configs;

        private readonly SyncVar<int> _seed = new();
        private bool _generationStarted;

        private void Awake() => _seed.OnChange += OnSeedChanged;
        private void OnDestroy() => _seed.OnChange -= OnSeedChanged;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _seed.Value = PickSeed();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (_seed.Value != 0) TryGenerate(_seed.Value);
        }

        private void OnSeedChanged(int prev, int next, bool asServer) => TryGenerate(next);

        private void TryGenerate(int seed)
        {
            if (_generationStarted || seed == 0) return;
            _generationStarted = true;
            _seedProvider.SetSeed(seed);
            _generation.GenerateAsync(seed, destroyCancellationToken).Forget();
        }

        private int PickSeed()
        {
            var cfg = _configs?.World;
            if (cfg != null && cfg.UseFixedSeed)
                return cfg.FixedSeed == 0 ? 1 : cfg.FixedSeed;
            int s = Environment.TickCount ^ (int)(Time.realtimeSinceStartupAsDouble * 1000d);
            return s == 0 ? 1 : s;
        }
    }
}
```

> Note: FishNet 4.x uses `SyncVar<T>` field syntax with `.Value` and `.OnChange`. If the project's FishNet build uses the attribute form (`[SyncVar(OnChange=...)] int _seed;`), adapt accordingly — verify against `Assets/Plugins/FishNet` SyncVar examples before finalizing.

- [ ] **Step 2: Verify compile**

Unity Console: no errors. If `SyncVar<int>` is not found, switch to the attribute form per the note.

- [ ] **Step 3: Commit checkpoint**

```
feat(world): networked seed sync + generation trigger
```

---

## Task 12: SpawnPointRegistry — runtime populate from Base anchor

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Services/Spawn/ISpawnPointRegistry.cs`
- Modify: `Assets/Scripts/Infrastructure/Services/Spawn/SpawnPointRegistry.cs`

- [ ] **Step 1: Replace the interface with a runtime-populated contract**

```csharp
using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine;

namespace Infrastructure.Services.Spawn
{
    public interface ISpawnPointRegistry
    {
        bool IsEmpty { get; }
        /// <summary>Populate spawn slots arranged in a circle around a center (called after world generation).</summary>
        void PopulateAround(Vector3 center, float radius, int slotCount);
        /// <summary>Deterministic per-connection slot (round-robin by ClientId).</summary>
        Vector3 GetForConnection(NetworkConnection conn);
    }
}
```

- [ ] **Step 2: Replace the implementation**

```csharp
using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine;

namespace Infrastructure.Services.Spawn
{
    public sealed class SpawnPointRegistry : ISpawnPointRegistry
    {
        private readonly List<Vector3> _slots = new();

        public bool IsEmpty => _slots.Count == 0;

        public void PopulateAround(Vector3 center, float radius, int slotCount)
        {
            _slots.Clear();
            slotCount = Mathf.Max(1, slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                float angle = (360f / slotCount) * i * Mathf.Deg2Rad;
                _slots.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        public Vector3 GetForConnection(NetworkConnection conn)
        {
            if (_slots.Count == 0) return Vector3.zero;
            int id = conn?.ClientId ?? 0;
            return _slots[Mathf.Abs(id) % _slots.Count];
        }
    }
}
```

- [ ] **Step 3: Verify compile**

`PlayerSpawnService` references `GetForConnection` — return type changed from `SpawnPoint` to `Vector3`. Update its `SpawnPlayerAsync` to use the `Vector3` directly (Task 13 covers this). Expect a compile error here until Task 13 — acceptable, fix in next task.

- [ ] **Step 4: Commit checkpoint (after Task 13 compiles)**

```
refactor(spawn): runtime-populated spawn registry
```

---

## Task 13: PlayerSpawnService — world-ready gate + Base spawn

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Services/Spawn/PlayerSpawnService.cs`

- [ ] **Step 1: Inject world gen + signal bus; gate spawning on world-ready**

Add constructor deps: `IWorldGenerationService _worldGen`, `ISpawnPointRegistry _spawnRegistry` (already present), `SignalBus` (already present). Add a `bool _worldReady` field. In `Initialize` (server branch): subscribe to `OnRemoteConnectionState` as before, AND `_signalBus.Subscribe<WorldGeneratedSignal>(OnWorldGenerated)`; do NOT spawn the already-connected clients yet (remove/guard that loop behind `_worldReady`).

```csharp
private void OnWorldGenerated(WorldGeneratedSignal _)
{
    _worldReady = true;
    var result = _worldGen.Result;
    if (result != null)
        _spawnRegistry.PopulateAround(result.Base.Position, _configs.World.BaseSpawnRadius, ExpectedSlots);

    // Spawn everyone already connected.
    var clients = _network.NetworkManager.ServerManager.Clients;
    foreach (var kv in clients)
        if (kv.Value != null && !_spawnedByClientId.ContainsKey(kv.Value.ClientId))
            SpawnPlayerAsync(kv.Value, _serviceCts.Token).Forget();
}
```
Where `ExpectedSlots` is a small constant (e.g. `4`) or `_configs.World`-derived. Add `IConfigDataProvider _configs` to the constructor for `BaseSpawnRadius`.

- [ ] **Step 2: In OnRemoteConn(Started), defer until world ready**

```csharp
if (args.ConnectionState == RemoteConnectionState.Started)
{
    if (_worldReady) SpawnPlayerAsync(conn, _serviceCts.Token).Forget();
    // else: will be spawned in OnWorldGenerated
}
```

- [ ] **Step 3: Use Vector3 slot from registry in SpawnPlayerAsync**

Replace the `SpawnPoint spawn = _spawnRegistry.GetForConnection(conn); var pos = spawn != null ? spawn.Position : Vector3.zero;` lines with:
```csharp
var pos = _spawnRegistry.GetForConnection(conn);
var rot = Quaternion.identity;
```

- [ ] **Step 4: Unsubscribe in Dispose**

Add `_signalBus.TryUnsubscribe<WorldGeneratedSignal>(OnWorldGenerated);` to `Dispose`.

- [ ] **Step 5: Verify compile (Task 12 + 13 together now compile)**

Unity Console: no errors.

- [ ] **Step 6: Commit checkpoint**

```
feat(spawn): gate player spawn on world generation, spawn at Base
```

---

## Task 14: LoadGameState — await world generation

**Files:**
- Modify: `Assets/Scripts/Core/States/LoadGameState.cs`

- [ ] **Step 1: Inject SignalBus, await WorldGeneratedSignal before GameplayState**

Add `SignalBus _signalBus` to the constructor. After the scene-load block (host `LoadGlobalSceneAsync` / client `WaitForSceneLoadedAsync`) and before `_loadingScreen.Hide()`, await world generation via a UniTaskCompletionSource bridged from the signal:

```csharp
// Wait for the world to be generated (fired by WorldNetworkController in the loaded scene).
var worldTcs = new UniTaskCompletionSource();
void OnWorldGenerated(WorldGeneratedSignal _) => worldTcs.TrySetResult();
_signalBus.Subscribe<WorldGeneratedSignal>(OnWorldGenerated);
try
{
    await worldTcs.Task
        .AttachExternalCancellation(ct)
        .Timeout(System.TimeSpan.FromSeconds(30));
}
catch (System.TimeoutException)
{
    Debug.LogError("[LoadGameState] World generation timed out.");
    await _session.LeaveAsync(System.Threading.CancellationToken.None);
    await _stateMachine.EnterAsync<LoadMainMenuState>(System.Threading.CancellationToken.None);
    return;
}
finally
{
    _signalBus.TryUnsubscribe<WorldGeneratedSignal>(OnWorldGenerated);
}
```

Place this inside the existing `try` (before `_loadingScreen.Hide()` in the `finally`). Add `using Signals;` and `using Cysharp.Threading.Tasks;` (already present).

- [ ] **Step 2: Verify compile**

Unity Console: no errors. `UniTask.Timeout` exists in UniTask; if the overload differs, use `.TimeoutWithoutException(TimeSpan)` and branch on the `IsTimeout` result instead.

- [ ] **Step 3: Commit checkpoint**

```
feat(flow): LoadGameState waits for world generation
```

---

## Task 15: GameSceneInstaller bindings

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Installers/GameSceneInstaller.cs`

- [ ] **Step 1: Add a MicroWorld reference field + world bindings; drop spawn-point field**

Add:
```csharp
[Header("World")]
[SerializeField] private MicroWorldNS.MicroWorld _microWorld;
```
Remove the `[SerializeField] private SpawnPoint[] _spawnPoints;` field and its `WithArguments` usage.

In `InstallBindings`, add a `BindWorld()` call and method:
```csharp
private void BindWorld()
{
    if (_microWorld == null)
        Debug.LogError("[GameSceneInstaller] MicroWorld reference not assigned.", this);

    Container.Bind<MicroWorldNS.MicroWorld>().FromInstance(_microWorld).AsSingle();
    Container.Bind<IMicroWorldRunner>().To<MicroWorldRunner>().AsSingle();
    Container.Bind<IAnchorPlacer>().To<AnchorPlacer>().AsSingle();
    Container.Bind<IWorldSeedProvider>().To<WorldSeedProvider>().AsSingle();
    Container.Bind<IWorldGenerationService>().To<WorldGenerationService>().AsSingle();
}
```
Bind the spawn services without scene `SpawnPoint[]`:
```csharp
Container.Bind<ISpawnPointRegistry>().To<SpawnPointRegistry>().AsSingle();
Container.Bind<INetworkSpawnService>().To<NetworkSpawnService>().AsSingle();
// PlayerSpawnService bound as before (BindInterfacesAndSelfTo, WithArguments _playerPrefab)
```
Add the required `using Infrastructure.Services.World;` import.

- [ ] **Step 2: Verify compile + scene injection**

Unity Console: no errors. `WorldNetworkController` (scene object) and other scene MonoBehaviours get injected by SceneContext.

- [ ] **Step 3: Commit checkpoint**

```
feat(di): bind world generation + spawn services in GameSceneInstaller
```

---

## Task 16: EditMode test assembly

**Files:**
- Create: `Assets/Tests/EditMode/CoOp.Tests.EditMode.asmdef`

- [ ] **Step 1: Create the test asmdef**

Use Unity: Window → General → Test Runner → EditMode → "Create EditMode Test Assembly Folder" (generates a correct asmdef with Test Framework references). Then add references to the production code: in the asmdef inspector, add `Assembly-CSharp` is not referenceable directly — instead ensure the test asmdef references `UniTask` and that production code is reachable. If the project has no production asmdefs (single Assembly-CSharp), move the two test files (`DeterministicRandomTests`, `AnchorPlacerTests`) under this asmdef folder and add an assembly reference via "Assembly Definition References" to any asmdef the production types live in. If production is in `Assembly-CSharp`, mark the test asmdef with "Override References" off and add `nunit.framework` — the test runner will include Assembly-CSharp automatically for EditMode in many setups; if types aren't found, create a thin `CoOp.Runtime` asmdef over `Assets/Scripts` and reference it.

- [ ] **Step 2: Run all EditMode tests**

Test Runner → Run All. Expected: `DeterministicRandomTests` + `AnchorPlacerTests` green.

- [ ] **Step 3: Commit checkpoint**

```
test: editmode assembly for world generation
```

---

## Task 17: Manual Unity setup + QA

**Files:** Unity scene/prefab/asset work (no code).

- [ ] **Step 1: WorldGenConfig asset** — Create → Configs → World Gen Config at `Assets/Resources/Configs/World/WorldGenConfig.asset`. For first test set `UseFixedSeed = true`.

- [ ] **Step 2: MicroWorld in Game.unity** — Add a MicroWorld object with `MapSpawner` + `TerrainSpawner` (required), `SurfaceSpawner`, `GrassSpawner`, and a `CellSpawner` whose `CellSpawnerPrefab` points to placeholder tree/rock prefabs (capsule/cube). `Locked = false`. Convert MicroWorld to URP (Start Screen → Convert To URP; enable Opaque Texture in URP settings).

- [ ] **Step 3: WorldRoot scene object** — GameObject `WorldRoot` + `NetworkObject` + `WorldNetworkController` in Game.unity.

- [ ] **Step 4: Game.unity essentials** — Main Camera + Directional Light present.

- [ ] **Step 5: GameSceneInstaller inspector** — assign `_microWorld`, `_playerPrefab`; confirm `_spawnPoints` field is gone.

- [ ] **Step 6: QA (single editor)** — Bootstrap → Host → ~7s loading → Game; terrain+trees+rocks+grass visible; player spawns on terrain at Base zone (no fall-through). Console shows seed.

- [ ] **Step 7: QA (ParrelSync, two editors)** — A Host, B Connect (Tools/CoOp/Playmode). Terrain identical in both; both players visible on same landscape. Late-join (B connects after A generated) sees same world.

- [ ] **Step 8: Final commit checkpoint**

```
feat(world): procedural Phase-1 location (MicroWorld + anchors + spawn)
```
