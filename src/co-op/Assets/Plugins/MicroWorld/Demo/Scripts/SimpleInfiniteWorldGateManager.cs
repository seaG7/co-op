using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public class SimpleInfiniteWorldGateManager : BaseGateManager
    {
        [SerializeField] MicroWorld MicroWorldPrefab;
        [SerializeField] int StartSeed = 1;
        const int KeepWorldsCount = 1;

        Dictionary<int, MicroWorld> worldsBySeed = new Dictionary<int, MicroWorld>();
        MicroWorld currentWorld;

        private IEnumerator Start()
        {
            // build first world
            currentWorld = GetOrBuild(StartSeed);
            MicroWorld.FlushBuild();// force fast build mode

            // wait for the world to be built
            while (!currentWorld.IsBuilt)
                yield return null;

            // activate world
            currentWorld.Terrain.gameObject.SetActive(true);

            // start to build prev and next worlds
            GetOrBuild(currentWorld.Seed + 1);
            GetOrBuild(currentWorld.Seed - 1);
        }

        protected override IEnumerator OnGatePassed(Gate gate, GameObject player)
        {
            var deltaSeed = gate.GateInfo.WorldSide == WorldSide.North ? +1 : -1;

            // disable current world
            currentWorld.Terrain?.gameObject.SetActive(false);

            // enable next world
            currentWorld = GetOrBuild(currentWorld.Seed + deltaSeed);

            // wait for the world to be built
            while (!currentWorld.IsBuilt)
                yield return null;

            // activate world
            currentWorld.Terrain.gameObject.SetActive(true);

            // teleport player to new world
            var targetGate = currentWorld.Gates.First(g => g.WorldSide != gate.GateInfo.WorldSide);
            TeleportPlayer(targetGate.Gate, player);

            // start to build next world
            GetOrBuild(currentWorld.Seed + deltaSeed);

            // destroy far worlds
            DestroyFarWorlds(currentWorld);
        }

        private MicroWorld GetOrBuild(int seed)
        {
            if (!worldsBySeed.TryGetValue(seed, out var world))
            {
                // create new MicroWorld
                world = Instantiate(MicroWorldPrefab);

                // assign seed
                world.Seed = seed;
                worldsBySeed[seed] = world;

                // start build
                world.BuildAsync();
            }

            return world;
        }

        private void DestroyFarWorlds(MicroWorld currentWorld)
        {
            var maxSeed = currentWorld.Seed + KeepWorldsCount;
            var minSeed = currentWorld.Seed - KeepWorldsCount;
            foreach (var seed in worldsBySeed.Keys.ToArray())
                if (seed < minSeed || seed > maxSeed)
                {
                    var go = worldsBySeed[seed].gameObject;
                    worldsBySeed.Remove(seed);
                    try
                    {
                        Destroy(go);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
        }

    }
}
