using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Auto-applies correct import settings for all modelAndre Mixamo FBX animation files.
/// Placed in an Editor folder, this runs automatically on every import/reimport of
/// files under .../Mixamo/modelAndre/.
/// </summary>
public class MixamoAndreImporter : AssetPostprocessor
{
    // Animations that should loop (locomotion, idle, airborne loops)
    private static readonly string[] LoopClips = new[]
    {
        "idle", "walk", "run", "sprint",
        "strafeleft", "straferight", "walkbackward", "runbackward",
        "crouchidle", "crouchwalk",
        "leftturn", "rightturn", "turn180",
        "fallingidle"
    };

    private bool IsAndreAnim =>
        assetPath.Contains("/Mixamo/modelAndre/") &&
        assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

    void OnPreprocessModel()
    {
        if (!IsAndreAnim) return;

        var mi = assetImporter as ModelImporter;
        if (mi == null) return;

        // Animation-only FBX — skip camera/light/material import
        // (Mixamo @-clip FBXs contain no mesh; no importMeshes API in Unity 6)
        mi.importCameras        = false;
        mi.importLights         = false;
        mi.importVisibility     = false;
        mi.materialImportMode   = ModelImporterMaterialImportMode.None;

        // Humanoid rig, copy avatar from the modelAndre character FBX
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup   = ModelImporterAvatarSetup.CopyFromOther;

        // Find the modelAndre character avatar (the mesh FBX, not an @-clip)
        string avatarPath = FindModelAndreAvatarPath();
        if (!string.IsNullOrEmpty(avatarPath))
        {
            var avatarAsset = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            if (avatarAsset != null)
                mi.sourceAvatar = avatarAsset;
        }
    }

    void OnPreprocessAnimation()
    {
        if (!IsAndreAnim) return;

        var mi = assetImporter as ModelImporter;
        if (mi == null) return;

        // Derive the clip keyword from the filename: modelAndre@Run.fbx → "run"
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        string clipName = fileName.Contains("@") ? fileName.Split('@')[1] : fileName;
        string clipKey  = clipName.ToLower().Replace(" ", "");

        bool isLoop = System.Array.Exists(LoopClips, k => clipKey.Contains(k));

        // Start from Unity's auto-detected clips (correct take names & frame ranges),
        // then patch loop settings and root motion bake flags.
        var defaults = mi.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0) return;

        for (int i = 0; i < defaults.Length; i++)
        {
            defaults[i].loopTime = isLoop;

            // Bake all root motion into pose — ThirdPersonController drives position via
            // CharacterController physics, not AnimatorRootMotion.
            defaults[i].lockRootHeightY    = true;
            defaults[i].lockRootRotation   = true;
            defaults[i].lockRootPositionXZ = true;

            defaults[i].keepOriginalPositionY   = false;
            defaults[i].heightFromFeet          = true;   // anchor root Y to feet, not center of mass
            defaults[i].keepOriginalPositionXZ  = false;
            defaults[i].keepOriginalOrientation = false;
        }

        mi.clipAnimations = defaults;
    }

    /// <summary>
    /// Finds the modelAndre base character FBX (the one without @ in the name) to copy the avatar from.
    /// </summary>
    private static string FindModelAndreAvatarPath()
    {
        string[] guids = AssetDatabase.FindAssets("modelAndre t:Model",
            new[] { "Assets/Network_Game/ThirdPersonController/Character" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path).ToLower();
            // The base mesh FBX has no @ separator
            if (!name.Contains("@") && name.Contains("modelandre"))
                return path;
        }
        return null;
    }
}
