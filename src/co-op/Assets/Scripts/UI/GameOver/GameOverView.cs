using System;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.GameOver
{
    public class GameOverView : WindowView<GameOverPresenter>
    {
        [SerializeField] private Button _backToMenuButton;

        public event Action BackToMenuClicked;

        protected override void OnBound()
        {
            if (_backToMenuButton != null)
                _backToMenuButton.onClick.AddListener(() => BackToMenuClicked?.Invoke());
        }

        protected override void OnUnbinding()
        {
            if (_backToMenuButton != null)
                _backToMenuButton.onClick.RemoveAllListeners();
        }
    }
}
