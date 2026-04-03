using Unity.Netcode.Components;
using UnityEngine;

namespace Network_Game.ThirdPersonController
{
    /// <summary>
    /// Owner-authoritative <see cref="NetworkTransform"/>.
    /// The owning client writes its own position and rotation to the network; the server
    /// and all other clients receive and apply those values.
    /// <para>
    /// This is required for client-side CharacterController movement in Netcode for GameObjects.
    /// Without it the default server-authority causes the server to overwrite the client's
    /// locally-moved position every network tick, making the character appear frozen.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        /// <summary>Returns false — the owner (client) is authoritative, not the server.</summary>
        protected override bool OnIsServerAuthoritative() => false;
    }
}
