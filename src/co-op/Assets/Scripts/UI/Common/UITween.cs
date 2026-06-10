using DG.Tweening;
using UnityEngine;

namespace UI.Common
{
    public static class UITween
    {
        public const float HoverDuration = 0.14f;
        public const float PressDuration = 0.07f;
        public const float WindowShowDuration = 0.26f;
        public const float WindowHideDuration = 0.16f;

        public static readonly Ease HoverEase = Ease.OutBack;
        public static readonly Ease PressEase = Ease.OutQuad;

        public static Tween ScaleTo(Transform target, Vector3 scale, float duration, Ease ease)
        {
            if (target == null) return null;
            target.DOKill();
            return target.DOScale(scale, duration).SetEase(ease).SetUpdate(true).SetLink(target.gameObject);
        }

        public static Tween Fade(CanvasGroup group, float alpha, float duration, Ease ease = Ease.OutQuad)
        {
            if (group == null) return null;
            group.DOKill();
            return DOTween.To(() => group.alpha, a => group.alpha = a, alpha, duration)
                .SetTarget(group).SetEase(ease).SetUpdate(true).SetLink(group.gameObject);
        }

        public static Tween Punch(Transform target, float strength = 0.18f, float duration = 0.3f)
        {
            if (target == null) return null;
            target.DOKill();
            return target.DOPunchScale(Vector3.one * strength, duration, vibrato: 6, elasticity: 0.6f)
                .SetUpdate(true).SetLink(target.gameObject);
        }
    }
}
