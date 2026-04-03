using System;
using UnityEngine;

namespace Network_Game.Diagnostics.Contracts
{
    /// <summary>
    /// Provides access to player identity information without direct Auth assembly reference.
    /// Implement in Auth assembly, use in Dialogue.
    /// </summary>
    public interface IPlayerIdentityProvider
    {
        bool HasCurrentPlayer { get; }
        PlayerIdentitySnapshot CurrentPlayer { get; }
    }

    /// <summary>
    /// Snapshot of player identity data - immutable value type.
    /// </summary>
    public readonly struct PlayerIdentitySnapshot
    {
        public readonly long PlayerId;
        public readonly string NameId;
        public readonly string ProfileVersion;
        public readonly string BaseModelId;
        public readonly bool IsValid;

        public PlayerIdentitySnapshot(long playerId, string nameId, string profileVersion, string baseModelId)
        {
            PlayerId = playerId;
            NameId = nameId ?? string.Empty;
            ProfileVersion = profileVersion ?? string.Empty;
            BaseModelId = baseModelId ?? string.Empty;
            IsValid = playerId > 0;
        }

        public static readonly PlayerIdentitySnapshot Invalid = new PlayerIdentitySnapshot(0, string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// Provides access to combat state without direct Combat assembly reference.
    /// Implement in Combat or Core, use in Dialogue.
    /// </summary>
    public interface ICombatStateProvider
    {
        int GetPlayerHealth(ulong clientId);
        bool IsPlayerInCombat(ulong clientId);
        bool IsPlayerAlive(ulong clientId);
    }

    /// <summary>
    /// Provides access to game state without direct Core assembly reference.
    /// Implement in Core assembly, use in Dialogue.
    /// </summary>
    public interface IGameStateProvider
    {
        string CurrentScene { get; }
        float GameTime { get; }
        int ConnectedClientCount { get; }
        bool IsServer { get; }
        bool IsClient { get; }
    }

    /// <summary>
    /// Static registry for provider instances. Higher-level assemblies (Behavior, Core)
    /// register implementations here for Dialogue to consume.
    /// </summary>
    public static class ProviderRegistry
    {
        private static IPlayerIdentityProvider s_PlayerIdentity;
        private static ICombatStateProvider s_CombatState;
        private static IGameStateProvider s_GameState;

        public static IPlayerIdentityProvider PlayerIdentity
        {
            get => s_PlayerIdentity ?? InvalidPlayerIdentityProvider.Instance;
            set => s_PlayerIdentity = value;
        }

        public static ICombatStateProvider CombatState
        {
            get => s_CombatState ?? InvalidCombatStateProvider.Instance;
            set => s_CombatState = value;
        }

        public static IGameStateProvider GameState
        {
            get => s_GameState ?? InvalidGameStateProvider.Instance;
            set => s_GameState = value;
        }

        public static void Clear()
        {
            s_PlayerIdentity = null;
            s_CombatState = null;
            s_GameState = null;
        }
    }

    /// <summary>
    /// Invalid/no-op implementations when providers not set.
    /// </summary>
    internal sealed class InvalidPlayerIdentityProvider : IPlayerIdentityProvider
    {
        public static readonly InvalidPlayerIdentityProvider Instance = new InvalidPlayerIdentityProvider();
        private InvalidPlayerIdentityProvider() { }
        public bool HasCurrentPlayer => false;
        public PlayerIdentitySnapshot CurrentPlayer => PlayerIdentitySnapshot.Invalid;
    }

    internal sealed class InvalidCombatStateProvider : ICombatStateProvider
    {
        public static readonly InvalidCombatStateProvider Instance = new InvalidCombatStateProvider();
        private InvalidCombatStateProvider() { }
        public int GetPlayerHealth(ulong clientId) => 0;
        public bool IsPlayerInCombat(ulong clientId) => false;
        public bool IsPlayerAlive(ulong clientId) => true;
    }

    internal sealed class InvalidGameStateProvider : IGameStateProvider
    {
        public static readonly InvalidGameStateProvider Instance = new InvalidGameStateProvider();
        private InvalidGameStateProvider() { }
        public string CurrentScene => string.Empty;
        public float GameTime => 0f;
        public int ConnectedClientCount => 0;
        public bool IsServer => false;
        public bool IsClient => true;
    }

    // ========== Auth Service Provider ==========

    /// <summary>
    /// Provides access to SupabaseAuthService without direct Auth assembly reference.
    /// Implement in Auth assembly, use in Core.
    /// </summary>
    public interface IAuthServiceProvider
    {
        bool IsAuthenticated { get; }
        string AccessToken { get; }
        string CurrentPlayerKey { get; }
        event Action<object> OnAuthStateChanged;
    }

    /// <summary>
    /// Registry for auth service. Set by Auth assembly, used by Core.
    /// </summary>
    public static class AuthServiceRegistry
    {
        private static IAuthServiceProvider s_AuthService;

        public static IAuthServiceProvider AuthService => s_AuthService ?? InvalidAuthServiceProvider.Instance;

        public static void Register(IAuthServiceProvider provider)
        {
            s_AuthService = provider;
        }

        public static void Unregister(IAuthServiceProvider provider)
        {
            if (ReferenceEquals(s_AuthService, provider))
            {
                s_AuthService = null;
            }
        }
    }

    internal sealed class InvalidAuthServiceProvider : IAuthServiceProvider
    {
        public static readonly InvalidAuthServiceProvider Instance = new InvalidAuthServiceProvider();
        private InvalidAuthServiceProvider() { }
        public bool IsAuthenticated => false;
        public string AccessToken => string.Empty;
        public string CurrentPlayerKey => string.Empty;
        
        // Empty event - never invoked in fallback
        public event Action<object> OnAuthStateChanged
        {
            add { }
            remove { }
        }
    }
}
