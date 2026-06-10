using Data.UI;
using Infrastructure.Services.UI;
using UI.Common;
using UnityEngine;

namespace Infrastructure.Factories.UI
{
    public interface IUIFactory
    {
        WindowView CreateScreen(WindowID id);
        void DestroyScreen(WindowID id);
        T GetView<T>(WindowID id) where T : Component;
        bool Exists(WindowID id);
    }
}
