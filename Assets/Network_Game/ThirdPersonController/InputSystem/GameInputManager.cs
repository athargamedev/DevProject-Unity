using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Network_Game.InputSystem
{
    /// <summary>
    /// Central singleton for global input state management.
    /// Provides input blocking for UI/dialogue/pause scenarios.
    /// </summary>
    public class GameInputManager : MonoBehaviour
    {
        public static GameInputManager Instance { get; private set; }

        [Header("Input Action Asset")]
        [SerializeField] private InputActionAsset _inputActionAsset;
        
        [Header("UI Input Module")]
        [SerializeField] private InputSystemUIInputModule _uiInputModule;

        [Header("Settings")]
        [SerializeField] private bool _defaultCursorLocked = true;

        // Input state
        private bool _isBlocked;
        private InputActionAsset _cachedAsset;

        // Events
        public event System.Action<bool> OnInputBlockingChanged;

        #region Properties

        public bool IsBlocked
        {
            get => _isBlocked;
            set
            {
                if (_isBlocked != value)
                {
                    _isBlocked = value;
                    OnInputBlockingChanged?.Invoke(value);
                    UpdateInputState();
                }
            }
        }

        public InputActionAsset InputAsset
        {
            get
            {
                if (_inputActionAsset == null)
                {
                    // Try to find in resources
                    _inputActionAsset = Resources.Load<InputActionAsset>("GameInputActions");
                }
                return _inputActionAsset;
            }
        }

        public InputSystemUIInputModule UIInputModule => _uiInputModule;

        public bool IsCursorLocked
        {
            get => Cursor.lockState == CursorLockMode.Locked;
            set => Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _cachedAsset = InputAsset;
        }

        private void OnEnable()
        {
            if (_cachedAsset != null)
            {
                _cachedAsset.Enable();
            }
        }

        private void OnDisable()
        {
            if (_cachedAsset != null)
            {
                _cachedAsset.Disable();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Block gameplay input (for dialogue, pause menu, etc.)
        /// </summary>
        public void BlockInput() => IsBlocked = true;

        /// <summary>
        /// Unblock gameplay input
        /// </summary>
        public void UnblockInput() => IsBlocked = false;

        /// <summary>
        /// Toggle input blocking
        /// </summary>
        public void ToggleInput() => IsBlocked = !IsBlocked;

        /// <summary>
        /// Get the currently active control scheme name
        /// </summary>
        public string GetCurrentControlScheme()
        {
            if (_cachedAsset == null) return "None";
            // Get current scheme from the first enabled action's binding
            foreach (var map in _cachedAsset.actionMaps)
            {
                if (map.enabled && map.actions.Count > 0)
                {
                    foreach (var binding in map.bindings)
                    {
                        if (binding.groups.Length > 0)
                            return binding.groups;
                    }
                }
            }
            return "KeyboardMouse"; // Default fallback
        }

        /// <summary>
        /// Check if a specific device is currently in use
        /// </summary>
        public bool IsDeviceInUse<T>() where T : InputDevice
        {
            var devices = _cachedAsset?.devices;
            if (devices == null) return false;
            foreach (var device in devices)
            {
                if (device is T) return true;
            }
            return false;
        }

        /// <summary>
        /// Switch to a specific control scheme
        /// </summary>
        public void SwitchControlScheme(string schemeName)
        {
            if (_cachedAsset == null) return;
            
            // Find the control scheme and switch to it
            for (int i = 0; i < _cachedAsset.controlSchemes.Count; i++)
            {
                if (_cachedAsset.controlSchemes[i].name == schemeName)
                {
                    // Use PlayerInput to switch schemes if available
                    var playerInput = UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();
                    playerInput?.SwitchCurrentControlScheme(schemeName);
                    return;
                }
            }
        }

        /// <summary>
        /// Enable a specific action map
        /// </summary>
        public void EnableActionMap(string mapName)
        {
            _cachedAsset?.FindActionMap(mapName)?.Enable();
        }

        /// <summary>
        /// Disable a specific action map
        /// </summary>
        public void DisableActionMap(string mapName)
        {
            _cachedAsset?.FindActionMap(mapName)?.Disable();
        }

        #endregion

        #region Private Methods

        private void UpdateInputState()
        {
            if (_cachedAsset == null) return;

            // Enable/disable gameplay actions based on blocking state
            var gameplayMap = _cachedAsset.FindActionMap("Gameplay");
            var uiMap = _cachedAsset.FindActionMap("UI");

            if (_isBlocked)
            {
                // Disable gameplay, ensure UI is enabled
                gameplayMap?.Disable();
                uiMap?.Enable();
                IsCursorLocked = false;
            }
            else
            {
                // Enable gameplay, disable UI
                gameplayMap?.Enable();
                uiMap?.Disable();
                IsCursorLocked = _defaultCursorLocked;
            }
        }

        #endregion

        #region Debug/Diagnostics

        public string GetDebugInfo()
        {
            if (_cachedAsset == null) return "No input asset configured";

            var enabledMaps = new System.Collections.Generic.List<string>();
            foreach (var map in _cachedAsset.actionMaps)
            {
                if (map.enabled) enabledMaps.Add(map.name);
            }

            var schemeNames = new System.Collections.Generic.List<string>();
            foreach (var scheme in _cachedAsset.controlSchemes)
            {
                schemeNames.Add(scheme.name);
            }
            return $"Blocking: {_isBlocked} | Schemes: {string.Join(", ", schemeNames)} | Enabled Maps: {string.Join(", ", enabledMaps)}";
        }

        #endregion
    }
}