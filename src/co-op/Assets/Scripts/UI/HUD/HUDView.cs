using Signals;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.HUD
{
    public class HUDView : WindowView<HUDPresenter>
    {
        [SerializeField] private TMP_Text _statusLabel;

        [Header("FPS")]
        [Tooltip("Optional label showing the smoothed frames-per-second. Leave unassigned to disable.")]
        [SerializeField] private TMP_Text _fpsLabel;
        [Tooltip("Seconds between FPS text refreshes (averaged over this window).")]
        [SerializeField] private float _fpsUpdateInterval = 0.25f;

        private float _fpsElapsed;
        private int _fpsFrames;

        [Header("Interact prompt")]
        [Tooltip("Root object of the interact prompt (shown/hidden).")]
        [SerializeField] private GameObject _interactPrompt;
        [Tooltip("Optional label for the prompt text. If unassigned, the prompt is just toggled.")]
        [SerializeField] private TMP_Text _interactPromptLabel;

        [Header("Round phase")]
        [Tooltip("Root of the gather/prepare timer (shown during the Gather phase).")]
        [SerializeField] private GameObject _gatherRoot;
        [Tooltip("Filled Image (Image Type = Filled) for the gather countdown.")]
        [SerializeField] private Image _gatherFill;
        [SerializeField] private TMP_Text _gatherLabel;
        [Tooltip("Shown while the cannon is charged AND the source is exposed — 'break the Source now'.")]
        [SerializeField] private GameObject _shootNowRoot;

        [Header("Weapon")]
        [Tooltip("Crosshair shown while operating the weapon.")]
        [SerializeField] private GameObject _crosshair;

        [Header("Cannon charge")]
        [Tooltip("Root of the corpse-charge readout (X/N).")]
        [SerializeField] private GameObject _chargeRoot;
        [Tooltip("Filled Image (Image Type = Filled) for the charge fraction.")]
        [SerializeField] private Image _chargeFill;
        [SerializeField] private TMP_Text _chargeLabel;
        [Tooltip("Panel shown while the local player is carrying a mob corpse (prompt to charge the cannon — 'Зарядите пушку').")]
        [SerializeField] private GameObject _chargePromptRoot;

        [Header("Cannon modules")]
        [Tooltip("Shown while any cannon module is under attack or detached.")]
        [SerializeField] private GameObject _modulesWarnRoot;
        [SerializeField] private TMP_Text _modulesWarnLabel;

        [Header("Combat")]
        [Tooltip("Hint shown when an enemy is within melee range (e.g. 'Melee (F)').")]
        [SerializeField] private GameObject _meleePromptRoot;

        [Header("Downed")]
        [Tooltip("Overlay shown to the local player when they are caught/downed.")]
        [SerializeField] private GameObject _downedSelfRoot;
        [Tooltip("Call-to-action shown when the partner is downed and needs rescue.")]
        [SerializeField] private GameObject _partnerDownedRoot;

        [Header("Cannon panel")]
        [SerializeField] private CannonHudPanel _cannonPanel;

        private int _lastLoaded;

        public void SetStatus(string s)
        {
            if (_statusLabel != null) _statusLabel.text = s;
        }

        private void Update()
        {
            if (_fpsLabel == null) return;
            _fpsElapsed += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsElapsed < _fpsUpdateInterval) return;
            float fps = _fpsFrames / _fpsElapsed;
            _fpsLabel.text = $"{Mathf.RoundToInt(fps)} FPS";
            _fpsElapsed = 0f;
            _fpsFrames = 0;
        }

        public void SetInteractPrompt(bool show, string text = null)
        {
            if (_interactPrompt != null) _interactPrompt.SetActive(show);
            if (show && text != null && _interactPromptLabel != null) _interactPromptLabel.text = text;
        }

        public void SetGather(bool show, float remaining = 0f, float total = 0f)
        {
            if (_gatherRoot != null) _gatherRoot.SetActive(show);
            if (!show) return;
            if (_gatherFill != null) _gatherFill.fillAmount = total > 0.01f ? Mathf.Clamp01(remaining / total) : 0f;
            if (_gatherLabel != null) _gatherLabel.text = $"Wave in {Mathf.CeilToInt(Mathf.Max(0f, remaining))}s";
        }

        public void SetShootNow(bool show)
        {
            if (_shootNowRoot != null) _shootNowRoot.SetActive(show);
        }

        public void SetCrosshair(bool on)
        {
            if (_crosshair != null) _crosshair.SetActive(on);
        }

        public void SetCharge(int loaded, int required)
        {
            if (_chargeRoot != null) _chargeRoot.SetActive(required > 0);
            if (_chargeFill != null) _chargeFill.fillAmount = required > 0 ? Mathf.Clamp01((float)loaded / required) : 0f;
            if (_chargeLabel != null) _chargeLabel.text = $"Charge {loaded}/{Mathf.Max(0, required)}";
            if (loaded > _lastLoaded && _chargeRoot != null) UITween.Punch(_chargeRoot.transform, 0.22f, 0.35f);
            _lastLoaded = loaded;
            if (_cannonPanel != null) _cannonPanel.SetCharge(loaded, required);
        }

        public void SetChargePrompt(bool show)
        {
            if (_chargePromptRoot != null) _chargePromptRoot.SetActive(show);
        }

        public void SetModulesWarning(int underAttack, int detached)
        {
            bool show = underAttack > 0 || detached > 0;
            if (_modulesWarnRoot != null) _modulesWarnRoot.SetActive(show);
            if (!show || _modulesWarnLabel == null) return;
            if (detached > 0 && underAttack > 0) _modulesWarnLabel.text = $"Cannon: {detached} down, {underAttack} under attack!";
            else if (detached > 0) _modulesWarnLabel.text = $"Cannon: {detached} module(s) down — reinstall!";
            else _modulesWarnLabel.text = $"Cannon under attack ({underAttack})!";
        }

        public void SetCannonModules(CannonModuleState[] modules, int assembled, int total)
        {
            if (_cannonPanel == null) return;
            _cannonPanel.SetAssembly(assembled, total);
            _cannonPanel.SetModules(modules);
        }

        public void SetCannonWaveActive(bool active)
        {
            if (_cannonPanel != null) _cannonPanel.SetWaveActive(active);
        }

        public void SetMeleePrompt(bool show)
        {
            if (_meleePromptRoot != null) _meleePromptRoot.SetActive(show);
        }

        public void SetDownedSelf(bool show)
        {
            if (_downedSelfRoot != null) _downedSelfRoot.SetActive(show);
        }

        public void SetPartnerDowned(bool show)
        {
            if (_partnerDownedRoot != null) _partnerDownedRoot.SetActive(show);
        }

        protected override void OnBound()
        {
            SetInteractPrompt(false);
            SetGather(false);
            SetShootNow(false);
            SetCrosshair(false);
            SetCharge(0, 0);
            SetChargePrompt(false);
            SetModulesWarning(0, 0);
            SetMeleePrompt(false);
            SetDownedSelf(false);
            SetPartnerDowned(false);
        }
    }
}
