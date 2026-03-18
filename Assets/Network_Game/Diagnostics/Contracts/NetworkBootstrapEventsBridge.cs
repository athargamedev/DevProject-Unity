using System;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    public interface INetworkBootstrapEventsBridge
    {
        event Action<NetworkManager> OnNetworkReady;
        event Action<bool> OnClientModeDetermined;
        event Action<GameObject> OnLocalPlayerSpawned;
        event Action<GameObject> OnLocalPlayerReady;
        event Action OnAuthGatePassed;
    }

    public static class NetworkBootstrapEventsBridgeRegistry
    {
        public static INetworkBootstrapEventsBridge Current { get; private set; }

        public static void Register(INetworkBootstrapEventsBridge bridge)
        {
            Current = bridge;
        }

        public static void Unregister(INetworkBootstrapEventsBridge bridge)
        {
            if (ReferenceEquals(Current, bridge))
            {
                Current = null;
            }
        }
    }
}
