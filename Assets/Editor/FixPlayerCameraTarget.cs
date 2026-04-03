using UnityEngine;
using UnityEditor;

/// <summary>
/// One-shot fix: wires PlayerArmature.ThirdPersonController.CinemachineCameraTarget
/// to the PlayerCameraRoot child and resets ShoulderOffset on the scene camera.
/// Menu: Tools/Andre/Fix Camera Setup
/// </summary>
public static class FixPlayerCameraTarget
{
    private const string PrefabPath =
        "Assets/Network_Game/ThirdPersonController/Prefabs/PlayerArmature.prefab";

    [MenuItem("Tools/Andre/Fix Camera Setup")]
    public static void Fix()
    {
        // ── 1. Fix prefab CinemachineCameraTarget ─────────────────────
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[FixCamera] Prefab not found: {PrefabPath}");
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var root = scope.prefabContentsRoot;
            var cameraRoot = root.transform.Find("PlayerCameraRoot");
            if (cameraRoot == null)
            {
                Debug.LogError("[FixCamera] PlayerCameraRoot child not found in prefab.");
                return;
            }

            var controller = root.GetComponent<Network_Game.ThirdPersonController.ThirdPersonController>();
            if (controller == null)
            {
                Debug.LogError("[FixCamera] ThirdPersonController component not found on prefab root.");
                return;
            }

            controller.CinemachineCameraTarget = cameraRoot.gameObject;
            Debug.Log($"[FixCamera] CinemachineCameraTarget -> '{cameraRoot.name}' (Y={cameraRoot.localPosition.y})");
        }

        // ── 2. Fix scene CinemachineThirdPersonFollow settings ─────────
        var vcam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineThirdPersonFollow>();
        if (vcam != null)
        {
            Undo.RecordObject(vcam, "Fix Camera ShoulderOffset");
            vcam.ShoulderOffset    = new Vector3(0.5f, 0.4f, 0.5f);
            vcam.CameraDistance    = 6.0f;
            vcam.VerticalArmLength = 0.4f;
            EditorUtility.SetDirty(vcam);
            Debug.Log("[FixCamera] CinemachineThirdPersonFollow: ShoulderOffset=(0.5,0.4,0.5), Distance=6");
        }
        else
        {
            Debug.LogWarning("[FixCamera] CinemachineThirdPersonFollow not found in scene. Open the gameplay scene and run again.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FixCamera] Done.");
    }
}
