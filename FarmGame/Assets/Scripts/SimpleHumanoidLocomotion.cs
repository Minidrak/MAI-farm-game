using UnityEngine;

namespace FarmSimulator.Player
{
    /// <summary>
    /// Adds a lightweight additive locomotion pose on top of the imported idle animation.
    /// This keeps the existing anime character setup usable even when no walk clip is available.
    /// </summary>
    public class SimpleHumanoidLocomotion : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private float _blendSpeed = 8f;
        [SerializeField] private float _walkFrequency = 7.5f;
        [SerializeField] private float _armSwingAngle = 20f;
        [SerializeField] private float _legSwingAngle = 18f;
        [SerializeField] private float _kneeBendAngle = 10f;
        [SerializeField] private float _bodyTiltAngle = 4f;
        [SerializeField] private float _hipBobAmount = 0.03f;
        [SerializeField] private float _idleBreathAngle = 1.5f;
        [SerializeField] private float _idleBreathAmount = 0.008f;

        private Transform _hips;
        private Transform _spine;
        private Transform _chest;
        private Transform _head;
        private Transform _leftUpperArm;
        private Transform _rightUpperArm;
        private Transform _leftUpperLeg;
        private Transform _rightUpperLeg;
        private Transform _leftLowerLeg;
        private Transform _rightLowerLeg;

        private Quaternion _hipsBaseRotation;
        private Quaternion _spineBaseRotation;
        private Quaternion _chestBaseRotation;
        private Quaternion _headBaseRotation;
        private Quaternion _leftUpperArmBaseRotation;
        private Quaternion _rightUpperArmBaseRotation;
        private Quaternion _leftUpperLegBaseRotation;
        private Quaternion _rightUpperLegBaseRotation;
        private Quaternion _leftLowerLegBaseRotation;
        private Quaternion _rightLowerLegBaseRotation;
        private Vector3 _hipsBasePosition;

