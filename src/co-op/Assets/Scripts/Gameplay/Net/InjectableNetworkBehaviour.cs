using System;
using Cysharp.Threading.Tasks;
using FishNet.Object;
using Infrastructure.Services.DI;
using UnityEngine;
using Zenject;

namespace Gameplay.Net
{

    public abstract class InjectableNetworkBehaviour : NetworkBehaviour, IRuntimeInjectable
    {
        private bool _injected;
        private bool _retrying;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            EnsureInjected();
        }

        public override void OnStopNetwork()
        {
            _injected = false;
            _retrying = false;
            base.OnStopNetwork();
        }

        public void EnsureInjected()
        {
            if (_injected) return;

            var container = ResolveSceneContainer();
            if (container == null)
            {
                if (!_retrying)
                {
                    _retrying = true;
                    RetryInjectAsync().Forget();
                }
                return;
            }

            DoInject(container);
        }

        private void DoInject(DiContainer container)
        {
            try
            {
                container.InjectGameObject(gameObject);
                _injected = true;
                OnInjected();
                NotifyInjectionListeners();
            }
            catch (Exception e)
            {
                Debug.LogError($"[InjectableNetworkBehaviour] Injection failed for '{name}': {e.Message}", this);
            }
        }

        protected virtual void OnInjected() { }

        private void NotifyInjectionListeners()
        {
            var listeners = GetComponents<IRuntimeInjectionListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                try { listeners[i].OnRuntimeInjected(); }
                catch (Exception e)
                {
                    Debug.LogError($"[InjectableNetworkBehaviour] OnRuntimeInjected failed on '{name}': {e.Message}", this);
                }
            }
        }

        private async UniTaskVoid RetryInjectAsync()
        {
            for (int i = 0; i < 900 && !_injected; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (this == null) return;
                if (_injected) return;
                var container = ResolveSceneContainer();
                if (container != null)
                {
                    DoInject(container);
                    _retrying = false;
                    return;
                }
            }

            _retrying = false;
            if (!_injected)
                Debug.LogWarning(
                    $"[InjectableNetworkBehaviour] Scene DiContainer never became available for '{name}'. " +
                    "Falling back to serialized data. (Is GameSceneInstaller present and ProjectContext alive?)",
                    this);
        }

        public void MarkAlreadyInjected() => _injected = true;

        private static DiContainer ResolveSceneContainer()
        {

            if (!ProjectContext.HasInstance) return null;

            var registry = ProjectContext.Instance.Container.TryResolve<ISceneDiContainerRegistry>();
            return registry?.Current;
        }
    }
}
