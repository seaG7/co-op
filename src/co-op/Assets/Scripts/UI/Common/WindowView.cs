using UnityEngine;

namespace UI.Common
{
    public abstract class WindowView : MonoBehaviour
    {
        public abstract void BindPresenter();
        public abstract void UnbindPresenter();
    }
}
