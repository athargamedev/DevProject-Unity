using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Network_Game.Dialogue.Tests
{
    /// <summary>
    /// Unit tests for per-request and per-conversation admission control.
    /// TryGetAutoRequestBlockReason is public; internal conversation state is
    /// seeded via reflection to avoid requiring a running NetworkManager.
    /// Coverage group C in the multiplayer readiness manifest.
    /// </summary>
    public class NetworkDialogueServiceAdmissionTests
    {
        private GameObject m_Root;
        private NetworkDialogueService m_Service;

        // Reflection handles for private inner state.
        private static readonly System.Type s_ConversationStateType =
            typeof(NetworkDialogueService).GetNestedType(
                "ConversationState",
                BindingFlags.NonPublic
            );

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("TestAdmission");
            m_Service = m_Root.AddComponent<NetworkDialogueService>();
        }

        [TearDown]
        public void TearDown()
        {
            // The test service is always a disabled secondary instance (the live scene
            // singleton fires first in Awake and returns early). Destroying the root is
            // sufficient — Instance never points to the test component.
            if (m_Root != null)
                Object.DestroyImmediate(m_Root);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects a ConversationState entry into the service's private dictionary
        /// so admission tests can simulate in-flight / awaiting-user-input conditions
        /// without running the full request worker.
        /// </summary>
        private void SetConversationState(
            string key,
            bool isInFlight = false,
            bool hasOutstandingRequest = false,
            bool awaitingUserInput = false,
            string lastCompletedPrompt = null,
            float lastCompletedAt = float.MinValue
        )
        {
            Assert.That(
                s_ConversationStateType,
                Is.Not.Null,
                "ConversationState nested type not found."
            );

            object state = System.Activator.CreateInstance(s_ConversationStateType, nonPublic: true);
            SetField(state, "IsInFlight", isInFlight);
            SetField(state, "HasOutstandingRequest", hasOutstandingRequest);
            SetField(state, "AwaitingUserInput", awaitingUserInput);
            SetField(state, "LastCompletedPrompt", lastCompletedPrompt ?? string.Empty);
            SetField(state, "LastCompletedAt", lastCompletedAt);

            // Get the private m_ConversationStates dictionary and insert the entry.
            var dictField = typeof(NetworkDialogueService).GetField(
                "m_ConversationStates",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.That(dictField, Is.Not.Null);

            // Use non-generic IDictionary to avoid referencing the private ConversationState type.
            var idict = dictField.GetValue(m_Service) as System.Collections.IDictionary;
            Assert.That(idict, Is.Not.Null);
            idict[key] = state;
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo f = instance.GetType().GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.That(f, Is.Not.Null, $"Field '{name}' not found on {instance.GetType().Name}.");
            f.SetValue(instance, value);
        }

        // ── C-01 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Admission_BlocksRequest_WhenConversationIsInFlight()
        {
            const string key = "2:10";
            SetConversationState(key, isInFlight: true);

            bool blocked = m_Service.TryGetAutoRequestBlockReason(
                key, "Hello", false, 0f, false, out string reason
            );

            Assert.That(blocked, Is.True);
            Assert.That(reason, Is.EqualTo("conversation_in_flight"));
        }

        // ── C-02 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Admission_BlocksRepeatedPrompt_CaseInsensitive()
        {
            const string key = "2:10";
            const string prompt = "Tell me your secrets";
            SetConversationState(
                key,
                lastCompletedPrompt: prompt,
                lastCompletedAt: Time.realtimeSinceStartup
            );

            bool blocked = m_Service.TryGetAutoRequestBlockReason(
                key, "TELL ME YOUR SECRETS", blockRepeatedPrompt: true, 0f, false, out string reason
            );

            Assert.That(blocked, Is.True);
            Assert.That(reason, Is.EqualTo("duplicate_prompt"));
        }

        // ── C-03 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Admission_AllowsRepeat_AfterMinRepeatDelay_HasElapsed()
        {
            const string key = "2:10";
            // LastCompletedAt set far in the past — delay has long elapsed.
            SetConversationState(
                key,
                lastCompletedPrompt: "greet",
                lastCompletedAt: -9999f
            );

            bool blocked = m_Service.TryGetAutoRequestBlockReason(
                key, "greet", false, minRepeatDelaySeconds: 5f, false, out string reason
            );

            Assert.That(blocked, Is.False);
        }

        // ── C-04 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Admission_BlocksRequest_WhenRepeatDelayHasNotElapsed()
        {
            const string key = "2:10";
            // LastCompletedAt is "now" — elapsed ≈ 0, well under the 60s delay.
            SetConversationState(
                key,
                lastCompletedAt: Time.realtimeSinceStartup
            );

            bool blocked = m_Service.TryGetAutoRequestBlockReason(
                key, "anything", false, minRepeatDelaySeconds: 60f, false, out string reason
            );

            Assert.That(blocked, Is.True);
            Assert.That(reason, Is.EqualTo("repeat_delay"));
        }

        // ── C-05 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Admission_BlocksAutoRequest_WhenAwaitingUserInput()
        {
            const string key = "2:10";
            SetConversationState(key, awaitingUserInput: true);

            bool blocked = m_Service.TryGetAutoRequestBlockReason(
                key, "npc_greeting", false, 0f, requireUserReply: true, out string reason
            );

            Assert.That(blocked, Is.True);
            Assert.That(reason, Is.EqualTo("awaiting_user_message"));
        }

        // ── C-06 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Admission_PassesFreshConversation_NoBlockReasons()
        {
            // A brand-new conversation key with no history should always pass.
            bool blocked = m_Service.TryGetAutoRequestBlockReason(
                "99:200", "Hello stranger", false, 0f, false, out string reason
            );

            Assert.That(blocked, Is.False);
            Assert.That(reason, Is.Null);
        }

        // ── C-07 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Admission_PerConversationInFlight_DoesNotAffectOtherConversation()
        {
            // PlayerA↔NPC in flight should not block PlayerB↔NPC.
            SetConversationState("2:10", isInFlight: true);

            bool blocked = m_Service.TryGetAutoRequestBlockReason(
                "2:11", "Hello", false, 0f, false, out string reason
            );

            Assert.That(blocked, Is.False, "PlayerB conversation must not be blocked by PlayerA's in-flight.");
        }
    }
}
