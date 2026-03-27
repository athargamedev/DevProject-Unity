using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Network_Game.ThirdPersonController.InputSystem;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace Network_Game.ThirdPersonController
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Tooltip("Use Rigidbody physics instead of CharacterController")]
        public bool UseRigidbody = false;

        [Tooltip("If true, attaches/uses NetworkRigidbody for multiplayer rigidbody sync")]
        public bool UseNetworkRigidbody = true;

        [Tooltip("How much to lerp rigidbody target velocity for smoother input-driven motion")]
        [Range(0.0f, 1.0f)]
        public float RigidbodyVelocityLerp = 0.9f;

        public AudioSource AudioFootsteps;
        public AudioSource LandingAudio;
        public AudioSource AudioFoley;
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animIDInputX;
        private int _animIDInputY;
        private int _animIDTurnDelta;
        private int _animIDHardLanding;
        private int _animIDAttack;
        private int _animIDEmote;
        private System.Collections.Generic.HashSet<int> _animatorParamHashes;

        // hard landing detection
        private float _peakFallVelocity;
        private float _hardLandingTimeoutDelta;
        [Tooltip("Vertical speed (m/s downward) that triggers a hard landing animation")]
        public float HardLandingThreshold = 8f;
        [Tooltip("How long HardLanding stays true before auto-reset (seconds)")]
        public float HardLandingDuration = 0.5f;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private Rigidbody _rigidbody;
        private NetworkRigidbody _networkRigidbody;
        private NetworkObject _networkObject;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;
        private FlyModeController _flyModeController;
        private bool _lastOwnershipInteractiveState;
        private bool _ownershipInteractiveStateInitialized;

        public string ActiveInputActionMap
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (_playerInput != null && _playerInput.currentActionMap != null)
                {
                    return _playerInput.currentActionMap.name;
                }
