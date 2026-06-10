using Data.UI;
using UI.Common;
using UnityEngine;

namespace Infrastructure.Services.UI
{
    public interface IWindowService
    {
        bool IsWindowOpened(WindowID id);
        WindowView Open(WindowID id);
        T OpenAndGet<T>(WindowID id) where T : WindowView;
        T Get<T>(WindowID id) where T : Component;
        void Close(WindowID id);
    }
}
