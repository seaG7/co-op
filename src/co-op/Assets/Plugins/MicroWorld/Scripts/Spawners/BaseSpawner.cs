using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary> 
    /// Base class for spawners 
    /// </summary>
    public class BaseSpawner : MonoBehaviour
    {
        public virtual int Order => 1000;
        protected MicroWorld Builder { get; set; }
        protected Map Map => Builder?.Map;
        protected Terrain Terrain => Builder?.Terrain;
        protected TerrainLayersBuilder TerrainLayersBuilder => Builder?.TerrainLayersBuilder;
        protected ICellGeometry CellGeometry { get; set; }

        protected Rnd rootRnd;
        protected IEnumerable<string> ProposedCellTypes => this.ProposedCellTypes();

        private void Start()
        {
            // to show enabled checkbox in inspector
        }

        public virtual IEnumerator Prepare(MicroWorld builder)
        {
            this.Builder = builder;
            CellGeometry = Builder.CellGeometry;
            rootRnd = new Rnd(builder.Seed).GetBranch(name);
            debugPoints.Clear();

            yield return null;
        }

        public virtual IEnumerator Build(MicroWorld builder)
        {
            yield return null;
        }

        #region Service functions

        protected float SampleHeight(Vector3 pos)
        {
            return Terrain.SampleHeight(pos) + Builder.transform.position.y;
        }

        protected void CheckNotNull(System.Object obj, string msg)
        {
            if (obj == null)
                throw new ApplicationException(msg);
        }

        protected void CheckMapSpawner()
        {
            CheckNotNull(Map, $"{nameof(MapSpawner)} is not found");
        }

        protected void CheckTerrainSpawner()
        {
            CheckNotNull(Builder?.TerrainSpawner, $"{nameof(TerrainSpawner)} is not found");
        }

        #endregion

        public virtual void OnBuildCompleted()
        {
        }

        #region Debug
        protected List<Vector3> debugPoints = new List<Vector3>();

        void OnDrawGizmosSelected()
        {
            for (int i = 0; i < debugPoints.Count; i += 2)
                Gizmos.DrawRay(debugPoints[i], debugPoints[i + 1] - debugPoints[i]);
        }
        #endregion
    }
}
