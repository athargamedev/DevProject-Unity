using Network_Game.Auth;
using Network_Game.Dialogue;
using PlayerController = Network_Game.ThirdPersonController.ThirdPersonController;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Network_Game.Diagnostics
{
    public static class AuthoritySnapshotBuilder
    {
        public static AuthoritySnapshot Build(string runId, string bootId)
        {
            var snapshot = new AuthoritySnapshot
            {
                RunId = runId ?? string.Empty,
                BootId = bootId ?? string.Empty,
                SceneName = SceneManager.GetActiveScene().name,
                Frame = Time.frameCount,
                RealtimeSinceStartup = Time.realtimeSinceStartup,
                CurrentPhase = ResolveCurrentPhase(),
            };

            NetworkManager manager = NetworkManager.Singleton;
            snapshot.NetworkManagerPresent = manager != null;
            snapshot.IsListening = manager != null && manager.IsListening;
            snapshot.IsServer = manager != null && manager.IsServer;
            snapshot.IsHost = manager != null && manager.IsHost;
            snapshot.IsClient = manager != null && manager.IsClient;
            snapshot.IsConnectedClient = manager != null && manager.IsConnectedClient;
            snapshot.LocalClientId = manager != null ? manager.LocalClientId : 0;

            LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
            snapshot.AuthServicePresent = authService != null;
            snapshot.HasAuthenticatedPlayer = authService != null && authService.HasCurrentPlayer;
            snapshot.AuthNameId =
                authService != null && authService.HasCurrentPlayer
                    ? authService.CurrentPlayer.NameId ?? string.Empty
                    : string.Empty;
            snapshot.AuthAttachedNetworkObjectId = authService != null
                ? authService.LocalPlayerNetworkId
                : 0;
            snapshot.PromptContextInitialized =
                authService != null && authService.IsPromptContextInitialized;
            snapshot.PromptContextAppliedToDialogue =
                authService != null && authService.LastPromptContextApplySucceeded;

            GameObject localPlayer = ResolveLocalPlayerObject(manager, authService);
            NetworkObject localNetworkObject = localPlayer != null
                ? localPlayer.GetComponent<NetworkObject>()
                : null;
            snapshot.LocalPlayerResolved = localPlayer != null;
            snapshot.LocalPlayerObjectName = localPlayer != null ? localPlayer.name : string.Empty;
            snapshot.LocalPlayerNetworkObjectId = localNetworkObject != null
                ? localNetworkObject.NetworkObjectId
                : (authService != null ? authService.LocalPlayerNetworkId : 0);
            snapshot.LocalPlayerOwnerClientId = localNetworkObject != null
                ? localNetworkObject.OwnerClientId
                : 0;
            snapshot.LocalPlayerIsSpawned = localNetworkObject != null && localNetworkObject.IsSpawned;
            snapshot.LocalPlayerIsOwner = localNetworkObject != null && localNetworkObject.IsOwner;

            PlayerController controller = localPlayer != null
                ? localPlayer.GetComponent<PlayerController>()
                : null;
            PlayerInput playerInput = localPlayer != null ? localPlayer.GetComponent<PlayerInput>() : null;
            snapshot.LocalControllerPresent = controller != null;
            snapshot.LocalControllerEnabled = controller != null && controller.enabled;
            snapshot.LocalInputComponentPresent = playerInput != null;
            snapshot.LocalInputEnabled = controller != null
                ? controller.PlayerInputComponentEnabled
                : playerInput != null && playerInput.enabled;
            snapshot.LocalActionMap = controller != null ? controller.ActiveInputActionMap : string.Empty;
            snapshot.CameraFollowAssigned = controller != null && controller.HasAssignedCameraFollow;

            NetworkDialogueService dialogueService = NetworkDialogueService.Instance;
            if (
                dialogueService != null
                && snapshot.LocalPlayerNetworkObjectId != 0
                && dialogueService.HasPlayerPromptContextBinding(snapshot.LocalPlayerNetworkObjectId)
            )
            {
                snapshot.PromptContextAppliedToDialogue = true;
            }

            snapshot.RefreshSummary();
            return snapshot;
        }

        private static GameObject ResolveLocalPlayerObject(
            NetworkManager manager,
            LocalPlayerAuthService authService
        )
        {
            if (manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null)
            {
                return manager.LocalClient.PlayerObject.gameObject;
            }

            if (
                manager != null
                && manager.SpawnManager != null
                && authService != null
                && authService.LocalPlayerNetworkId != 0
                && manager.SpawnManager.SpawnedObjects.TryGetValue(
                    authService.LocalPlayerNetworkId,
                    out NetworkObject spawned
                )
            )
            {
                return spawned != null ? spawned.gameObject : null;
            }

            try
            {
                GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
                for (int i = 0; i < taggedPlayers.Length; i++)
                {
                    if (taggedPlayers[i] == null)
                    {
                        continue;
                    }

                    NetworkObject taggedNetworkObject = taggedPlayers[i].GetComponent<NetworkObject>();
                    if (taggedNetworkObject != null && taggedNetworkObject.IsOwner)
                    {
                        return taggedPlayers[i];
                    }
                }
            }
            catch (UnityException)
            {
            }

            return null;
        }

        private static string ResolveCurrentPhase()
        {
            if (SceneWorkflowDiagnostics.IsMilestoneComplete("runtime_bind_auth_complete"))
            {
                return "runtime_bind_auth_complete";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("runtime_bind_core_complete"))
            {
                return "runtime_bind_core_complete";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("local_player_ready"))
            {
                return "local_player_ready";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("local_player_spawned"))
            {
                return "local_player_spawned";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("auth_gate_passed"))
            {
                return "auth_gate_passed";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("auth_identity_ready"))
            {
                return "auth_identity_ready";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("network_ready"))
            {
                return "network_ready";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("network_mode_known"))
            {
                return "network_mode_known";
            }

            if (SceneWorkflowDiagnostics.IsMilestoneComplete("scene_bootstrap_ready"))
            {
                return "scene_bootstrap_ready";
            }

            return "scene_compose";
        }
    }
}
