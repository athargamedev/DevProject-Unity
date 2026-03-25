using System;
using System.Reflection;
using NUnit.Framework;

namespace Network_Game.Dialogue.Tests
{
    /// <summary>
    /// Unit tests for NetworkDialogueAuthGate.CanAccept.
    /// Pure static logic — no Unity objects or NetworkManager required.
    /// Coverage group A in the multiplayer readiness manifest.
    /// </summary>
    public class NetworkDialogueAuthGateTests
    {
        // NetworkDialogueAuthGate is internal — access via reflection.
        private static readonly Type s_GateType = typeof(NetworkDialogueService).Assembly
            .GetType("Network_Game.Dialogue.NetworkDialogueAuthGate");

        private static readonly MethodInfo s_CanAccept = s_GateType?.GetMethod(
            "CanAccept",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
        );

        private static bool InvokeCanAccept(
            bool requireAuth,
            bool isUserInitiated,
            ulong clientId,
            Func<ulong, bool> hasIdentity,
            out string rejectionReason
        )
        {
            Assert.That(s_CanAccept, Is.Not.Null, "CanAccept method not found via reflection.");
            var args = new object[] { requireAuth, isUserInitiated, clientId, hasIdentity, null };
            bool result = (bool)s_CanAccept.Invoke(null, args);
            rejectionReason = (string)args[4];
            return result;
        }

        // ── A-01 ─────────────────────────────────────────────────────────────────

        [Test]
        public void CanAccept_AllowsNonUserInitiated_WhenRequireAuthTrue()
        {
            // NPC auto-prompts must bypass the auth gate entirely.
            bool result = InvokeCanAccept(
                requireAuth: true,
                isUserInitiated: false,
                clientId: 5,
                hasIdentity: null,
                out string reason
            );

            Assert.That(result, Is.True);
            Assert.That(reason, Is.Null);
        }

        // ── A-02 ─────────────────────────────────────────────────────────────────

        [Test]
        public void CanAccept_RejectsUserInitiated_WhenNoIdentityAndRequireAuthTrue()
        {
            bool result = InvokeCanAccept(
                requireAuth: true,
                isUserInitiated: true,
                clientId: 5,
                hasIdentity: _ => false,
                out string reason
            );

            Assert.That(result, Is.False);
            Assert.That(reason, Is.EqualTo("auth_missing_identity"));
        }

        // ── A-03 ─────────────────────────────────────────────────────────────────

        [Test]
        public void CanAccept_AllowsUserInitiated_WhenRequireAuthFalse()
        {
            // Flag off — identity check is skipped entirely.
            bool result = InvokeCanAccept(
                requireAuth: false,
                isUserInitiated: true,
                clientId: 5,
                hasIdentity: _ => false,
                out string reason
            );

            Assert.That(result, Is.True);
        }

        // ── A-04 ─────────────────────────────────────────────────────────────────

        [Test]
        public void CanAccept_RejectsHost_ClientZero_WhenIdentityMissing()
        {
            // Host (clientId=0) without a registered snapshot must be rejected.
            bool result = InvokeCanAccept(
                requireAuth: true,
                isUserInitiated: true,
                clientId: 0,
                hasIdentity: _ => false,
                out string reason
            );

            Assert.That(result, Is.False);
            Assert.That(reason, Is.EqualTo("auth_missing_client"));
        }

        // ── A-05 ─────────────────────────────────────────────────────────────────

        [Test]
        public void CanAccept_AllowsHost_ClientZero_WhenIdentityPresent()
        {
            bool result = InvokeCanAccept(
                requireAuth: true,
                isUserInitiated: true,
                clientId: 0,
                hasIdentity: id => id == 0,
                out string reason
            );

            Assert.That(result, Is.True);
            Assert.That(reason, Is.Null);
        }

        // ── A-06 ─────────────────────────────────────────────────────────────────

        [Test]
        public void CanAccept_AllowsAllClients_WhenAllHaveIdentity_ThreePlayerScenario()
        {
            // Mirrors the live scene: host + 2 remote clients all registered.
            var registered = new System.Collections.Generic.HashSet<ulong> { 0, 1, 2 };

            foreach (ulong clientId in registered)
            {
                bool result = InvokeCanAccept(
                    requireAuth: true,
                    isUserInitiated: true,
                    clientId: clientId,
                    hasIdentity: id => registered.Contains(id),
                    out string reason
                );

                Assert.That(result, Is.True, $"Client {clientId} should be accepted.");
                Assert.That(reason, Is.Null);
            }
        }

        // ── A-07 ─────────────────────────────────────────────────────────────────

        [Test]
        public void CanAccept_RejectsUnregisteredClient_WhileOthersAreRegistered()
        {
            // Only clients 0 and 1 registered; client 2 tries to send a request.
            var registered = new System.Collections.Generic.HashSet<ulong> { 0, 1 };

            bool result = InvokeCanAccept(
                requireAuth: true,
                isUserInitiated: true,
                clientId: 2,
                hasIdentity: id => registered.Contains(id),
                out string reason
            );

            Assert.That(result, Is.False);
            Assert.That(reason, Is.EqualTo("auth_missing_identity"));
        }
    }
}
