using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FarmSimulator.Visual
{
    /// <summary>
    /// Simple character camera with switchable first/third-person modes.
    /// Keeps a lightweight API so the rest of the farm slice can still call focus methods.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public enum CameraMode
        {
            ThirdPerson,
            FirstPerson
        }

        [Header("References")]
        [SerializeField] private Transform _target;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Renderer[] _firstPersonHiddenRenderers;

        [Header("View")]
        [SerializeField] private CameraMode _defaultMode = CameraMode.ThirdPerson;
        [SerializeField] private Vector3 _thirdPersonOffset = new(0f, 3.15f, -5.6f);
        [SerializeField] private Vector3 _firstPersonOffset = new(0f, 1.6f, 0.12f);
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _minPitch = -30f;
        [SerializeField] private float _maxPitch = 65f;
        [SerializeField] private float _positionSmooth = 12f;
        [SerializeField] private float _rotationSmooth = 14f;
        [SerializeField] private bool _requireRightMouseForOrbit = true;

        [Header("Focus")]
        [SerializeField] private float _focusBlendSpeed = 8f;

        private CameraMode _currentMode;
        private float _yaw = 45f;
        private float _pitch = 18f;
        private bool _focusOverrideActive;
        private Vector3 _focusDirection;

        public Transform CameraPivot => _cameraPivot;
        public CameraMode CurrentMode => _currentMode;
        public bool IsFirstPerson => _currentMode == CameraMode.FirstPerson;

        private void Awake()
        {
            if (_cameraPivot == null && Camera.main != null)
            {
                _cameraPivot = Camera.main.transform;
            }

            if (_target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _target = player.transform;
                }
            }

            _currentMode = _defaultMode;

            if (_cameraPivot != null)
            {
                Vector3 euler = _cameraPivot.rotation.eulerAngles;
                _yaw = euler.y;
                _pitch = NormalizePitch(euler.x);
            }

            SnapToCurrentMode();
            ApplyCharacterVisibility();
        }

        private void Update()
        {
            if (WasTogglePressed())
            {
                ToggleMode();
            }

            UpdateLookInput();
        }

        private void LateUpdate()
        {
            if (_target == null || _cameraPivot == null)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 offset = _currentMode == CameraMode.FirstPerson ? _firstPersonOffset : _thirdPersonOffset;
            Vector3 desiredPosition = _target.position + targetRotation * offset;

            float positionLerp = 1f - Mathf.Exp(-_positionSmooth * Time.deltaTime);
            float rotationLerp = 1f - Mathf.Exp(-_rotationSmooth * Time.deltaTime);

            _cameraPivot.position = Vector3.Lerp(_cameraPivot.position, desiredPosition, positionLerp);
            _cameraPivot.rotation = Quaternion.Slerp(_cameraPivot.rotation, targetRotation, rotationLerp);
        }

        public void ConfigurePivot(Transform pivot)
        {
            _cameraPivot = pivot;
        }

        public void ConfigureTarget(Transform target)
        {
            _target = target;
        }

        public void ToggleMode()
        {
            _currentMode = IsFirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson;
            SnapToCurrentMode();
            ApplyCharacterVisibility();
        }

        public void SetMode(CameraMode mode)
        {
            _currentMode = mode;
            SnapToCurrentMode();
            ApplyCharacterVisibility();
        }

        public void FocusOn(Vector3 worldPos)
        {
            if (_target == null)
            {
                return;
            }

            Vector3 lookDirection = worldPos - _target.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            _focusDirection = lookDirection.normalized;
            _focusOverrideActive = true;
        }

        public void FocusOnCell(Core.GridCell cell)
        {
            if (cell != null)
            {
                FocusOn(cell.transform.position);
            }
        }

        public void FocusOnEvent(Core.ActiveEvent active, Vector3 farmCenter)
        {
            FocusOn(farmCenter);
        }

        public void ReturnToOverview()
        {
            _focusOverrideActive = false;
        }

        private void UpdateLookInput()
        {
            if (_cameraPivot == null)
            {
                return;
            }

            Vector2 lookDelta = ReadLookInput();
            bool allowOrbit = !_requireRightMouseForOrbit || IsRightMousePressed();

            if (allowOrbit && lookDelta.sqrMagnitude > 0.0001f)
            {
                _yaw += lookDelta.x * _mouseSensitivity;
                _pitch -= lookDelta.y * _mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
                _focusOverrideActive = false;
            }
            else if (_focusOverrideActive)
            {
                float desiredYaw = Quaternion.LookRotation(_focusDirection, Vector3.up).eulerAngles.y;
                _yaw = Mathf.LerpAngle(_yaw, desiredYaw, 1f - Mathf.Exp(-_focusBlendSpeed * Time.deltaTime));
            }
        }

        private static float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            return pitch;
        }

        private static bool IsRightMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }

        private bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        private static Vector2 ReadLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return Vector2.zero;
            }

            return mouse.delta.ReadValue() * 0.02f;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
        }

        private void SnapToCurrentMode()
        {
            if (_target == null || _cameraPivot == null)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 offset = _currentMode == CameraMode.FirstPerson ? _firstPersonOffset : _thirdPersonOffset;
            _cameraPivot.SetPositionAndRotation(_target.position + targetRotation * offset, targetRotation);
        }

        private void ApplyCharacterVisibility()
        {
            if (_firstPersonHiddenRenderers == null)
            {
                return;
            }

            bool visible = !IsFirstPerson;
            foreach (Renderer renderer in _firstPersonHiddenRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}
