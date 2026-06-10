using System;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.GameOver
{
    public class GameOverView : WindowView<GameOverPresenter>
    {
        [SerializeField] private TMP_Text _outcomeLabel;
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
