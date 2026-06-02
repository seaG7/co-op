using UnityEngine;

namespace MicroWorldNS
{
    [RequireComponent(typeof(CharacterController))]
    public class FreeCameraChar : MonoBehaviour
    {
        [SerializeField] protected new Transform camera;
        [SerializeField] protected CharacterController character;
        /// <summary>
        /// Rotation speed when using a controller.
        /// </summary>
        public float m_LookSpeedController = 120f;
        /// <summary>
        /// Rotation speed when using the mouse.
        /// </summary>
        public float m_LookSpeedMouse = 4.0f;
        /// <summary>
        /// Movement speed.
        /// </summary>
        public float m_MoveSpeed = 10.0f;
        /// <summary>
        /// Value added to the speed when incrementing.
        /// </summary>
        public float m_MoveSpeedIncrement = 2.5f;
        /// <summary>
        /// Scale factor of the turbo mode.
        /// </summary>
        public float m_TurboCoeff = 10.0f;

        public float m_WalkCoeff = 0.3f;

        [SerializeField] float minHeight = 2;
        [SerializeField] float checkHeight = 1;
        [SerializeField] LayerMask collisionMask = 1;
        [SerializeField] float liftSpeed = 10;
        [SerializeField] bool useLeftMouseButton = true;

        private static string kMouseX = "Mouse X";
        private static string kMouseY = "Mouse Y";
        private static string kVertical = "Vertical";
        private static string kHorizontal = "Horizontal";
        private static string kJump = "Jump";

        float inputRotateAxisX, inputRotateAxisY;
        float inputChangeSpeed;
        float inputVertical, inputHorizontal, inputY;
        bool leftShiftBoost, leftShift, walk;
        Vector3 playerVelocity;

        void UpdateInputs()
        {
            inputRotateAxisX = 0.0f;
            inputRotateAxisY = 0.0f;
            leftShiftBoost = false;

            if (Input.GetMouseButton(1) || (Input.GetMouseButton(0) && useLeftMouseButton))
            {
                leftShiftBoost = true;
                inputRotateAxisX = Input.GetAxis(kMouseX) * m_LookSpeedMouse;
                inputRotateAxisY = Input.GetAxis(kMouseY) * m_LookSpeedMouse;

                Cursor.visible = false;
            }
            else
                Cursor.visible = true;

            leftShift = Input.GetKey(KeyCode.LeftShift);
            if (Input.GetKeyDown(KeyCode.LeftControl))
                walk = !walk;

            inputVertical = Input.GetAxis(kVertical);
            inputHorizontal = Input.GetAxis(kHorizontal);
            inputY = Input.GetButton(kJump) ? 1 : 0;
        }

        void Update()
        {
            UpdateInputs();
            UpdateCharacter();
        }

        private void UpdateCharacter()
        {
            if (inputChangeSpeed != 0.0f)
            {
                m_MoveSpeed += inputChangeSpeed * m_MoveSpeedIncrement;
                if (m_MoveSpeed < m_MoveSpeedIncrement) m_MoveSpeed = m_MoveSpeedIncrement;
            }

            float rotationX = camera.localEulerAngles.x;
            float newRotationY = camera.localEulerAngles.y + inputRotateAxisX;

            // Weird clamping code due to weird Euler angle mapping...
            float newRotationX = (rotationX - inputRotateAxisY);
            if (rotationX <= 90.0f && newRotationX >= 0.0f)
                newRotationX = Mathf.Clamp(newRotationX, 0.0f, 90.0f);
            if (rotationX >= 270.0f)
                newRotationX = Mathf.Clamp(newRotationX, 270.0f, 360.0f);

            camera.localRotation = Quaternion.Euler(newRotationX, newRotationY, camera.localEulerAngles.z);

            float moveSpeed = m_MoveSpeed;
            if (leftShiftBoost && leftShift)
                moveSpeed *= m_TurboCoeff;
            if (leftShiftBoost && walk)
                moveSpeed *= m_WalkCoeff;

            playerVelocity = camera.forward * moveSpeed * inputVertical;
            playerVelocity += camera.right * moveSpeed * inputHorizontal;
            playerVelocity += Vector3.up * moveSpeed * inputY;

            character.Move(playerVelocity * Time.deltaTime);
        }

        // Update is called once per frame
        void LateUpdate()
        {
            var ray = new Ray(transform.position + Vector3.up * checkHeight, Vector3.down);
            if (Physics.Raycast(ray, out var hit, 2000, collisionMask, QueryTriggerInteraction.Ignore))
            {
                var point = ray.GetPoint(hit.distance);
                if (point.y + minHeight > transform.position.y)
                {
                    character.enabled = false;
                    transform.position = Vector3.Lerp(transform.position, new Vector3(transform.position.x, point.y + minHeight, transform.position.z), liftSpeed * Time.deltaTime);
                    character.enabled = true;
                }
            }
        }

        private void Awake()
        {
            BaseGateManager.OnTeleportNeeded += Teleport;
        }

        private void OnDestroy()
        {
            BaseGateManager.OnTeleportNeeded -= Teleport;
        }

        public void Teleport(GameObject player, Vector3 pos, Quaternion rot)
        {
            character.enabled = false;
            transform.position = pos;
            camera.transform.rotation = rot;
            character.enabled = true;
        }
    }
}