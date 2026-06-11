using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MimicSpace
{
    public class Mimic : MonoBehaviour
    {
        [Header("Animation")]
        public GameObject legPrefab;

        [Range(2, 20)]
        public int numberOfLegs = 5;
        [Tooltip("The number of splines per leg")]
        [Range(1, 10)]
        public int partsPerLeg = 4;
        int maxLegs;

        public int legCount;
        public int deployedLegs;
        [Range(0, 19)]
        public int minimumAnchoredLegs = 2;
        public int minimumAnchoredParts;

        [Tooltip("Minimum duration before leg is replaced")]
        public float minLegLifetime = 5;
        [Tooltip("Maximum duration before leg is replaced")]
        public float maxLegLifetime = 15;

        public Vector3 legPlacerOrigin = Vector3.zero;
        [Tooltip("Leg placement radius offset")]
        public float newLegRadius = 3;

        public float minLegDistance = 4.5f;
        public float maxLegDistance = 6.3f;

        [Range(2, 50)]
        [Tooltip("Number of spline samples per legpart")]
        public int legResolution = 40;

        [Tooltip("Minimum lerp coeficient for leg growth smoothing")]
        public float minGrowCoef = 4.5f;
        [Tooltip("MAximum lerp coeficient for leg growth smoothing")]
        public float maxGrowCoef = 6.5f;

        [Tooltip("Minimum duration before a new leg can be placed")]
        public float newLegCooldown = 0.3f;

        bool canCreateLeg = true;

        // --- Cling mode: set by the enemy when latched onto a player; legs grab the given body
        // parts (hands/head/...) and track them instead of planting on the ground. ---
        private List<Transform> _clingTargets;
        private bool _clinging;
        private int _clingCursor;

        public void SetCling(IReadOnlyList<Transform> targets)
        {
            _clingTargets = new List<Transform>();
            if (targets != null)
                for (int i = 0; i < targets.Count; i++)
                    if (targets[i] != null) _clingTargets.Add(targets[i]);
            _clinging = _clingTargets.Count > 0;
        }

        public void ClearCling()
        {
            _clinging = false;
            _clingTargets = null;
            foreach (Leg leg in GetComponentsInChildren<Leg>())
                leg.footTarget = null;
        }

        List<GameObject> availableLegPool = new List<GameObject>();

        [Tooltip("This must be updates as the Mimin moves to assure great leg placement")]
        public Vector3 velocity;

        [Tooltip("Layers the legs/body may stand on. EXCLUDE the Player and Enemy layers so feet never anchor to players or other mimics — only world geometry/cannon.")]
        public LayerMask groundMask = ~0;

        void Start()
        {
            ResetMimic();
        }

        private void OnValidate()
        {
            ResetMimic();
        }

        private void ResetMimic()
        {
            foreach (Leg g in GetComponentsInChildren<Leg>())
            {
                Destroy(g.gameObject);
            }
            legCount = 0;
            deployedLegs = 0;

            maxLegs = numberOfLegs * partsPerLeg;
            float rot = 360f / maxLegs;
            Vector2 randV = Random.insideUnitCircle;
            velocity = new Vector3(randV.x, 0, randV.y);
            minimumAnchoredParts = partsPerLeg;
            maxLegDistance = newLegRadius * 2.1f;

        }

        IEnumerator NewLegCooldown()
        {
            canCreateLeg = false;
            yield return new WaitForSeconds(newLegCooldown);
            canCreateLeg = true;
        }

        // Update is called once per frame
        void Update()
        {
            if (_clinging) { UpdateCling(); return; }

            if (!canCreateLeg)
                return;

            // New leg origin is placed in front of the mimic
            legPlacerOrigin = transform.position + velocity.normalized * newLegRadius;

            if (legCount <= maxLegs - partsPerLeg)
            {
                // Offset The leg origin by a random vector
                Vector2 offset = Random.insideUnitCircle * newLegRadius;
                Vector3 newLegPosition = legPlacerOrigin + new Vector3(offset.x, 0, offset.y);

                // If the mimic is moving and the new leg position is behind it, mirror it to make
                // it reach in front of the mimic.
                if (velocity.magnitude > 1f)
                {
                    float newLegAngle = Vector3.Angle(velocity, newLegPosition - transform.position);

                    if (Mathf.Abs(newLegAngle) > 90)
                    {
                        newLegPosition = transform.position - (newLegPosition - transform.position);
                    }
                }

                if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(legPlacerOrigin.x, 0, legPlacerOrigin.z)) < minLegDistance)
                    newLegPosition = ((newLegPosition - transform.position).normalized * minLegDistance) + transform.position;

                // if the angle is too big, adjust the new leg position towards the velocity vector
                if (Vector3.Angle(velocity, newLegPosition - transform.position) > 45)
                    newLegPosition = transform.position + ((newLegPosition - transform.position) + velocity.normalized * (newLegPosition - transform.position).magnitude) / 2f;

                RaycastHit hit;
                Physics.Raycast(newLegPosition + Vector3.up * 10f, -Vector3.up, out hit, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore);
                Vector3 myHit = hit.point;
                if (Physics.Linecast(transform.position, hit.point, out hit, groundMask, QueryTriggerInteraction.Ignore))
                    myHit = hit.point;

                float lifeTime = Random.Range(minLegLifetime, maxLegLifetime);
                float deployScale = 1f + velocity.magnitude * 0.6f;

                StartCoroutine("NewLegCooldown");
                for (int i = 0; i < partsPerLeg; i++)
                {
                    RequestLeg(myHit, legResolution, maxLegDistance, Random.Range(minGrowCoef, maxGrowCoef) * deployScale, this, lifeTime);
                    if (legCount >= maxLegs)
                        return;
                }
            }
        }

        // object pooling to limit leg instantiation
        void RequestLeg(Vector3 footPosition, int legResolution, float maxLegDistance, float growCoef, Mimic myMimic, float lifeTime)
        {
            GameObject newLeg;
            if (availableLegPool.Count > 0)
            {
                newLeg = availableLegPool[availableLegPool.Count - 1];
                availableLegPool.RemoveAt(availableLegPool.Count - 1);
            }
            else
            {
                newLeg = Instantiate(legPrefab, transform.position, Quaternion.identity);
            }
            newLeg.SetActive(true);
            Leg legComp = newLeg.GetComponent<Leg>();
            legComp.footTarget = null;
            legComp.Initialize(footPosition, legResolution, maxLegDistance, growCoef, myMimic, lifeTime);
            newLeg.transform.SetParent(myMimic.transform);
        }

        void UpdateCling()
        {
            if (!canCreateLeg || _clingTargets == null || _clingTargets.Count == 0) return;
            if (legCount > maxLegs - 1) return;

            Transform target = _clingTargets[_clingCursor % _clingTargets.Count];
            _clingCursor++;
            if (target == null) return;

            float lifeTime = Random.Range(minLegLifetime, maxLegLifetime);
            StartCoroutine("NewLegCooldown");
            RequestClingLeg(target, Random.Range(minGrowCoef, maxGrowCoef), lifeTime);
        }

        void RequestClingLeg(Transform target, float growCoef, float lifeTime)
        {
            GameObject newLeg;
            if (availableLegPool.Count > 0)
            {
                newLeg = availableLegPool[availableLegPool.Count - 1];
                availableLegPool.RemoveAt(availableLegPool.Count - 1);
            }
            else
            {
                newLeg = Instantiate(legPrefab, transform.position, Quaternion.identity);
            }
            newLeg.SetActive(true);
            Leg leg = newLeg.GetComponent<Leg>();
            leg.footTarget = target;
            leg.Initialize(target.position, legResolution, maxLegDistance, growCoef, this, lifeTime);
            newLeg.transform.SetParent(transform);
        }

        public void RecycleLeg(GameObject leg)
        {
            availableLegPool.Add(leg);
            leg.SetActive(false);
        }
    }

}