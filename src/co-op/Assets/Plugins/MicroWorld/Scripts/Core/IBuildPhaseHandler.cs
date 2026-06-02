using System.Collections;

namespace MicroWorldNS
{
    public interface IBuildPhaseHandler
    {
        IEnumerator OnPhaseCompleted(BuildPhase phase);
    }

    public enum BuildPhase
    {
        SpawnersListIsReady, // after spawner list is created
        SpawnersArePrepared, // after spawner's Prepare methods are finished
        MapCreated, // after cell types and map are completely created (include CellModifiers)
        StartTerrainSpawner, // TerrainSpawner starts
        CellHeightsCreated,// after cell heights created
        BeforeTerrainBuilding,// after finally heights are created, but before terrain created
        TerrainHeightMapCreated,// before TerrainSpawner.Heights will be flushed to terrain mesh
        TerrainCreated,// after terrain created
        BuildCompleted
    }
}
