using System;
using Signals;
using TMPro;
using UI.Common;
using UnityEngine;

namespace UI.HUD
{
    public sealed class CannonHudPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class ChargeCell
        {
            [Tooltip("Panel shown when this cell holds a loaded corpse.")]
            public GameObject Filled;
            [Tooltip("Panel shown when this cell is empty (still to load).")]
            public GameObject Empty;

            public void Set(bool filled)
            {
                if (Filled != null) Filled.SetActive(filled);
                if (Empty != null) Empty.SetActive(!filled);
            }

            public void Hide()
            {
                if (Filled != null) Filled.SetActive(false);
                if (Empty != null) Empty.SetActive(false);
            }
        }

        [Serializable]
        public sealed class ModuleCell
        {
            [Tooltip("Whole cell (hidden if there is no module at this order).")]
            public GameObject Root;
            [Tooltip("Panel: module assembled, no mobs (intact / 'цел').")]
            public GameObject Intact;
            [Tooltip("Panel: module assembled with mobs on it ('под атакой').")]
            public GameObject UnderAttack;
            [Tooltip("Mob-count label on the under-attack panel ('×2').")]
            public TMP_Text MobCountLabel;
            [Tooltip("Panel: module not assembled / torn off ('оторван').")]
            public GameObject Torn;

            public void Apply(bool assembled, int mobCount)
            {
                if (Root != null) Root.SetActive(true);
                bool intact = assembled && mobCount <= 0;
                bool under = assembled && mobCount > 0;
                bool torn = !assembled;
                if (Intact != null) Intact.SetActive(intact);
                if (UnderAttack != null) UnderAttack.SetActive(under);
                if (Torn != null) Torn.SetActive(torn);
                if (under && MobCountLabel != null) MobCountLabel.text = $"×{mobCount}";
            }

            public void Hide()
            {
                if (Root != null) Root.SetActive(false);
            }
        }

        [Header("Root / visibility")]
        [Tooltip("Visual content toggled on/off. MUST be a child — keep the CannonHudPanel component on an always-active parent so its proximity check keeps running.")]
        [SerializeField] private GameObject _root;
        [Tooltip("Show only when the local camera is within this distance of the cannon. 0 = always show once cannon data exists.")]
        [SerializeField] private float _proximityDistance = 12f;

        [Header("Header")]
        [SerializeField] private TMP_Text _assemblyLabel;

        [Header("Charge")]
        [SerializeField] private TMP_Text _chargeLabel;
        [SerializeField] private ChargeCell[] _chargeCells;
        [Tooltip("Line shown only during a wave: how many corpses still to load.")]
        [SerializeField] private GameObject _waveInfoRoot;
        [SerializeField] private TMP_Text _waveInfoLabel;

        [Header("Modules (one cell per order; index 0 = module 1)")]
        [SerializeField] private ModuleCell[] _moduleCells;

        private int _loaded;
        private int _required;
        private bool _waveActive;
        private bool _hasData;
        private int _lastLoaded;

        private void Awake() => SetVisibleInternal(false);

        public void SetAssembly(int assembled, int total)
        {
            _hasData = true;
            if (_assemblyLabel != null) _assemblyLabel.text = $"СБОРКА {assembled} / {total}";
        }

        public void SetCharge(int loaded, int required)
        {
            _loaded = loaded;
            _required = required;
            _hasData = true;
            if (_chargeLabel != null) _chargeLabel.text = $"{loaded} / {Mathf.Max(0, required)}";
            if (_chargeCells != null)
            {
                for (int i = 0; i < _chargeCells.Length; i++)
                {
                    if (_chargeCells[i] == null) continue;
                    if (i < required) _chargeCells[i].Set(i < loaded);
                    else _chargeCells[i].Hide();
                }
            }
            if (loaded > _lastLoaded && _root != null) UITween.Punch(_root.transform, 0.18f, 0.25f);
            _lastLoaded = loaded;
            RefreshWaveInfo();
        }

        public void SetModules(CannonModuleState[] modules)
        {
            _hasData = true;
            if (_moduleCells == null) return;
            for (int i = 0; i < _moduleCells.Length; i++)
            {
                var cell = _moduleCells[i];
                if (cell == null) continue;
                if (modules != null && i < modules.Length) cell.Apply(modules[i].Assembled, modules[i].MobCount);
                else cell.Hide();
            }
        }

        public void SetWaveActive(bool active)
        {
            _waveActive = active;
            RefreshWaveInfo();
        }

        private void RefreshWaveInfo()
        {
            int remaining = Mathf.Max(0, _required - _loaded);
            bool show = _waveActive && _required > 0 && remaining > 0;
            if (_waveInfoRoot != null) _waveInfoRoot.SetActive(show);
            if (show && _waveInfoLabel != null) _waveInfoLabel.text = $"Осталось собрать: {remaining}";
        }

        private void Update()
        {
            if (!_hasData) { SetVisibleInternal(false); return; }
            if (_proximityDistance <= 0f) { SetVisibleInternal(true); return; }

            var slots = Gameplay.World.Weapon.WeaponModuleSlot.All;
            if (slots == null || slots.Count == 0 || slots[0] == null) { SetVisibleInternal(false); return; }
            var cam = Camera.main;
            if (cam == null) { SetVisibleInternal(true); return; }
            float d = Vector3.Distance(cam.transform.position, slots[0].transform.position);
            SetVisibleInternal(d <= _proximityDistance);
        }

        private void SetVisibleInternal(bool show)
        {
            if (_root != null && _root.activeSelf != show) _root.SetActive(show);
        }
    }
}
