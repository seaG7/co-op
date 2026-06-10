using System;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.GameOver
{
    public class GameOverView : WindowView<GameOverPresenter>
    {
        [Tooltip("Optional outcome text (kept for compat). Leave unassigned and use the panels below if you prefer.")]
        [SerializeField] private TMP_Text _outcomeLabel;
        [Tooltip("Root shown on Victory (hidden on Defeat).")]
        [SerializeField] private GameObject _victoryRoot;
        [Tooltip("Root shown on Defeat (hidden on Victory).")]
        [SerializeField] private GameObject _defeatRoot;
        [SerializeField] private Button _backToMenuButton;
        [SerializeField] private Button _restartButton;

        public event Action BackToMenuClicked;
        public event Action RestartClicked;

        public void SetOutcome(string text, Color color)
        {
            if (_outcomeLabel == null) return;
            _outcomeLabel.text = text;
            _outcomeLabel.color = color;
        }

        public void SetOutcomePanels(bool victory, bool defeat)
        {
            if (_victoryRoot != null) _victoryRoot.SetActive(victory);
            if (_defeatRoot != null) _defeatRoot.SetActive(defeat);
        }

        protected override void OnBound()
        {
            if (_backToMenuButton != null)
                _backToMenuButton.onClick.AddListener(() => BackToMenuClicked?.Invoke());
            if (_restartButton != null)
                _restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
        }

        protected override void OnUnbinding()
        {
            if (_backToMenuButton != null)
                _backToMenuButton.onClick.RemoveAllListeners();
            if (_restartButton != null)
                _restartButton.onClick.RemoveAllListeners();
        }
    }
}
