using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Network_Game.Dialogue.Tests
{
    internal static class NetworkDialogueServiceTestReflection
    {
        private static readonly Type ServiceType = typeof(NetworkDialogueService);
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static object InvokePrivateStatic(string methodName, params object[] args)
        {
            MethodInfo method = ServiceType.GetMethod(methodName, PrivateStatic);
            Assert.That(method, Is.Not.Null, $"Could not resolve {methodName} on {ServiceType.FullName}.");
            return method.Invoke(null, args);
        }

        public static bool InvokeBool(string methodName, params object[] args)
        {
            return (bool)InvokePrivateStatic(methodName, args);
        }

        public static string InvokeString(string methodName, params object[] args)
        {
            return (string)InvokePrivateStatic(methodName, args);
        }

        public static Color? InvokeNullableColor(string methodName, params object[] args)
        {
            object result = InvokePrivateStatic(methodName, args);
            return result is Color color ? color : (Color?)null;
        }

        public static object CreatePlayerIdentityBinding(
            string customizationJson,
            string nameId = "player_local",
            ulong clientId = 0,
            ulong playerNetworkId = 0
        )
        {
            Type bindingType = ServiceType.GetNestedType("PlayerIdentityBinding", BindingFlags.NonPublic);
            Assert.That(bindingType, Is.Not.Null, "PlayerIdentityBinding nested type was not found.");

            object binding = Activator.CreateInstance(bindingType, true);
            SetField(binding, "CustomizationJson", customizationJson);
            SetField(binding, "NameId", nameId);
            SetField(binding, "ClientId", clientId);
            SetField(binding, "PlayerNetworkId", playerNetworkId);
            return binding;
        }

        public static object InvokeBuildPlayerEffectModifier(object identityBinding)
        {
            return InvokePrivateStatic("BuildPlayerEffectModifier", identityBinding);
        }

        public static NpcDialogueProfile CreateNpcProfile(string profileId)
        {
            NpcDialogueProfile profile = ScriptableObject.CreateInstance<NpcDialogueProfile>();
            SetField(profile, "m_ProfileId", profileId);
            return profile;
        }

        public static T GetFieldValue<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Could not resolve field {fieldName} on {instance.GetType().FullName}.");
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Could not resolve field {fieldName} on {instance.GetType().FullName}.");
            field.SetValue(instance, value);
        }
    }
}
