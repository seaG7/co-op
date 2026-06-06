using TMPro;
using UI.Common;
using UnityEngine;

namespace UI.HUD
{
    public class HUDView : WindowView<HUDPresenter>
    {
        [SerializeField] private TMP_Text _statusLabel;

        [Header("Interact prompt")]
        [Tooltip("Root object of the interact prompt (shown/hidden).")]
        [SerializeField] private GameObject _interactPrompt;
        [Tooltip("Optional label for the prompt text. If unassigned, the prompt is just toggled and keeps its prefab-authored text.")]
        [SerializeField] private TMP_Text _interactPromptLabel;

        public void SetStatus(string s)
        {
            if (_statusLabel != null) _statusLabel.text = s;
        }

        public void SetInteractPrompt(bool show, string text = null)
        {
            if (_interactPrompt != null) _interactPrompt.SetActive(show);
            if (show && text != null && _interactPromptLabel != null) _interactPromptLabel.text = text;
        }

        protected override void OnBound() => SetInteractPrompt(false);
    }
}
