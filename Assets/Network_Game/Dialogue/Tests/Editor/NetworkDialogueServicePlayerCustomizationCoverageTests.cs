using NUnit.Framework;
using UnityEngine;

namespace Network_Game.Dialogue.Tests
{
    // Coverage-focused EditMode tests for prompt-context and customization helpers.
    public class NetworkDialogueServicePlayerCustomizationTests
    {
        [Test]
        public void NormalizePromptContextJson_ReplacesPlaceholderWithNetworkContextEnvelope()
        {
            string result = NetworkDialogueServiceTestReflection.InvokeString(
                "NormalizePromptContextJson",
                "mage \"ice\"\nline",
                "{ }"
            );

            Assert.That(result, Does.Contain("\"source\":\"network_context\""));
            Assert.That(result, Does.Contain("\"customization\":{}"));
            Assert.That(result, Does.Contain("\"name_id\":\"mage \\\"ice\\\"\\nline\""));
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("{}", true)]
        [TestCase("{ }", true)]
        [TestCase("null", true)]
        [TestCase("{\"name_id\":\"player_1\"}", false)]
        public void IsPlaceholderPromptContextJson_RecognizesOnlyPlaceholderPayloads(
            string json,
            bool expected
        )
        {
            bool result = NetworkDialogueServiceTestReflection.InvokeBool(
                "IsPlaceholderPromptContextJson",
                json
            );

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void BuildPlayerEffectModifier_ParsesAndClampsCustomizationValues()
        {
            object identity = NetworkDialogueServiceTestReflection.CreatePlayerIdentityBinding(
                "{\"vulnerability\":\"9.0\",\"effect_size_bias\":\"0.1\",\"effect_duration_bias\":6.5,\"aggression_bias\":\"0.1\",\"has_shield\":\"1\",\"element_affinity\":\"storm\",\"color_theme\":\"fire\"}"
            );

            object modifier = NetworkDialogueServiceTestReflection.InvokeBuildPlayerEffectModifier(identity);

            Assert.That(
                NetworkDialogueServiceTestReflection.GetFieldValue<float>(
                    modifier,
                    "DamageScaleReceived"
                ),
                Is.EqualTo(3f)
            );
            Assert.That(
                NetworkDialogueServiceTestReflection.GetFieldValue<float>(modifier, "EffectSizeScale"),
                Is.EqualTo(0.25f)
            );
            Assert.That(
                NetworkDialogueServiceTestReflection.GetFieldValue<float>(
                    modifier,
                    "EffectDurationScale"
                ),
                Is.EqualTo(4f)
            );
            Assert.That(
                NetworkDialogueServiceTestReflection.GetFieldValue<float>(modifier, "AggressionBias"),
                Is.EqualTo(0.25f)
            );
            Assert.That(
                NetworkDialogueServiceTestReflection.GetFieldValue<bool>(modifier, "IsShielded"),
                Is.True
            );
            Assert.That(
                NetworkDialogueServiceTestReflection.GetFieldValue<string>(
                    modifier,
                    "PreferredElement"
                ),
                Is.EqualTo("storm")
            );

            Color? preferredColor = NetworkDialogueServiceTestReflection.GetFieldValue<Color?>(
                modifier,
                "PreferredColor"
            );
            Assert.That(preferredColor.HasValue, Is.True);
            Assert.That(preferredColor.Value, Is.EqualTo(new Color(1f, 0.4f, 0.05f)));
        }

        [Test]
        public void ParseColorFromTheme_SupportsNamedThemesAndHtmlColors()
        {
            Color? named = NetworkDialogueServiceTestReflection.InvokeNullableColor(
                "ParseColorFromTheme",
                "storm"
            );
            Color? html = NetworkDialogueServiceTestReflection.InvokeNullableColor(
                "ParseColorFromTheme",
                "#336699"
            );
            Color? invalid = NetworkDialogueServiceTestReflection.InvokeNullableColor(
                "ParseColorFromTheme",
                "not-a-theme"
            );

            Assert.That(named, Is.EqualTo(new Color(0.5f, 0.5f, 0.9f)));
            Assert.That(html.HasValue, Is.True);
            Assert.That(html.Value.r, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(html.Value.g, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(html.Value.b, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(invalid, Is.Null);
        }

        [Test]
        public void BuildNarrativeHints_CombinesPlayerAndNpcSpecificSignals()
        {
            NpcDialogueProfile npcProfile = NetworkDialogueServiceTestReflection.CreateNpcProfile(
                "npc.forge_keeper"
            );
            string customizationJson =
                "{\"element_affinity\":\"fire\",\"color_theme\":\"red\",\"aggression_bias\":\"1.5\",\"has_shield\":\"true\",\"vulnerability\":\"1.6\",\"class\":\"warden\",\"reputation_npc.forge_keeper\":\"85\",\"inventory_tags\":\"ember blade, shield core\",\"quest_flags\":\"forge_opened\",\"last_action\":\"defeated the ash sentinel\"}";

            string hints = NetworkDialogueServiceTestReflection.InvokeString(
                "BuildNarrativeHints",
                NetworkDialogueServiceTestReflection.CreatePlayerIdentityBinding(customizationJson),
                npcProfile,
                customizationJson
            );

            Assert.That(hints, Does.StartWith("[NarrativeHints]"));
            Assert.That(hints, Does.Contain("fire affinity"));
            Assert.That(hints, Does.Contain("color theme is red"));
            Assert.That(hints, Does.Contain("hostile to this player"));
            Assert.That(hints, Does.Contain("active shield"));
            Assert.That(hints, Does.Contain("player is vulnerable"));
            Assert.That(hints, Does.Contain("player is a warden"));
            Assert.That(hints, Does.Contain("trusted champion"));
            Assert.That(hints, Does.Contain("ember blade, shield core"));
            Assert.That(hints, Does.Contain("forge_opened"));
            Assert.That(hints, Does.Contain("defeated the ash sentinel"));
            Assert.That(hints, Does.Contain("Do not reference internal identifiers"));

            Object.DestroyImmediate(npcProfile);
        }
    }
}
