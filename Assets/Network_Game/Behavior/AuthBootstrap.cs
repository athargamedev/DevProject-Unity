using System;
using System.Collections;
using Network_Game.Auth;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using NGLogLevel = Network_Game.Diagnostics.LogLevel;

namespace Network_Game.Behavior
{
    /// <summary>
    /// Handles authentication gate - ensures player is authenticated before network proceeds.
    /// </summary>
    [DefaultExecutionOrder(-220)]
    public class AuthBootstrap : MonoBehaviour
    {
        private const string Category = "AuthBootstrap";

        [Header("Configuration")]
        [SerializeField][Min(0.5f)] public float m_TimeoutSeconds = 15f;
        [SerializeField] public bool m_BlockNetworkStartUntilAuthenticated = true;
        [SerializeField] public bool m_RequireExplicitLoginEachSession = true;

        private LocalPlayerAuthService m_AuthService;
        private bool m_IsClientMode;
        private bool m_AuthSatisfied;
        private Coroutine m_AuthGateRoutine;

        public bool IsAuthenticated => m_AuthSatisfied;

        private void OnEnable()
        {
            NGLog.Lifecycle(Category, "enable", CreateTraceContext("auth_gate"), this);
            var events = NetworkBootstrapEvents.Instance;
            if (events != null)
            {
                events.OnClientModeDetermined += OnClientModeDetermined;
                events.OnNetworkReady += OnNetworkReady;
                NGLog.Subscribe(
                    Category,
                    "bootstrap_events",
                    CreateTraceContext("auth_gate"),
                    this
                );
            }
        }

        private void OnDisable()
        {
            NGLog.Lifecycle(Category, "disable", CreateTraceContext("auth_gate"), this);
            if (m_AuthGateRoutine != null)
            {
                StopCoroutine(m_AuthGateRoutine);
                m_AuthGateRoutine = null;
            }
            var events = NetworkBootstrapEvents.Instance;
            if (events != null)
            {
                events.OnClientModeDetermined -= OnClientModeDetermined;
                events.OnNetworkReady -= OnNetworkReady;
            }
        }

        private void OnClientModeDetermined(bool isClient)
        {
            m_IsClientMode = isClient;
            NGLog.Transition(
                Category,
                "mode_unknown",
                isClient ? "client" : "host",
                CreateTraceContext("auth_gate"),
                this
            );
        }

        private void OnNetworkReady(NetworkManager manager)
        {
            NGLog.Trigger(
                Category,
                "network_ready_received",
                CreateTraceContext("auth_gate"),
                this,
                data: new[] { ("manager", (object)(manager != null ? manager.name : "null")) }
            );
            if (m_AuthGateRoutine != null)
            {
                StopCoroutine(m_AuthGateRoutine);
            }

            m_AuthGateRoutine = StartCoroutine(BeginAuthGateAfterNetworkReady());
        }

        private IEnumerator BeginAuthGateAfterNetworkReady()
        {
            // Let the network-ready publication fully propagate before the post-network auth gate runs.
            yield return null;
            yield return EnsureAuthGate();
            m_AuthGateRoutine = null;
        }

