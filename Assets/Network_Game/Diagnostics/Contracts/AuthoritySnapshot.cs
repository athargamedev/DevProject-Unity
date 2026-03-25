using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct AuthoritySnapshot
    {
        public string RunId;
        public string BootId;
        public string SceneName;
        public int Frame;
        public float RealtimeSinceStartup;

        public bool NetworkManagerPresent;
        public bool IsListening;
        public bool IsServer;
        public bool IsHost;
        public bool IsClient;
        public bool IsConnectedClient;
        public ulong LocalClientId;

        public bool LocalPlayerResolved;
        public ulong LocalPlayerNetworkObjectId;
        public ulong LocalPlayerOwnerClientId;
        public string LocalPlayerObjectName;
        public bool LocalPlayerIsSpawned;
        public bool LocalPlayerIsOwner;

        public bool LocalControllerPresent;
        public bool LocalControllerEnabled;
        public bool LocalInputComponentPresent;
        public bool LocalInputEnabled;
        public string LocalActionMap;
        public bool CameraFollowAssigned;

        public bool AuthServicePresent;
        public bool HasAuthenticatedPlayer;
        public string AuthNameId;
        public ulong AuthAttachedNetworkObjectId;
        public bool PromptContextInitialized;
        public bool PromptContextAppliedToDialogue;

        public string CurrentPhase;
        public string Summary;

        public bool HasAuthorityBlocker =>
            !NetworkManagerPresent
            || !IsListening
            || !LocalPlayerResolved
            || (IsClient && !IsConnectedClient)
            || (HasAuthenticatedPlayer && !PromptContextAppliedToDialogue);

        public string ResolvePrimaryAuthorityProblem()
        {
            if (!NetworkManagerPresent)
            {
                return "network_manager_missing";
            }

            if (!IsListening)
            {
                return "network_not_listening";
            }

            if (IsClient && !IsConnectedClient)
            {
                return "client_not_connected";
            }

            if (!LocalPlayerResolved)
            {
                return "local_player_unresolved";
            }

            if (HasAuthenticatedPlayer && !PromptContextAppliedToDialogue)
            {
                return "prompt_context_not_applied";
            }

            return string.Empty;
        }

        public AuthorityRole ResolveLocalRole()
        {
            if (IsHost)
            {
                return AuthorityRole.Host;
            }

            if (IsServer)
            {
                return AuthorityRole.Server;
            }

            if (IsClient)
            {
                return AuthorityRole.Client;
            }

            return AuthorityRole.Offline;
        }
    }
}
