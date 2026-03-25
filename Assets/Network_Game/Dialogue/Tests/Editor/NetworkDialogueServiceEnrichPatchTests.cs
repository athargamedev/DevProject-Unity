using System.Reflection;
using NUnit.Framework;

namespace Network_Game.Dialogue.Tests
{
    /// <summary>
    /// Unit tests for NetworkDialogueService.EnrichPatchFromSpeech.
    /// The method is private static — accessed via reflection.
    /// It infers PATCH fields (Visible, PatchColor, Emission, Scale) from NPC speech
    /// when the LLM omits them from the structured action payload.
    /// Coverage group F in the multiplayer readiness manifest.
    /// </summary>
    public class NetworkDialogueServiceEnrichPatchTests
    {
        private static readonly MethodInfo s_EnrichPatch = typeof(NetworkDialogueService).GetMethod(
            "EnrichPatchFromSpeech",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        private static DialogueAction Enrich(DialogueAction action, string speech)
        {
            Assert.That(s_EnrichPatch, Is.Not.Null, "EnrichPatchFromSpeech not found via reflection.");
            s_EnrichPatch.Invoke(null, new object[] { action, speech });
            return action;
        }

        private static DialogueAction MakePatch(string tag = "listener") =>
            new DialogueAction { Type = "PATCH", Tag = tag };

        // ── F-01 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_SetsVisible_False_WhenSpeechContainsInvisibleWord()
        {
            var action = Enrich(MakePatch(), "You vanish into the shadows!");

            Assert.That(action.Visible, Is.EqualTo(false));
        }

        // ── F-02 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_SetsVisible_True_WhenSpeechContainsRevealWord()
        {
            var action = Enrich(MakePatch(), "You appear before me, revealed at last.");

            Assert.That(action.Visible, Is.EqualTo(true));
        }

        // ── F-03 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_SetsPatchColor_FromFirstMatchingColorWord()
        {
            var action = Enrich(MakePatch(), "A crimson mist envelops you.");

            Assert.That(action.PatchColor, Is.EqualTo("crimson"));
        }

        // ── F-04 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_SetsEmission_WhenSpeechContainsGlowWord()
        {
            var action = Enrich(MakePatch(), "You begin to glow with power.");

            Assert.That(action.Emission, Is.EqualTo(2f).Within(0.001f));
        }

        // ── F-05 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_SetsScale_Large_WhenSpeechContainsBigWord()
        {
            var action = Enrich(MakePatch(), "You grow to a massive size!");

            Assert.That(action.Scale, Is.EqualTo(2.5f).Within(0.001f));
        }

        // ── F-06 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_SetsScale_Small_WhenSpeechContainsShrinkWord()
        {
            var action = Enrich(MakePatch(), "You shrink to a tiny creature.");

            Assert.That(action.Scale, Is.EqualTo(0.3f).Within(0.001f));
        }

        // ── F-07 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_DoesNotOverwrite_WhenActionAlreadyHasFields()
        {
            // Pre-populated action: PatchHasFields returns true, so enrich skips entirely.
            var action = MakePatch();
            action.Scale = 1.5f;

            Enrich(action, "You grow to a massive size!");

            // Scale must remain 1.5, not overwritten to 2.5.
            Assert.That(action.Scale, Is.EqualTo(1.5f).Within(0.001f));
        }

        // ── F-08 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_NoChange_WhenSpeechIsEmpty()
        {
            var action = Enrich(MakePatch(), string.Empty);

            Assert.That(action.Visible,    Is.Null);
            Assert.That(action.PatchColor, Is.Null.Or.Empty);
            Assert.That(action.Emission,   Is.Null);
            Assert.That(action.Scale,      Is.Null);
        }

        // ── F-09 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_NoChange_WhenSpeechIsWhitespace()
        {
            var action = Enrich(MakePatch(), "   ");

            Assert.That(action.Visible,    Is.Null);
            Assert.That(action.PatchColor, Is.Null.Or.Empty);
            Assert.That(action.Emission,   Is.Null);
            Assert.That(action.Scale,      Is.Null);
        }

        // ── F-10 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_InvisibleTakesPrecedence_OverVisible_WhenBothPresentInSpeech()
        {
            // "invisible" appears before "appear" in s_InvisibleWords check order.
            var action = Enrich(MakePatch(), "You are invisible but appear to flicker.");

            Assert.That(action.Visible, Is.EqualTo(false));
        }

        // ── F-11 ─────────────────────────────────────────────────────────────────

        [Test]
        public void EnrichPatch_ColorMatchIsCaseInsensitive()
        {
            // speech is lowercased before matching, so "BLUE" → "blue" → color match.
            var action = Enrich(MakePatch(), "A BLUE flame erupts.");

            Assert.That(action.PatchColor, Is.EqualTo("blue"));
        }
    }
}
