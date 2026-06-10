using DG.Tweening;
using UnityEngine;

namespace UI.Common
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIPanelPop : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float _fromScale = 0.85f;
        [SerializeField] private float _duration = 0.32f;
        [SerializeField] private float _delay = 0f;
        [SerializeField] private Ease _ease = Ease.OutBack;

        private RectTransform _rt;

        private void Awake() => _rt = (RectTransform)transform;

        private void OnEnable()
        {
            _rt.DOKill();
            _rt.localScale = Vector3.one * _fromScale;
            _rt.DOScale(Vector3.one, _duration).SetEase(_ease).SetDelay(_delay).SetUpdate(true).SetLink(gameObject);
        }

        private void OnDisable()
        {
            _rt.DOKill();
            _rt.localScale = Vector3.one;
        }
    }
}
