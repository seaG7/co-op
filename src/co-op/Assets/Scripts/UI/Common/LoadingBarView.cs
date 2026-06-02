using UnityEngine;
using UnityEngine.UI;

namespace UI.Common
{
    public class LoadingBarView : WindowView<EmptyPresenter>
    {
        [SerializeField] private Slider _progressBar;

        public void SetProgress(float p)
        {
            if (_progressBar != null) _progressBar.value = Mathf.Clamp01(p);
        }
    }
}
