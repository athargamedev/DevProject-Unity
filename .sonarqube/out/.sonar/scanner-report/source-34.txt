using System;
using UnityEngine;

namespace Network_Game.Diagnostics.Contracts
{
    /// <summary>
    /// Events published by NetworkBootstrap for component communication.
    /// This is the contract - implementations live in Behavior assembly.
    /// </summary>
    public class NetworkBootstrapEvents : MonoBehaviour
    {
        public static NetworkBootstrapEvents Instance { get; private set; }

        // Network events - use object to avoid Netcode dependency in contracts
        public event Action<object> OnNetworkReady;
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
                Debug.LogWarning($"[BootstrapEvents] Duplicate instance destroyed: {gameObject.name}");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Publish methods
        public void PublishNetworkReady(object networkManager) => OnNetworkReady?.Invoke(networkManager);
        public void PublishClientModeDetermined(bool isClient) => OnClientModeDetermined?.Invoke(isClient);
        public void PublishHostStarted() => OnHostStarted?.Invoke();
        public void PublishClientStarted() => OnClientStarted?.Invoke();
        public void PublishNetworkError(string error) => OnNetworkError?.Invoke(error);
        public void PublishLocalPlayerSpawned(GameObject player) => OnLocalPlayerSpawned?.Invoke(player);
        public void PublishLocalPlayerReady(GameObject player) => OnLocalPlayerReady?.Invoke(player);
        public void PublishAuthGatePassed() => OnAuthGatePassed?.Invoke();
    }
}