        private float _targetMoveAmount;
        private float _currentMoveAmount;
        private float _cycleTime;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator == null || !_animator.isHuman || _animator.avatar == null)
            {
                enabled = false;
                return;
            }

            CacheBones();
            CachePose();
        }

        public void SetMoveAmount(float moveAmount)
        {
            _targetMoveAmount = Mathf.Clamp01(moveAmount);
        }

        private void LateUpdate()
        {
            if (_animator == null)
            {
                return;
            }

            _currentMoveAmount = Mathf.Lerp(_currentMoveAmount, _targetMoveAmount, Time.deltaTime * _blendSpeed);
            _cycleTime += Time.deltaTime * Mathf.Lerp(1.5f, _walkFrequency, _currentMoveAmount);

            ApplyIdlePose();
            ApplyWalkPose();
        }

        private void CacheBones()
        {
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        private void CachePose()
        {
            if (_hips != null)
            {
                _hipsBaseRotation = _hips.localRotation;
                _hipsBasePosition = _hips.localPosition;
            }

            if (_spine != null) _spineBaseRotation = _spine.localRotation;
            if (_chest != null) _chestBaseRotation = _chest.localRotation;
            if (_head != null) _headBaseRotation = _head.localRotation;
            if (_leftUpperArm != null) _leftUpperArmBaseRotation = _leftUpperArm.localRotation;
            if (_rightUpperArm != null) _rightUpperArmBaseRotation = _rightUpperArm.localRotation;
            if (_leftUpperLeg != null) _leftUpperLegBaseRotation = _leftUpperLeg.localRotation;
            if (_rightUpperLeg != null) _rightUpperLegBaseRotation = _rightUpperLeg.localRotation;
            if (_leftLowerLeg != null) _leftLowerLegBaseRotation = _leftLowerLeg.localRotation;
            if (_rightLowerLeg != null) _rightLowerLegBaseRotation = _rightLowerLeg.localRotation;
        }

        private void ApplyIdlePose()
        {
            float breathWave = Mathf.Sin(_cycleTime * 1.25f);
            float idleWeight = 1f - _currentMoveAmount;

            if (_hips != null)
            {
                _hips.localPosition = _hipsBasePosition + new Vector3(0f, breathWave * _idleBreathAmount * idleWeight, 0f);
                _hips.localRotation = _hipsBaseRotation;
            }

            if (_spine != null)
            {
                _spine.localRotation = _spineBaseRotation * Quaternion.Euler(breathWave * _idleBreathAngle * idleWeight, 0f, 0f);
            }

            if (_chest != null)
            {
                _chest.localRotation = _chestBaseRotation * Quaternion.Euler(breathWave * (_idleBreathAngle * 1.2f) * idleWeight, 0f, 0f);
            }

            if (_head != null)
            {
                _head.localRotation = _headBaseRotation * Quaternion.Euler(-breathWave * (_idleBreathAngle * 0.6f) * idleWeight, 0f, 0f);
            }

            if (_leftUpperArm != null) _leftUpperArm.localRotation = _leftUpperArmBaseRotation;
            if (_rightUpperArm != null) _rightUpperArm.localRotation = _rightUpperArmBaseRotation;
            if (_leftUpperLeg != null) _leftUpperLeg.localRotation = _leftUpperLegBaseRotation;
            if (_rightUpperLeg != null) _rightUpperLeg.localRotation = _rightUpperLegBaseRotation;
            if (_leftLowerLeg != null) _leftLowerLeg.localRotation = _leftLowerLegBaseRotation;
            if (_rightLowerLeg != null) _rightLowerLeg.localRotation = _rightLowerLegBaseRotation;
        }

        private void ApplyWalkPose()
        {
            if (_currentMoveAmount <= 0.001f)
            {
                return;
            }

            float phase = Mathf.Sin(_cycleTime);
            float oppositePhase = Mathf.Sin(_cycleTime + Mathf.PI);
            float stepLiftLeft = Mathf.Max(0f, -phase);
            float stepLiftRight = Mathf.Max(0f, -oppositePhase);

            if (_hips != null)
            {
                _hips.localPosition += new Vector3(0f, Mathf.Abs(phase) * _hipBobAmount * _currentMoveAmount, 0f);
                _hips.localRotation *= Quaternion.Euler(0f, phase * _bodyTiltAngle * 0.4f * _currentMoveAmount, 0f);
            }

            if (_spine != null)
            {
                _spine.localRotation *= Quaternion.Euler(0f, -phase * _bodyTiltAngle * _currentMoveAmount, 0f);
            }

            if (_chest != null)
            {
                _chest.localRotation *= Quaternion.Euler(-Mathf.Abs(phase) * _bodyTiltAngle * _currentMoveAmount, 0f, 0f);
            }

            if (_leftUpperArm != null)
            {
                _leftUpperArm.localRotation *= Quaternion.Euler(oppositePhase * _armSwingAngle * _currentMoveAmount, 0f, 0f);
            }

            if (_rightUpperArm != null)
            {
                _rightUpperArm.localRotation *= Quaternion.Euler(phase * _armSwingAngle * _currentMoveAmount, 0f, 0f);
            }

            if (_leftUpperLeg != null)
            {
                _leftUpperLeg.localRotation *= Quaternion.Euler(phase * _legSwingAngle * _currentMoveAmount, 0f, 0f);
            }

            if (_rightUpperLeg != null)
            {
                _rightUpperLeg.localRotation *= Quaternion.Euler(oppositePhase * _legSwingAngle * _currentMoveAmount, 0f, 0f);
            }

            if (_leftLowerLeg != null)
            {
                _leftLowerLeg.localRotation *= Quaternion.Euler(stepLiftLeft * _kneeBendAngle * _currentMoveAmount, 0f, 0f);
            }

            if (_rightLowerLeg != null)
            {
                _rightLowerLeg.localRotation *= Quaternion.Euler(stepLiftRight * _kneeBendAngle * _currentMoveAmount, 0f, 0f);
            }
        }
    }
}
