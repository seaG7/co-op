using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Common
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIButtonAnimator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(1f)] private float _hoverScale = 1.1f;
        [SerializeField, Range(0.5f, 1f)] private float _pressScale = 0.9f;
        [SerializeField] private float _duration = 0.12f;
        [SerializeField] private Ease _ease = Ease.OutBack;
        [Tooltip("Optional. If set, animations are skipped while this Selectable is non-interactable. Auto-found.")]
        [SerializeField] private Selectable _selectable;

        private RectTransform _rt;
        private Vector3 _baseScale = Vector3.one;
        private bool _hovered;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
            if (_selectable == null) _selectable = GetComponent<Selectable>();
        }

        private void OnEnable()
        {
            _hovered = false;
            _rt.localScale = _baseScale;
        }

        private void OnDisable()
        {
            _rt.DOKill();
            _rt.localScale = _baseScale;
            _hovered = false;
        }

        private bool Interactable => _selectable == null || _selectable.interactable;

        private void To(float factor)
        {
            _rt.DOKill();
            _rt.DOScale(_baseScale * factor, _duration).SetEase(_ease).SetUpdate(true).SetLink(gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (Interactable) To(_hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (Interactable) To(1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Interactable) To(_pressScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Interactable) To(_hovered ? _hoverScale : 1f);
        }
    }
}
