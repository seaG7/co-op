using System;
using System.Collections.Generic;
using Infrastructure.Services.UI;
using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Windows Config", fileName = "WindowsConfig")]
    public class WindowsConfig : ScriptableObject
    {
        public List<WindowRecord> windows = new();
    }

    [Serializable]
    public class WindowRecord
    {
        public WindowID windowID;
        public GameObject prefab;
    }
}
