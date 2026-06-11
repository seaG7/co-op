using UnityEngine;

namespace Data
{
    public static class Platform
    {
        public static bool IsDedicatedServer
        {
            get
            {
#if UNITY_EDITOR
                return false;
#elif UNITY_SERVER
                return true;
#else
                return Application.isBatchMode;
#endif
            }
        }
    }
}
