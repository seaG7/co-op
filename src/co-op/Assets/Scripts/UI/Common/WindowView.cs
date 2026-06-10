using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Common
{
    public abstract class WindowView : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;

        public abstract void BindPresenter();
        public abstract void UnbindPresenter();

        public virtual void PlayShow()
        {
            EnsureRefs();
            EnsureButtonAnimators();
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = 0f;
            UITween.Fade(_canvasGroup, 1f, UITween.WindowShowDuration, Ease.OutQuad);
        }

        public virtual void PlayHide(Action onComplete)
        {
            EnsureRefs();
            if (_canvasGroup == null) { onComplete?.Invoke(); return; }
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            var t = UITween.Fade(_canvasGroup, 0f, UITween.WindowHideDuration, Ease.InQuad);
            if (t != null) t.OnComplete(() => onComplete?.Invoke());
            else onComplete?.Invoke();
        }

        private void EnsureRefs()
        {
            if (_canvasGroup != null) return;
            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void EnsureButtonAnimators()
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null && !buttons[i].TryGetComponent<UIButtonAnimator>(out _))
                    buttons[i].gameObject.AddComponent<UIButtonAnimator>();
        }
    }
}
