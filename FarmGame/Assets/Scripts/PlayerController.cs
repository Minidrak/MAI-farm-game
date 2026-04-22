using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FarmSimulator.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private float _moveSpeed = 4.5f;
        [SerializeField] private float _rotationSpeed = 12f;
        [SerializeField] private Animator _animator;
        [SerializeField] private SimpleHumanoidLocomotion _locomotionVisuals;

        private CharacterController _characterController;
        private bool _hasSpeedParameter;

        public Vector3 MoveInput { get; private set; }
        public bool IsMoving => MoveInput.sqrMagnitude > 0.001f;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_locomotionVisuals == null)
            {
                _locomotionVisuals = GetComponent<SimpleHumanoidLocomotion>();
            }

            if (_locomotionVisuals == null)
            {
                _locomotionVisuals = gameObject.AddComponent<SimpleHumanoidLocomotion>();
            }

            _hasSpeedParameter = HasAnimatorParameter("Speed");
        }

        private void Update()
        {
            Vector2 rawInput = ReadMoveInput();
            float horizontal = rawInput.x;
            float vertical = rawInput.y;

            Vector3 input = new Vector3(horizontal, 0f, vertical);
            input = Vector3.ClampMagnitude(input, 1f);
            MoveInput = ConvertToCameraRelative(input);

            if (MoveInput.sqrMagnitude > 0.001f)
            {
                _characterController.Move(MoveInput * (_moveSpeed * Time.deltaTime));
                Quaternion targetRotation = Quaternion.LookRotation(MoveInput, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }

            if (_characterController.isGrounded)
            {
                _characterController.Move(Vector3.down * 0.05f);
            }

            if (_animator != null && _hasSpeedParameter)
            {
                _animator.SetFloat("Speed", MoveInput.magnitude);
            }

            _locomotionVisuals?.SetMoveAmount(MoveInput.magnitude);
        }

        public void Configure(Transform cameraPivot, float moveSpeed)
        {
            _cameraPivot = cameraPivot;
            _moveSpeed = moveSpeed;
        }

        private Vector3 ConvertToCameraRelative(Vector3 input)
        {
            if (_cameraPivot == null || input.sqrMagnitude <= 0.001f)
            {
                return input;
            }

            Vector3 forward = _cameraPivot.forward;
            Vector3 right = _cameraPivot.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            return forward * input.z + right * input.x;
        }

        private Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 value = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return value;
            }

            if (keyboard.aKey.isPressed) value.x -= 1f;
            if (keyboard.dKey.isPressed) value.x += 1f;
            if (keyboard.sKey.isPressed) value.y -= 1f;
            if (keyboard.wKey.isPressed) value.y += 1f;
            return Vector2.ClampMagnitude(value, 1f);
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }

        private bool HasAnimatorParameter(string parameterName)
        {
            if (_animator == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
