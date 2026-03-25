using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Network_Game.Dialogue.Tests
{
    /// <summary>
    /// Unit tests for OpenAIChatClient.TryExtractActionResponse.
    /// The method is internal static — accessed via reflection to match project convention.
    /// Coverage group D in the multiplayer readiness manifest.
    /// </summary>
    public class OpenAIChatClientActionResponseTests
    {
        private static readonly MethodInfo s_TryExtract = typeof(OpenAIChatClient).GetMethod(
            "TryExtractActionResponse",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        private static bool TryExtract(string content, out DialogueActionResponse response)
        {
            Assert.That(s_TryExtract, Is.Not.Null, "TryExtractActionResponse not found via reflection.");
            var args = new object[] { content, null };
            bool result = (bool)s_TryExtract.Invoke(null, args);
            response = (DialogueActionResponse)args[1];
            return result;
        }

        // ── D-01 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_ParsesWellFormedJson_SpeechAndEffectAction()
        {
            string json =
                @"{""speech"":""I call upon the storm!"",""actions"":[{""type"":""EFFECT"",""tag"":""lightning_strike"",""target"":""listener""}]}";

            bool result = TryExtract(json, out var response);

            Assert.That(result, Is.True);
            Assert.That(response.Speech, Is.EqualTo("I call upon the storm!"));
            Assert.That(response.Actions, Has.Count.EqualTo(1));
            Assert.That(response.Actions[0].Type, Is.EqualTo("EFFECT"));
            Assert.That(response.Actions[0].Tag, Is.EqualTo("lightning_strike"));
            Assert.That(response.Actions[0].Target, Is.EqualTo("listener"));
        }

        // ── D-02 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_ParsesAnimAction_WithTag()
        {
            string json =
                @"{""speech"":""Witness my power."",""actions"":[{""type"":""ANIM"",""tag"":""casting_spell""}]}";

            TryExtract(json, out var response);

            Assert.That(response.Actions, Has.Count.EqualTo(1));
            Assert.That(response.Actions[0].Type, Is.EqualTo("ANIM"));
            Assert.That(response.Actions[0].Tag, Is.EqualTo("casting_spell"));
        }

        // ── D-03 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_ParsesPatchAction_WithHealthAndScale()
        {
            string json =
                @"{""speech"":""You shrink."",""actions"":[{""type"":""PATCH"",""tag"":""listener"",""health"":-20,""scale"":0.5}]}";

            TryExtract(json, out var response);

            Assert.That(response.Actions, Has.Count.EqualTo(1));
            DialogueAction patch = response.Actions[0];
            Assert.That(patch.Type, Is.EqualTo("PATCH"));
            Assert.That(patch.HealthDelta, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(patch.Scale, Is.EqualTo(0.5f).Within(0.001f));
        }

        // ── D-04 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_FallsBackTo_ResponseTextField_LegacyFormat()
        {
            string json = @"{""responseText"":""An old response.""}";

            TryExtract(json, out var response);

            Assert.That(response.Speech, Is.EqualTo("An old response."));
            Assert.That(response.Actions, Is.Null);
        }

        // ── D-05 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_FallsBackTo_PlainText_WhenNotJson()
        {
            string plainText = "Just some plain dialogue text.";

            TryExtract(plainText, out var response);

            Assert.That(response.Speech, Is.EqualTo(plainText));
            Assert.That(response.Actions, Is.Null);
        }

        // ── D-06 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_HandlesEmptyActionsArray_NoDispatch()
        {
            string json = @"{""speech"":""Nothing to do."",""actions"":[]}";

            TryExtract(json, out var response);

            Assert.That(response.Speech, Is.EqualTo("Nothing to do."));
            // ParseActionArray returns null for empty arrays — no actions dispatched.
            Assert.That(response.Actions, Is.Null);
        }

        // ── D-07 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_SkipsActions_WithMissingTypeOrTag()
        {
            // Actions without both "type" and "tag" are silently dropped.
            string json =
                @"{""speech"":""Hello."",""actions"":[{""type"":""EFFECT""},{""tag"":""fire""},{""type"":""ANIM"",""tag"":""wave""}]}";

            TryExtract(json, out var response);

            // Only the third action has both type and tag — it should survive.
            Assert.That(response.Actions, Has.Count.EqualTo(1));
            Assert.That(response.Actions[0].Type, Is.EqualTo("ANIM"));
        }

        // ── D-08 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_ParsesDelayedAction_DelayField()
        {
            string json =
                @"{""speech"":""Slow burn."",""actions"":[{""type"":""EFFECT"",""tag"":""ember"",""delay"":2.5}]}";

            TryExtract(json, out var response);

            Assert.That(response.Actions[0].Delay, Is.EqualTo(2.5f).Within(0.001f));
        }

        // ── D-09 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_ParsesMultipleActions_PreservesOrder()
        {
            string json =
                @"{""speech"":""Combo."",""actions"":[
                    {""type"":""ANIM"",""tag"":""raise_hand""},
                    {""type"":""EFFECT"",""tag"":""spark"",""delay"":0.3},
                    {""type"":""EFFECT"",""tag"":""explosion"",""delay"":1.0}
                ]}";

            TryExtract(json, out var response);

            Assert.That(response.Actions, Has.Count.EqualTo(3));
            Assert.That(response.Actions[0].Tag, Is.EqualTo("raise_hand"));
            Assert.That(response.Actions[1].Tag, Is.EqualTo("spark"));
            Assert.That(response.Actions[2].Tag, Is.EqualTo("explosion"));
        }

        // ── D-10 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_DoesNotThrow_OnMalformedJson()
        {
            // Truncated JSON — simulates a model cutting off mid-token.
            string broken = @"{""speech"":""I was cut o";

            Assert.DoesNotThrow(() => TryExtract(broken, out _));

            TryExtract(broken, out var response);
            // Falls back to plain-text path.
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Speech, Is.EqualTo(broken));
        }

        // ── D-11 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_ReturnsEmptySpeech_WhenContentIsEmpty()
        {
            TryExtract(string.Empty, out var response);

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Speech, Is.EqualTo(string.Empty));
        }

        // ── D-12 ─────────────────────────────────────────────────────────────────

        [Test]
        public void TryExtract_ParsesEffectColor_AndIntensity()
        {
            string json =
                @"{""speech"":""Crimson tide."",""actions"":[{""type"":""EFFECT"",""tag"":""wave"",""color"":""#FF0000"",""scale"":2.0}]}";

            TryExtract(json, out var response);

            DialogueAction action = response.Actions[0];
            Assert.That(action.PatchColor, Is.EqualTo("#FF0000"));
            Assert.That(action.Scale, Is.EqualTo(2.0f).Within(0.001f));
        }
    }
}
