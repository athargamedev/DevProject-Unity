using System;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using NGLogLevel = Network_Game.Diagnostics.LogLevel;

namespace Network_Game.Behavior
{
    /// <summary>
    /// Events published by NetworkBootstrap for component communication.
    /// </summary>
    public class NetworkBootstrapEvents : MonoBehaviour, INetworkBootstrapEventsBridge
    {
        private const string Category = "BootstrapEvents";

        public static NetworkBootstrapEvents Instance { get; private set; }

        // Network events
        public event Action<NetworkManager> OnNetworkReady;
        public event Action<bool> OnClientModeDetermined; // true = client, false = host
        public event Action OnHostStarted;
        public event Action OnClientStarted;
        public event Action<string> OnNetworkError;

        // Player events
        public event Action<GameObject> OnLocalPlayerSpawned;
        public event Action<GameObject> OnLocalPlayerReady;

        // Auth events
        public event Action OnAuthGatePassed;

        // Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                NGLog.Warn(
                    Category,
                    NGLog.Format("Duplicate bootstrap events instance destroyed", ("object", gameObject.name)),
                    this
                );
                Destroy(gameObject);
                return;
            }

            Instance = this;
            NetworkBootstrapEventsBridgeRegistry.Register(this);
            NGLog.Lifecycle(Category, "awake", CreateTraceContext("scene_compose"), this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            NetworkBootstrapEventsBridgeRegistry.Unregister(this);

            NGLog.Lifecycle(Category, "destroy", CreateTraceContext("scene_compose"), this);
        }

        // Publish methods
        public void PublishNetworkReady(NetworkManager manager)
        {
            NGLog.Publish(
                Category,
                nameof(OnNetworkReady),
                CreateTraceContext("network_ready"),
                this,
                data:
                new[]
                {
                    ("listeners", (object)GetSubscriberCount(OnNetworkReady)),
                    ("manager", manager != null ? manager.name : "null"),
                }
            );
            OnNetworkReady?.Invoke(manager);
        }

        public void PublishClientModeDetermined(bool isClient)
        {
            NGLog.Publish(
                Category,
                nameof(OnClientModeDetermined),
                CreateTraceContext("network_mode"),
                this,
                data:
                new[]
                {
                    ("listeners", (object)GetSubscriberCount(OnClientModeDetermined)),
                    ("isClient", isClient),
                }
            );
            OnClientModeDetermined?.Invoke(isClient);
        }

        public void PublishHostStarted()
        {
            NGLog.Publish(
                Category,
                nameof(OnHostStarted),
                CreateTraceContext("network_ready"),
                this,
                data: new[] { ("listeners", (object)GetSubscriberCount(OnHostStarted)) }
            );
            OnHostStarted?.Invoke();
        }

        public void PublishClientStarted()
        {
            NGLog.Publish(
                Category,
                nameof(OnClientStarted),
                CreateTraceContext("network_ready"),
                this,
                data: new[] { ("listeners", (object)GetSubscriberCount(OnClientStarted)) }
            );
            OnClientStarted?.Invoke();
        }

        public void PublishNetworkError(string error)
        {
            NGLog.Publish(
                Category,
                nameof(OnNetworkError),
                CreateTraceContext("network_ready"),
                this,
                NGLogLevel.Warning,
                data:
                new[]
                {
                    ("listeners", (object)GetSubscriberCount(OnNetworkError)),
                    ("error", error ?? string.Empty),
                }
            );
            OnNetworkError?.Invoke(error);
        }

        public void PublishLocalPlayerSpawned(GameObject player)
        {
            NGLog.Publish(
                Category,
                nameof(OnLocalPlayerSpawned),
                CreateTraceContext("player_spawn"),
                this,
                data:
                new[]
                {
                    ("listeners", (object)GetSubscriberCount(OnLocalPlayerSpawned)),
                    ("player", player != null ? player.name : "null"),
                }
            );
            OnLocalPlayerSpawned?.Invoke(player);
        }

        public void PublishLocalPlayerReady(GameObject player)
        {
            NGLog.Publish(
                Category,
                nameof(OnLocalPlayerReady),
                CreateTraceContext("player_ready"),
                this,
                data:
                new[]
                {
                    ("listeners", (object)GetSubscriberCount(OnLocalPlayerReady)),
                    ("player", player != null ? player.name : "null"),
                }
            );
            OnLocalPlayerReady?.Invoke(player);
        }

        public void PublishAuthGatePassed()
        {
            NGLog.Publish(
                Category,
                nameof(OnAuthGatePassed),
                CreateTraceContext("auth_gate"),
                this,
                data: new[] { ("listeners", (object)GetSubscriberCount(OnAuthGatePassed)) }
            );
            OnAuthGatePassed?.Invoke();
        }

        private static int GetSubscriberCount(Delegate handlers)
        {
            return handlers?.GetInvocationList().Length ?? 0;
        }

        private static TraceContext CreateTraceContext(string phase)
        {
            return new TraceContext(phase: phase, script: nameof(NetworkBootstrapEvents));
        }
    }
}
