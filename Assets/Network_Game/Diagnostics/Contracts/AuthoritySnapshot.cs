using System;
using UnityEngine;

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
            || (IsClient || IsHost) && !LocalPlayerResolved
            || LocalPlayerResolved && LocalPlayerIsSpawned && !LocalPlayerIsOwner
            || LocalPlayerResolved && LocalPlayerIsOwner && LocalInputComponentPresent && !LocalInputEnabled
            || AuthServicePresent && !HasAuthenticatedPlayer;

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
                return LocalPlayerResolved && LocalPlayerIsOwner
                    ? AuthorityRole.ClientOwner
                    : AuthorityRole.ClientObserver;
            }

            return AuthorityRole.Unknown;
        }

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

            if (AuthServicePresent && !HasAuthenticatedPlayer)
            {
                return "auth_identity_missing";
            }

            if ((IsClient || IsHost) && !LocalPlayerResolved)
            {
                return "local_player_missing";
            }

            if (LocalPlayerResolved && LocalPlayerIsSpawned && !LocalPlayerIsOwner)
            {
                return "local_player_not_owner";
            }

            if (LocalPlayerResolved && LocalPlayerIsOwner && LocalInputComponentPresent && !LocalInputEnabled)
            {
                return "local_input_disabled";
            }

            if (LocalPlayerResolved && PromptContextInitialized && !PromptContextAppliedToDialogue)
            {
                return "prompt_context_not_applied";
            }

            return string.Empty;
        }

        public void RefreshSummary()
        {
            string role = ResolveLocalRole().ToString();
            string playerName = string.IsNullOrWhiteSpace(LocalPlayerObjectName)
                ? "none"
                : LocalPlayerObjectName;
            string authName = string.IsNullOrWhiteSpace(AuthNameId) ? "none" : AuthNameId;
            string blocker = ResolvePrimaryAuthorityProblem();
            Summary = string.Format(
                "role={0} listening={1} player={2} owner={3} input={4} auth={5}{6}",
                role,
                IsListening,
                playerName,
                LocalPlayerIsOwner,
                LocalInputEnabled,
                authName,
                string.IsNullOrWhiteSpace(blocker) ? string.Empty : " blocker=" + blocker
            );
        }
    }
}
