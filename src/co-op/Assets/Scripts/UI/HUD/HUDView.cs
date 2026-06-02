using TMPro;
using UI.Common;
using UnityEngine;

namespace UI.HUD
{
    public class HUDView : WindowView<HUDPresenter>
    {
        [SerializeField] private TMP_Text _statusLabel;

        [Header("Interact prompt")]
        [Tooltip("Root object of the 'press E to pick up' prompt. Its text is authored in the prefab.")]
        [SerializeField] private GameObject _interactPrompt;

        public void SetStatus(string s)
        {
            if (_statusLabel != null) _statusLabel.text = s;
        }

        public void SetInteractPrompt(bool show)
        {
            if (_interactPrompt != null) _interactPrompt.SetActive(show);
        }

        protected override void OnBound() => SetInteractPrompt(false);
    }
}
