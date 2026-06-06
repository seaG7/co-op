using UnityEngine;
using Zenject;

namespace Infrastructure.Services.DI
{

    public sealed class SceneDiContainerRegistry : ISceneDiContainerRegistry
    {
        private DiContainer _current;

        public DiContainer Current => _current;

        public void SetCurrent(DiContainer container)
        {
            if (container == null)
            {
                Debug.LogWarning("[SceneDiContainerRegistry] SetCurrent called with null; ignored.");
                return;
            }

            if (_current != null && _current != container)
                Debug.LogWarning("[SceneDiContainerRegistry] Overwriting an existing scene container. " +
                                 "Expected the previous game scene to have cleared it on unload.");

            _current = container;
        }

        public void Clear(DiContainer container)
        {

            if (_current == container)
                _current = null;
        }
    }
}