#endif
                return string.Empty;
            }
        }

        public bool IsOwner
        {
            get
            {
                if (_networkObject == null)
                {
                    _networkObject = GetComponent<NetworkObject>();
                }
                return _networkObject == null || _networkObject.IsOwner;
            }
        }

        public bool InputComponentEnabled => _input != null && _input.enabled;

        public bool PlayerInputComponentEnabled => _playerInput != null && _playerInput.enabled;

        public bool FlyModeComponentEnabled => _flyModeController != null && _flyModeController.enabled;

        public bool HasAssignedCameraFollow => CinemachineCameraTarget != null;

        public void RefreshLocalControlState()
        {
            SyncOwnershipDrivenComponents(force: true);
        }

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
#else
return false;
#endif
            }
        }

        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
            _networkObject = GetComponent<NetworkObject>();
        }

        private void OnEnable()
        {
            SyncOwnershipDrivenComponents();
        }

        private void Start()
        {
            if (CinemachineCameraTarget == null)
            {
                Transform cameraRoot = transform.Find("PlayerCameraRoot");
                CinemachineCameraTarget = cameraRoot != null ? cameraRoot.gameObject : gameObject;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main?.gameObject;
                if (_mainCamera == null)
                {
                    _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                }

                if (_mainCamera == null)
                {
                    Debug.LogWarning("ThirdPersonController: MainCamera not found. Camera rotation may not work.");
                }
            }

            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            _hasAnimator = _animator != null;
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _flyModeController = GetComponent<FlyModeController>();
            _rigidbody = GetComponent<Rigidbody>();

#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif
            ConfigureRigids();

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            SyncOwnershipDrivenComponents(force: true);
        }

        private void ConfigureRigids()
        {
            if (UseRigidbody)
            {
                if (_rigidbody == null)
                {
                    _rigidbody = gameObject.AddComponent<Rigidbody>();
                }

                // Apply physics settings regardless of whether Rigidbody was just created
                // or already existed on the prefab (prefab Rigidbody has default settings).
                _rigidbody.interpolation          = RigidbodyInterpolation.Interpolate;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rigidbody.constraints            = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                _rigidbody.mass                   = 1f;
                _rigidbody.linearDamping          = 0f;
                _rigidbody.angularDamping         = 0.05f;

                // Add CapsuleCollider if not present (needed for Rigidbody physics)
                var capsuleCollider = GetComponent<CapsuleCollider>();
                if (capsuleCollider == null)
                {
                    capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
                    // Match CharacterController dimensions
                    if (_controller != null)
                    {
                        capsuleCollider.center = _controller.center;
                        capsuleCollider.radius = _controller.radius;
                        capsuleCollider.height = _controller.height;
                    }
                    else
                    {
                        // Default capsule dimensions
                        capsuleCollider.center = new Vector3(0f, 1f, 0f);
                        capsuleCollider.radius = 0.28f;
                        capsuleCollider.height = 2f;
                    }
                }

                if (_controller != null)
                {
                    _controller.enabled = false;
                }

                _rigidbody.useGravity = true;

                if (_networkRigidbody == null)
                {
                    _rigidbody.isKinematic = false;
                }

                if (UseNetworkRigidbody)
                {
                    _networkRigidbody = GetComponent<NetworkRigidbody>();
                    if (_networkRigidbody == null)
                    {
                        var networkTransform = GetComponent<NetworkTransform>();
                        if (networkTransform != null && (_networkObject == null || !_networkObject.IsSpawned))
                        {
                            _networkRigidbody = gameObject.AddComponent<NetworkRigidbody>();
                        }
                    }

                    if (_networkRigidbody != null)
                    {
                        _networkRigidbody.UseRigidBodyForMotion = true;
                        _networkRigidbody.AutoUpdateKinematicState = true;
                    }
                }
                else
                {
                    if (_networkRigidbody != null)
                    {
                        _networkRigidbody.AutoUpdateKinematicState = false;
                    }
                }
            }
            else
            {
                if (_controller != null)
                {
                    _controller.enabled = true;
                }

                if (_rigidbody != null)
                {
                    _rigidbody.isKinematic = true;
                }
            }
        }

        private void Update()
        {
            SyncOwnershipDrivenComponents();
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            _hasAnimator = _animator != null;

            if (!CanProcessLocalControl())
            {
                return;
            }

            if (UseRigidbody)
            {
                // All physics (grounded, jump, move) live in FixedUpdate for Rigidbody mode.
                return;
            }

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void FixedUpdate()
        {
            if (!UseRigidbody || !IsOwner) return;

            if (_rigidbody == null)
            {
                UseRigidbody = false; // fallback to CharacterController
                return;
            }

            GroundedCheck();
            JumpAndGravity();
            MoveRigidbody();
        }

        private void LateUpdate()
        {
            if (!CanProcessLocalControl())
            {
                return;
            }

            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("IsGrounded"); // controller param is "IsGrounded"
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animIDInputX = Animator.StringToHash("InputX");
            _animIDInputY = Animator.StringToHash("InputY");
            _animIDTurnDelta = Animator.StringToHash("TurnDelta");
            _animIDHardLanding = Animator.StringToHash("HardLanding");
            _animIDAttack      = Animator.StringToHash("Attack");
            _animIDEmote       = Animator.StringToHash("Emote");

            // Build a set of hashes that actually exist in the controller so SetFloat/SetBool
            // calls for parameters not yet wired up are silently skipped rather than spamming errors.
            _animatorParamHashes = new System.Collections.Generic.HashSet<int>();
            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                    _animatorParamHashes.Add(p.nameHash);
            }
        }

        /// <summary>Returns true if this hash exists as a parameter in the current Animator controller.</summary>
        private bool HasAnimParam(int hash) => _animatorParamHashes != null && _animatorParamHashes.Contains(hash);

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // Debug ground detection in Rigidbody mode
            if (UseRigidbody && !Grounded && _rigidbody != null && _rigidbody.linearVelocity.y < -1f)
            {
                Debug.DrawRay(spherePosition, Vector3.down * GroundedRadius, Color.red, 0.1f);
                // Also check what we're actually hitting
                Collider[] hits = Physics.OverlapSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
                if (hits.Length == 0)
                {
                    Debug.LogWarning($"[GroundCheck] No colliders found in ground check sphere! Position: {spherePosition}, Radius: {GroundedRadius}, LayerMask: {GroundLayers.value}");
                }
            }
            else if (Grounded)
            {
                Debug.DrawRay(spherePosition, Vector3.down * GroundedRadius, Color.green, 0.1f);
            }

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            if (!CanProcessLocalControl() || _input == null || _input.look.sqrMagnitude < _threshold || LockCameraPosition)
            {
                return;
            }

            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
            _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            if (CinemachineCameraTarget != null)
            {
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                    _cinemachineTargetYaw, 0.0f);
            }
        }

        private void Move()
        {
            if (!CanProcessLocalControl())
            {
                return;
            }

            if (UseRigidbody)
            {
                MoveRigidbody();
                return;
            }

            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            if (_controller != null)
            {
                _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                                 new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
            }

            if (_hasAnimator)
            {
                float normalizedSpeed = SprintSpeed > 0f ? _animationBlend / SprintSpeed : 0f;
                _animator.SetFloat(_animIDSpeed, normalizedSpeed);
                if (HasAnimParam(_animIDMotionSpeed)) _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
                if (HasAnimParam(_animIDInputX))      _animator.SetFloat(_animIDInputX, _input.move.x * inputMagnitude);
                if (HasAnimParam(_animIDInputY))      _animator.SetFloat(_animIDInputY, _input.move.y * inputMagnitude);
                if (HasAnimParam(_animIDTurnDelta))   _animator.SetFloat(_animIDTurnDelta, Mathf.Clamp(_rotationVelocity / 360f, -1f, 1f));
            }

            if (_hasAnimator && _input.attack)
            {
                _animator.SetTrigger(_animIDAttack);
                _input.attack = false;
            }
            if (_hasAnimator && _input.emote && HasAnimParam(_animIDEmote))
            {
                _animator.SetTrigger(_animIDEmote);
                _input.emote = false;
            }
        }

        private void MoveRigidbody()
        {
            if (!CanProcessLocalControl() || _rigidbody == null || _input == null) return;

            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            Vector3 horizontalVelocity = new Vector3(_rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z);
            float currentHorizontalSpeed = horizontalVelocity.magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.fixedDeltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.fixedDeltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);
                // MoveRotation integrates properly with the physics engine and interpolation.
                _rigidbody.MoveRotation(Quaternion.Euler(0.0f, rotation, 0.0f));
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            Vector3 desiredVel = targetDirection.normalized * (_speed);
            Vector3 newVelocity = Vector3.Lerp(horizontalVelocity, desiredVel, RigidbodyVelocityLerp);
            newVelocity.y = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = newVelocity;

            if (_hasAnimator)
            {
                float normalizedSpeed = SprintSpeed > 0f ? _animationBlend / SprintSpeed : 0f;
                _animator.SetFloat(_animIDSpeed, normalizedSpeed);
                if (HasAnimParam(_animIDMotionSpeed)) _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
                if (HasAnimParam(_animIDInputX))      _animator.SetFloat(_animIDInputX, _input.move.x * inputMagnitude);
                if (HasAnimParam(_animIDInputY))      _animator.SetFloat(_animIDInputY, _input.move.y * inputMagnitude);
                if (HasAnimParam(_animIDTurnDelta))   _animator.SetFloat(_animIDTurnDelta, Mathf.Clamp(_rotationVelocity / 360f, -1f, 1f));
            }

            if (_hasAnimator && _input.attack)
            {
                _animator.SetTrigger(_animIDAttack);
                _input.attack = false;
            }
            if (_hasAnimator && _input.emote && HasAnimParam(_animIDEmote))
            {
                _animator.SetTrigger(_animIDEmote);
                _input.emote = false;
            }
        }

        private void JumpAndGravity()
        {
            if (!CanProcessLocalControl())
            {
                return;
            }

            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_hasAnimator)
                {
                    // Jump is a Trigger in modelAndre_Animator — no bool reset needed.
                    if (HasAnimParam(_animIDFreeFall))   _animator.SetBool(_animIDFreeFall, false);
                    if (HasAnimParam(_animIDHardLanding))
                    {
                        if (-_peakFallVelocity > HardLandingThreshold)
                        {
                            _animator.SetBool(_animIDHardLanding, true);
                            _hardLandingTimeoutDelta = HardLandingDuration;
                        }
                        else if (_hardLandingTimeoutDelta > 0f)
                        {
                            _hardLandingTimeoutDelta -= Time.deltaTime;
                            if (_hardLandingTimeoutDelta <= 0f)
                                _animator.SetBool(_animIDHardLanding, false);
                        }
                    }
                    _peakFallVelocity = 0f;
                }

                if (UseRigidbody)
                {
                    if (_rigidbody != null && _input.jump && _jumpTimeoutDelta <= 0.0f)
                    {
                        Vector3 current = _rigidbody.linearVelocity;
                        // Use Physics.gravity.y: the Rigidbody falls at -9.81, not the custom Gravity field.
                        current.y = Mathf.Sqrt(JumpHeight * -2f * Physics.gravity.y);
                        _rigidbody.linearVelocity = current;

                        if (_hasAnimator)
                        {
                            _animator.SetTrigger(_animIDJump); // Jump is a Trigger in modelAndre_Animator
                        }
                    }

                    if (_jumpTimeoutDelta >= 0.0f)
                    {
                        _jumpTimeoutDelta -= Time.deltaTime;
                    }

                    return;
                }

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    if (_hasAnimator)
                    {
                        _animator.SetTrigger(_animIDJump);
                    }
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    _peakFallVelocity = Mathf.Min(_peakFallVelocity, UseRigidbody ? _rigidbody?.linearVelocity.y ?? 0.0f : _verticalVelocity);

                    if (_hasAnimator && HasAnimParam(_animIDFreeFall))
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                if (UseRigidbody)
                {
                    _input.jump = false;

                    // Safety check: prevent infinite falling
                    if (_rigidbody != null && _rigidbody.linearVelocity.y < -_terminalVelocity)
                    {
                        Vector3 vel = _rigidbody.linearVelocity;
                        vel.y = -_terminalVelocity;
                        _rigidbody.linearVelocity = vel;
                    }

                    return;
                }

                _input.jump = false;
            }

            if (!UseRigidbody)
            {
                if (_verticalVelocity < _terminalVelocity)
                {
                    _verticalVelocity += Gravity * Time.deltaTime;
                }
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {

                if (AudioFootsteps != null)
                    AudioFootsteps.Play();
                if (AudioFoley != null)
                    AudioFoley.Play();
            }
        }

        [ContextMenu("Setup Mesh Colliders for Ground")]
        private void SetupMeshCollidersForGround()
        {
            // Find all mesh colliders in the scene that might be ground
            MeshCollider[] meshColliders = FindObjectsOfType<MeshCollider>(true);
            int fixedCount = 0;

            foreach (var mc in meshColliders)
            {
                // Check if this collider is on a ground layer
                if ((GroundLayers.value & (1 << mc.gameObject.layer)) != 0)
                {
                    if (!mc.convex)
                    {
                        mc.convex = true;
                        fixedCount++;
                        Debug.Log($"Fixed MeshCollider on {mc.gameObject.name} - set Convex=true");
                    }
                    if (mc.isTrigger)
                    {
                        mc.isTrigger = false;
                        Debug.Log($"Fixed MeshCollider on {mc.gameObject.name} - set IsTrigger=false");
                    }
                }
            }

            Debug.Log($"Setup complete: Fixed {fixedCount} mesh colliders for ground collision");
        }

        private bool CanProcessLocalControl()
        {
            return _networkObject == null || !_networkObject.IsSpawned || IsOwner;
        }

        private void SyncOwnershipDrivenComponents(bool force = false)
        {
            bool interactive = CanProcessLocalControl();
            if (!force && _ownershipInteractiveStateInitialized && interactive == _lastOwnershipInteractiveState)
            {
                return;
            }

            _ownershipInteractiveStateInitialized = true;
            _lastOwnershipInteractiveState = interactive;

#if ENABLE_INPUT_SYSTEM
            if (_playerInput == null)
            {
                _playerInput = GetComponent<PlayerInput>();
            }

            if (_playerInput != null)
            {
                _playerInput.enabled = interactive;
                if (interactive)
                {
                    _playerInput.ActivateInput();
                    if (_playerInput.currentActionMap == null || _playerInput.currentActionMap.name != "Player")
                    {
                        _playerInput.SwitchCurrentActionMap("Player");
                    }
                    _playerInput.currentActionMap?.Enable();
                }
                else
                {
                    _playerInput.DeactivateInput();
                }
            }
#endif

            if (_input == null)
            {
                _input = GetComponent<StarterAssetsInputs>();
            }

            if (_input != null)
            {
                if (!interactive)
                {
                    _input.move = Vector2.zero;
                    _input.look = Vector2.zero;
                    _input.jump = false;
                    _input.sprint = false;
                    _input.attack = false;
                    _input.emote = false;
                    _input.interact = false;
                }

                _input.enabled = interactive;
                _input.inputBlocked = !interactive;
                _input.cursorLocked = interactive;
                _input.cursorInputForLook = interactive;
                if (interactive)
                {
                    _input.SetCursorState(true);
                }
            }

            if (_flyModeController == null)
            {
                _flyModeController = GetComponent<FlyModeController>();
            }

            if (_flyModeController != null)
            {
                _flyModeController.enabled = interactive;
                if (!interactive)
                {
                    _flyModeController.SetFlyMode(false);
                }
            }
        }
    }
}
