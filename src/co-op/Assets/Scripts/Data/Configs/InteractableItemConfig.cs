using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Interactable Item Config", fileName = "InteractableItemConfig")]
    public sealed class InteractableItemConfig : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("The prefab to spawn for this item. Must have NetworkObject + Rigidbody + Carryable.")]
        public GameObject Prefab;

        [Header("Physics")]
        [Tooltip("Item mass (kg). Applied to the Rigidbody at runtime, also gates player movement speed (heavier = slower).")]
        public float Mass = 2f;

        [Header("Carry")]
        [Tooltip("Player linearVelocity (m/s) above which the held item is forcibly detached. Heavy items should have low MaxCarrySpeed so sprint/jump drops them.")]
        public float MaxCarrySpeed = 5f;
        [Tooltip("Distance from the camera at which the item floats while held (meters). 0 = use CarryConfig default.")]
        public float HoldDistance = 0f;

        [Header("Two-handed (future)")]
        [Tooltip("Number of simultaneous holders required to lift this item. MVP supports only 1; field reserved for post-MVP two-handed carry.")]
        [Min(1)] public int MinHolders = 1;
    }
}
