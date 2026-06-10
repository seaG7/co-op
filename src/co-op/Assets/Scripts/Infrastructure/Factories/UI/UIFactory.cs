using System.Collections.Generic;
using Data.UI;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.UI;
using UI.Common;
using UnityEngine;
using Zenject;

namespace Infrastructure.Factories.UI
{
    public sealed class UIFactory : IUIFactory
    {
        private readonly DiContainer _container;
        private readonly IConfigDataProvider _config;
        private readonly Dictionary<WindowID, WindowView> _opened = new();
        private Transform _uiRoot;

        public UIFactory(DiContainer container, IConfigDataProvider config)
        {
            _container = container;
            _config = config;
        }

        public WindowView CreateScreen(WindowID id)
        {
            if (_opened.TryGetValue(id, out var existing) && existing != null) return existing;

            var prefab = _config.GetWindowPrefab(id);
            if (prefab == null)
            {
                Debug.LogError($"[UIFactory] No prefab for {id}");
                return null;
            }

            EnsureUIRoot();
            var go = _container.InstantiatePrefab(prefab, _uiRoot);
            if (go == null)
            {
                Debug.LogError($"[UIFactory] Failed to instantiate prefab for {id}.");
                return null;
            }

            var view = go.GetComponent<WindowView>();
            if (view == null)
            {
                Debug.LogError($"[UIFactory] Prefab {prefab.name} has no WindowView component.");
                Object.Destroy(go);
                return null;
            }

            view.BindPresenter();
            view.PlayShow();
            _opened[id] = view;
            return view;
        }

        public void DestroyScreen(WindowID id)
        {
            if (!_opened.Remove(id, out var view) || view == null) return;
            view.UnbindPresenter();
            view.PlayHide(() => { if (view != null) Object.Destroy(view.gameObject); });
        }

        public T GetView<T>(WindowID id) where T : Component =>
            _opened.TryGetValue(id, out var v) && v != null ? v.GetComponent<T>() : null;

        public bool Exists(WindowID id) => _opened.TryGetValue(id, out var v) && v != null;

        private void EnsureUIRoot()
        {
            if (_uiRoot != null) return;
            var rootGo = new GameObject("UIRoot");
            Object.DontDestroyOnLoad(rootGo);
            _uiRoot = rootGo.transform;
        }
    }
}