        private IEnumerator EnsureAuthGate()
        {
            NGLog.Lifecycle(Category, "auth_gate_begin", CreateTraceContext("auth_gate"), this);
            m_AuthSatisfied = false;
            m_AuthService = LocalPlayerAuthService.EnsureInstance();

            if (m_AuthService == null)
            {
                m_AuthSatisfied = ShouldAllowUnauthenticatedStart();
                if (m_AuthSatisfied)
                {
                    NGLog.Ready(
                        Category,
                        "auth_gate_passed",
                        true,
                        CreateTraceContext("auth_gate"),
                        this,
                        data: new[] { ("mode", (object)"no_auth_service") }
                    );
                    NetworkBootstrapEvents.Instance.PublishAuthGatePassed();
                }
                yield break;
            }

            EnsureAuthLoginUiAvailable();

            // Already have a player
            if (m_AuthService.HasCurrentPlayer)
            {
                m_AuthService.EnsurePromptContextInitialized();
                m_AuthSatisfied = true;
                NGLog.Ready(
                    Category,
                    "auth_gate_passed",
                    true,
                    CreateTraceContext("auth_gate"),
                    this,
                    data: new[] { ("mode", (object)"existing_player") }
                );
                NetworkBootstrapEvents.Instance.PublishAuthGatePassed();
                yield break;
            }

            // Auto-login if not required explicit
            if (!m_RequireExplicitLoginEachSession)
            {
                m_AuthService.EnsureLoggedIn();
            }

            // Wait for auth
            float timeout = Mathf.Max(0.5f, m_TimeoutSeconds);
            while (!m_AuthService.HasCurrentPlayer && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            // Handle timeout
            if (!m_AuthService.HasCurrentPlayer && m_BlockNetworkStartUntilAuthenticated)
            {
                if (ShouldAllowUnauthenticatedStart())
                {
                    NGLog.Ready(
                        Category,
                        "auth_gate_wait_timeout",
                        false,
                        CreateTraceContext("auth_gate"),
                        this,
                        NGLogLevel.Warning,
                        data: new[] { ("continued", (object)true) }
                    );
                }
                else
                {
                    NGLog.Trigger(
                        Category,
                        "waiting_for_login",
                        CreateTraceContext("auth_gate"),
                        this
                    );
                    while (!m_AuthService.HasCurrentPlayer)
                        yield return null;
                }
            }

            if (m_AuthService.HasCurrentPlayer)
            {
                m_AuthService.EnsurePromptContextInitialized();
            }

            m_AuthSatisfied = true;
            NGLog.Ready(Category, "auth_gate_passed", true, CreateTraceContext("auth_gate"), this);
            NetworkBootstrapEvents.Instance.PublishAuthGatePassed();
        }

        private bool ShouldAllowUnauthenticatedStart()
        {
#if UNITY_EDITOR
            // Editor multiplayer sessions
            if (IsEditorMultiplayerSession())
                return true;
#endif
            // Client mode always allows
            return m_IsClientMode;
        }

#if UNITY_EDITOR
        private static bool IsEditorMultiplayerSession()
        {
            try
            {
                var tags = Unity.Multiplayer.PlayMode.CurrentPlayer.Tags;
                return tags != null && tags.Count > 0;
            }
            catch {}
            return false;
        }

#endif

        private void EnsureAuthLoginUiAvailable()
        {
            if (m_AuthService == null) return;

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (
                    behaviour == null
                    || !behaviour.gameObject.scene.IsValid()
                    || behaviour is not Network_Game.UI.Login.PlayerLoginController loginUi
                )
                {
                    continue;
                }

                if (!behaviour.gameObject.activeSelf)
                {
                    behaviour.gameObject.SetActive(true);
                }

                if (!behaviour.enabled)
                {
                    behaviour.enabled = true;
                }

                if (!loginUi.IsVisible)
                {
                    loginUi.Show();
                }

                return;
            }
        }

        /// <summary>
        /// Called by other components to attach auth to a player.
        /// </summary>
        public void AttachAuthToPlayer(GameObject player)
        {
            if (m_AuthService == null) return;
            m_AuthService.AttachLocalPlayer(player);
            NGLog.Trigger(
                Category,
                "attach_auth_to_player",
                CreateTraceContext("player_ready"),
                this,
                data: new[] { ("player", (object)(player != null ? player.name : "null")) }
            );

            if (m_IsClientMode)
            {
                if (!m_RequireExplicitLoginEachSession)
                {
                    string clientName = GenerateClientPlayerName();
                    m_AuthService.Login(clientName);
                    NGLog.Info("AuthBootstrap", $"Client auto-auth: {clientName}");
                }
                else
                {
                    EnsureAuthLoginUiAvailable();
                }
            }
        }

        private string GenerateClientPlayerName()
        {
            string baseName = "player";
#if UNITY_EDITOR
            try
            {
                var tags = Unity.Multiplayer.PlayMode.CurrentPlayer.Tags;
                if (tags != null && tags.Count > 0 && !string.IsNullOrEmpty(tags[0]))
                    baseName = tags[0].Replace(" ", "_").ToLowerInvariant();
            }
            catch {}
#endif
            return $"{baseName}_{UnityEngine.Random.Range(100, 999)}";
        }

        private static TraceContext CreateTraceContext(
            string phase,
            [System.Runtime.CompilerServices.CallerMemberName] string caller = null
        )
        {
            return new TraceContext(
                phase: phase,
                script: nameof(AuthBootstrap),
                callback: caller
            );
        }
    }
}
