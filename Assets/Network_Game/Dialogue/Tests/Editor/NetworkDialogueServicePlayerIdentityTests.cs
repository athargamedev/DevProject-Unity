using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Network_Game.Dialogue.Tests
{
    /// <summary>
    /// Unit tests for NetworkDialogueService player identity cache:
    /// UpsertPlayerIdentity, RebuildPlayerIdentityLookup, FindOrCreatePlayerIdentityBinding.
    /// All private members accessed via reflection.
    /// Coverage group G in the multiplayer readiness manifest.
    /// </summary>
    public class NetworkDialogueServicePlayerIdentityTests
    {
        private GameObject m_Root;
        private NetworkDialogueService m_Service;

        private static readonly MethodInfo s_Upsert = typeof(NetworkDialogueService).GetMethod(
            "UpsertPlayerIdentity",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        private static readonly MethodInfo s_Rebuild = typeof(NetworkDialogueService).GetMethod(
            "RebuildPlayerIdentityLookup",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        [SetUp]
        public void SetUp()
        {
            m_Root    = new GameObject("TestPlayerIdentity");
            m_Service = m_Root.AddComponent<NetworkDialogueService>();

            Assert.That(s_Upsert,  Is.Not.Null, "UpsertPlayerIdentity not found via reflection.");
            Assert.That(s_Rebuild, Is.Not.Null, "RebuildPlayerIdentityLookup not found via reflection.");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null)
                Object.DestroyImmediate(m_Root);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void Upsert(ulong clientId, ulong playerNetworkId, string nameId, string json = "{}")
        {
            s_Upsert.Invoke(m_Service, new object[] { clientId, playerNetworkId, nameId, json });
        }

        private IDictionary GetByClientId()
        {
            var field = typeof(NetworkDialogueService).GetField(
                "m_PlayerIdentityByClientId",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.That(field, Is.Not.Null, "m_PlayerIdentityByClientId not found.");
            return field.GetValue(m_Service) as IDictionary;
        }

        private IDictionary GetByNetworkId()
        {
            var field = typeof(NetworkDialogueService).GetField(
                "m_PlayerIdentityByNetworkId",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.That(field, Is.Not.Null, "m_PlayerIdentityByNetworkId not found.");
            return field.GetValue(m_Service) as IDictionary;
        }

        private static string GetNameId(object binding)
        {
            return (string)binding.GetType()
                .GetField("NameId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(binding);
        }

        // ── G-01 ─────────────────────────────────────────────────────────────────

        [Test]
        public void UpsertPlayerIdentity_CreatesEntry_IndexedByClientId()
        {
            Upsert(clientId: 3, playerNetworkId: 10, nameId: "Alice");

            var byClient = GetByClientId();
            Assert.That(byClient.Contains((ulong)3), Is.True);
            Assert.That(GetNameId(byClient[(ulong)3]), Is.EqualTo("Alice"));
        }

        // ── G-02 ─────────────────────────────────────────────────────────────────

        [Test]
        public void UpsertPlayerIdentity_CreatesEntry_IndexedByNetworkId()
        {
            Upsert(clientId: 3, playerNetworkId: 10, nameId: "Alice");

            var byNetwork = GetByNetworkId();
            Assert.That(byNetwork.Contains((ulong)10), Is.True);
            Assert.That(GetNameId(byNetwork[(ulong)10]), Is.EqualTo("Alice"));
        }

        // ── G-03 ─────────────────────────────────────────────────────────────────

        [Test]
        public void UpsertPlayerIdentity_PreservesRealName_OverClientPlaceholder()
        {
            // First upsert sets a real name.
            Upsert(clientId: 3, playerNetworkId: 10, nameId: "Alice");
            // Second upsert tries to overwrite with a placeholder — must be rejected.
            Upsert(clientId: 3, playerNetworkId: 10, nameId: "client_3");

            var byClient = GetByClientId();
            Assert.That(
                GetNameId(byClient[(ulong)3]),
                Is.EqualTo("Alice"),
                "Real name must not be replaced by a client_N placeholder."
            );
        }

        // ── G-04 ─────────────────────────────────────────────────────────────────

        [Test]
        public void UpsertPlayerIdentity_UpdatesRealName_WhenPlaceholderWasStored()
        {
            // First upsert stores a placeholder.
            Upsert(clientId: 3, playerNetworkId: 10, nameId: "client_3");
            // Second upsert provides the real Supabase name — must win.
            Upsert(clientId: 3, playerNetworkId: 10, nameId: "Andre");

            var byClient = GetByClientId();
            Assert.That(
                GetNameId(byClient[(ulong)3]),
                Is.EqualTo("Andre"),
                "Real name must replace the client_N placeholder."
            );
        }

        // ── G-05 ─────────────────────────────────────────────────────────────────

        [Test]
        public void UpsertPlayerIdentity_ThreeClients_AllIndexedIndependently()
        {
            // Mirrors the live scene: host + 2 remotes.
            Upsert(clientId: 0, playerNetworkId: 10, nameId: "HostPlayer");
            Upsert(clientId: 1, playerNetworkId: 11, nameId: "RemoteA");
            Upsert(clientId: 2, playerNetworkId: 12, nameId: "RemoteB");

            var byClient  = GetByClientId();
            var byNetwork = GetByNetworkId();

            Assert.That(GetNameId(byClient[(ulong)0]),  Is.EqualTo("HostPlayer"));
            Assert.That(GetNameId(byClient[(ulong)1]),  Is.EqualTo("RemoteA"));
            Assert.That(GetNameId(byClient[(ulong)2]),  Is.EqualTo("RemoteB"));
            Assert.That(GetNameId(byNetwork[(ulong)10]), Is.EqualTo("HostPlayer"));
            Assert.That(GetNameId(byNetwork[(ulong)11]), Is.EqualTo("RemoteA"));
            Assert.That(GetNameId(byNetwork[(ulong)12]), Is.EqualTo("RemoteB"));
        }

        // ── G-06 ─────────────────────────────────────────────────────────────────

        [Test]
        public void UpsertPlayerIdentity_DoesNotIndex_WhenClientIdIsMaxValue()
        {
            // ulong.MaxValue is the sentinel for "no client id".
            Upsert(clientId: ulong.MaxValue, playerNetworkId: 10, nameId: "Ghost");

            var byClient = GetByClientId();
            Assert.That(
                byClient.Contains(ulong.MaxValue),
                Is.False,
                "Sentinel clientId (ulong.MaxValue) must not be added to the client lookup."
            );
        }

        // ── G-07 ─────────────────────────────────────────────────────────────────

        [Test]
        public void UpsertPlayerIdentity_DoesNotIndex_ByNetworkId_WhenNetworkIdIsZero()
        {
            // netId=0 means the network object isn't spawned yet.
            Upsert(clientId: 5, playerNetworkId: 0, nameId: "Pending");

            var byNetwork = GetByNetworkId();
            Assert.That(
                byNetwork.Contains((ulong)0),
                Is.False,
                "netId=0 must not be inserted into the network-id lookup."
            );
        }

        // ── G-08 ─────────────────────────────────────────────────────────────────

        [Test]
        public void RebuildPlayerIdentityLookup_ExcludesDisabledBindings()
        {
            // Seed an entry, then disable its binding in the list, then rebuild.
            // RebuildPlayerIdentityLookup clears and re-indexes from m_PlayerIdentityBindings,
            // skipping any entry where Enabled == false.
            Upsert(clientId: 1, playerNetworkId: 5, nameId: "Seeded");
            Assert.That(GetByClientId().Count, Is.GreaterThan(0), "Pre-condition: entry must exist before disable.");

            // Disable the binding directly in the list.
            var listField = typeof(NetworkDialogueService).GetField(
                "m_PlayerIdentityBindings",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.That(listField, Is.Not.Null);
            var list = listField.GetValue(m_Service) as System.Collections.IList;
            Assert.That(list, Is.Not.Null);
            Assert.That(list.Count, Is.GreaterThan(0));

            object binding = list[0];
            FieldInfo enabledField = binding.GetType().GetField(
                "Enabled",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.That(enabledField, Is.Not.Null);
            enabledField.SetValue(binding, false);

            s_Rebuild.Invoke(m_Service, null);

            Assert.That(GetByClientId().Count,  Is.EqualTo(0), "Disabled binding must not be re-indexed by clientId.");
            Assert.That(GetByNetworkId().Count, Is.EqualTo(0), "Disabled binding must not be re-indexed by networkId.");
        }
    }
}
