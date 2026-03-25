using NUnit.Framework;
using UnityEngine;

namespace Network_Game.Dialogue.Tests
{
    /// <summary>
    /// Unit tests for conversation key generation and isolation rules.
    /// ResolveConversationKey is public — called directly on a minimal service instance.
    /// Coverage group B in the multiplayer readiness manifest.
    /// </summary>
    public class NetworkDialogueServiceConversationKeyTests
    {
        private GameObject m_Root;
        private NetworkDialogueService m_Service;

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("TestDialogueService");
            m_Service = m_Root.AddComponent<NetworkDialogueService>();
        }

        [TearDown]
        public void TearDown()
        {
            // The test service is always a disabled secondary instance (the live scene
            // singleton fires first in Awake). Destroying the root is sufficient cleanup.
            if (m_Root != null)
                Object.DestroyImmediate(m_Root);
        }

        // ── B-01 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ConversationKey_IsSymmetric_ForNpcPlayerPair()
        {
            // key(npc=2, player=5) must equal key(player=5, npc=2).
            string forward = m_Service.ResolveConversationKey(2, 5, 0);
            string reverse = m_Service.ResolveConversationKey(5, 2, 0);

            Assert.That(forward, Is.EqualTo(reverse));
            Assert.That(forward, Is.EqualTo("2:5"));
        }

        // ── B-02 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ConversationKey_IsUnique_PerPlayer_SameNpc()
        {
            // Player A (netId=10) and Player B (netId=11) both talking to NPC (netId=2).
            string keyA = m_Service.ResolveConversationKey(2, 10, 0);
            string keyB = m_Service.ResolveConversationKey(2, 11, 0);

            Assert.That(keyA, Is.Not.EqualTo(keyB));
        }

        // ── B-03 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ConversationKey_IsUnique_PerNpc_SamePlayer()
        {
            // Player (netId=10) talking to NPC1 (netId=2) vs NPC2 (netId=3).
            // Mirrors the 3 NPC_Andre(Clone) instances in Behavior_Scene.
            string keyNpc1 = m_Service.ResolveConversationKey(2, 10, 0);
            string keyNpc2 = m_Service.ResolveConversationKey(3, 10, 0);
            string keyNpc3 = m_Service.ResolveConversationKey(4, 10, 0);

            Assert.That(keyNpc1, Is.Not.EqualTo(keyNpc2));
            Assert.That(keyNpc1, Is.Not.EqualTo(keyNpc3));
            Assert.That(keyNpc2, Is.Not.EqualTo(keyNpc3));
        }

        // ── B-04 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ConversationKey_FallsBackToClientScope_WhenBothIdsZero()
        {
            string key = m_Service.ResolveConversationKey(0, 0, 7);

            Assert.That(key, Is.EqualTo("client:7"));
        }

        // ── B-05 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ConversationKey_UsesActorScope_WhenOnlySpeakerProvided()
        {
            string key = m_Service.ResolveConversationKey(42, 0, 0);

            Assert.That(key, Is.EqualTo("actor:42"));
        }

        // ── B-06 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ConversationKey_RespectsOverride_WhenExplicitlyProvided()
        {
            string key = m_Service.ResolveConversationKey(2, 10, 0, "my_custom_key");

            Assert.That(key, Is.EqualTo("my_custom_key"));
        }

        // ── B-07 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ConversationKey_ThreeNpcsThreePlayers_AllNineKeysMustBeDistinct()
        {
            // Stress-test the key space with the actual scene topology:
            // 3 NPC_Andre clones (netIds 2,3,4) × 3 players (netIds 10,11,12).
            ulong[] npcIds = { 2, 3, 4 };
            ulong[] playerIds = { 10, 11, 12 };

            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (ulong npc in npcIds)
            {
                foreach (ulong player in playerIds)
                {
                    string key = m_Service.ResolveConversationKey(npc, player, 0);
                    Assert.That(
                        keys.Add(key),
                        Is.True,
                        $"Duplicate key '{key}' for npc={npc} player={player}"
                    );
                }
            }

            Assert.That(keys.Count, Is.EqualTo(9));
        }
    }
}
