using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using Network_Game.ThirdPersonController.InputSystem;

namespace Network_Game.ThirdPersonController
{
    /// <summary>
    /// Consolidated multiplayer ownership setup for the player prefab.
    /// Runs once in <see cref="OnNetworkSpawn"/> — after NGO has reliably set
    /// <see cref="NetworkBehaviour.IsOwner"/> — to configure every ownership-driven
    /// component in one place and at the right time.
    ///
    /// <para>
    /// Why this matters: Unity MonoBehaviour lifecycle events (Awake / Start / OnEnable)
    /// fire <em>before</em> NGO completes network spawn and assigns ownership, so any
    /// initialization that depends on <c>IsOwner</c> done there may be incorrect.
    /// <see cref="OnNetworkSpawn"/> is the canonical NGO hook that fires only after
    /// ownership is known.
    /// </para>
    ///
    /// <para>
    /// Works alongside <see cref="ThirdPersonController"/>'s per-frame
    /// <c>SyncOwnershipDrivenComponents</c> guard — this script performs the
    /// authoritative first-pass setup; TPC re-validates every Update for late changes
    /// (e.g. authority transfers).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(100)] // after NGO default (-1000) and TPC default (0)
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerSetup : NetworkBehaviour
    {
        // ── cached component references ──────────────────────────────────────────

        private ThirdPersonController _tpc;
        private FlyModeController _fly;
        private StarterAssetsInputs _starterInputs;
        private PlayerInput _playerInput;
        private CharacterController _characterController;
        private NetworkAnimator _networkAnimator;

        // ── lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _tpc               = GetComponent<ThirdPersonController>();
            _fly               = GetComponent<FlyModeController>();
            _starterInputs     = GetComponent<StarterAssetsInputs>();
            _playerInput       = GetComponent<PlayerInput>();
            _characterController = GetComponent<CharacterController>();
            _networkAnimator   = GetComponent<NetworkAnimator>();
        }

        /// <summary>
        /// Called by NGO after the object is fully spawned and ownership is set.
        /// This is the single authoritative initialization point for all
        /// ownership-driven components.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            ConfigureNetworkAnimator();

            if (IsOwner)
            {
                SetupOwner();
            }
            else
            {
                SetupObserver();
            }
        }

        // ── owner setup (local player on this machine) ───────────────────────────

        private void SetupOwner()
        {
            // Let ThirdPersonController's own sync logic run with force=true so its
            // cached state is correct from the first frame.
            _tpc?.RefreshLocalControlState();

            // Activate Unity Input System for this player.
            if (_playerInput != null)
            {
                // Ensure SendMessages mode so StarterAssetsInputs.OnMove etc. receive callbacks.
                if (_playerInput.notificationBehavior != PlayerNotifications.SendMessages &&
                    _playerInput.notificationBehavior != PlayerNotifications.BroadcastMessages)
                {
                    _playerInput.notificationBehavior = PlayerNotifications.SendMessages;
                }

                _playerInput.enabled = true;
                _playerInput.ActivateInput();

                if (_playerInput.currentActionMap == null ||
                    _playerInput.currentActionMap.name != "Player")
                {
                    _playerInput.SwitchCurrentActionMap("Player");
                }

                _playerInput.currentActionMap?.Enable();
            }

            if (_starterInputs != null)
            {
                _starterInputs.enabled         = true;
                _starterInputs.inputBlocked    = false;
                _starterInputs.cursorLocked    = true;
                _starterInputs.cursorInputForLook = true;
                _starterInputs.SetCursorState(true);
            }

            if (_characterController != null)
                _characterController.enabled = true;

            if (_fly != null)
            {
                _fly.enabled = true;
                _fly.SetFlyMode(false);
            }

            Debug.Log($"[NetworkPlayerSetup] Owner setup complete — {gameObject.name}");
        }

        // ── observer setup (remote player seen on this machine) ──────────────────

        private void SetupObserver()
        {
            // Hard-disable all input so keyboard/mouse events can never leak into a
            // remote player's controller on the local machine.
            if (_playerInput != null)
            {
                _playerInput.DeactivateInput();
                _playerInput.enabled = false;
            }

            if (_starterInputs != null)
            {
                _starterInputs.move        = Vector2.zero;
                _starterInputs.look        = Vector2.zero;
                _starterInputs.jump        = false;
                _starterInputs.sprint      = false;
                _starterInputs.attack      = false;
                _starterInputs.emote       = false;
                _starterInputs.interact    = false;
                _starterInputs.enabled     = false;
                _starterInputs.inputBlocked = true;
            }

            if (_fly != null)
            {
                _fly.SetFlyMode(false);
                _fly.enabled = false;
            }

            // Force TPC to re-evaluate so its cached ownership state matches reality.
            _tpc?.RefreshLocalControlState();

            Debug.Log($"[NetworkPlayerSetup] Observer setup complete — {gameObject.name}");
        }

        // ── shared helpers ───────────────────────────────────────────────────────

        private void ConfigureNetworkAnimator()
        {
            if (_networkAnimator == null) return;

            // Owner drives animation state; NetworkAnimator replicates to others.
            _networkAnimator.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;

            if (_networkAnimator.Animator == null)
            {
                _networkAnimator.Animator = GetComponentInChildren<Animator>(true);
            }

            if (_networkAnimator.Animator != null && _networkAnimator.Animator.applyRootMotion)
            {
                _networkAnimator.Animator.applyRootMotion = false;
            }
        }
    }
}
