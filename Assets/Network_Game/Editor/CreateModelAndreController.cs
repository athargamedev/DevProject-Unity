using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// Builds the modelAndre AnimatorController from scratch.
/// Menu: Tools/Andre/Build Animation Controller
/// </summary>
public static class CreateModelAndreController
{
    private const string ControllerPath =
        "Assets/Network_Game/ThirdPersonController/Character/Animations/Mixamo/modelAndre_Controller.controller";

    private const string MaskPath =
        "Assets/Network_Game/ThirdPersonController/Character/Animations/Mixamo/UpperBody_Mask.mask";

    private const string ClipRoot =
        "Assets/Network_Game/ThirdPersonController/Character/Animations/Mixamo/modelAndre/";

    // Speed thresholds that exactly match ThirdPersonController
    private const float SpeedIdle   = 0.0f;
    private const float SpeedWalk   = 2.0f;
    private const float SpeedSprint = 5.335f;

    [MenuItem("Tools/Andre/Build Animation Controller")]
    public static void Build()
    {
        // ── Create controller asset ────────────────────────────────────
        if (File.Exists(Application.dataPath + ControllerPath.Substring("Assets".Length)))
            AssetDatabase.DeleteAsset(ControllerPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ── Parameters (must match Animator.StringToHash calls in ThirdPersonController) ──
        ctrl.AddParameter("Speed",        AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MotionSpeed",  AnimatorControllerParameterType.Float);
        ctrl.AddParameter("InputX",       AnimatorControllerParameterType.Float);
        ctrl.AddParameter("InputY",       AnimatorControllerParameterType.Float);
        ctrl.AddParameter("TurnDelta",    AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Grounded",     AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Jump",         AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("FreeFall",     AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("HardLanding",  AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Attack",       AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Emote",        AnimatorControllerParameterType.Trigger);

        // ── Layer 0: Base (full body) ──────────────────────────────────
        var baseSM = ctrl.layers[0].stateMachine;
        baseSM.name = "Base Layer";
        BuildBaseLayer(ctrl, baseSM);

        // ── Layer 1: Upper Body override ──────────────────────────────
        var upperBodyMask = BuildUpperBodyMask();
        ctrl.AddLayer("Upper Body");

        // Struct-level properties need the copy-write pattern
        var upperSM = ctrl.layers[1].stateMachine;
        var layers   = ctrl.layers;
        layers[1].defaultWeight  = 1f;
        layers[1].blendingMode   = AnimatorLayerBlendingMode.Override;
        layers[1].avatarMask     = upperBodyMask;
        ctrl.layers = layers;

        BuildUpperBodyLayer(ctrl, upperSM);

        // ── Finalize ───────────────────────────────────────────────────
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Andre] AnimatorController built → {ControllerPath}");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    // ─────────────────────────────────────────────────────────────────
    // LAYER 0 — BASE
    // ─────────────────────────────────────────────────────────────────

    static void BuildBaseLayer(AnimatorController ctrl, AnimatorStateMachine sm)
    {
        sm.entryPosition   = new Vector3(-200,  20);
        sm.anyStatePosition = new Vector3(-200, 120);
        sm.exitPosition    = new Vector3(-200, 220);

        // ── Locomotion blend tree ──────────────────────────────────────
        AnimatorState locomotion = sm.AddState("Locomotion", new Vector3(100, 20));
        locomotion.speedParameterActive = true;
        locomotion.speedParameter = "MotionSpeed";

        var blendTree = new BlendTree
        {
            name              = "Locomotion",
            blendType         = BlendTreeType.Simple1D,
            blendParameter    = "Speed",
            useAutomaticThresholds = false
        };
        blendTree.AddChild(Clip("modelAndre@Standing Idle 03.fbx"), SpeedIdle);
        blendTree.AddChild(Clip("modelAndre@Walk.fbx"),             SpeedWalk);
        blendTree.AddChild(Clip("modelAndre@Run.fbx"),              SpeedSprint);
        AssetDatabase.AddObjectToAsset(blendTree, ctrl);
        locomotion.motion = blendTree;
        sm.defaultState = locomotion;

        // ── Jump ───────────────────────────────────────────────────────
        AnimatorState jump = sm.AddState("Jump", new Vector3(400, -80));
        jump.motion = Clip("modelAndre@Jump.fbx");

        // ── FreeFall ───────────────────────────────────────────────────
        AnimatorState freeFall = sm.AddState("FreeFall", new Vector3(400, 20));
        freeFall.motion = Clip("modelAndre@FallingIdle.fbx");

        // ── JumpLand ───────────────────────────────────────────────────
        AnimatorState jumpLand = sm.AddState("JumpLand", new Vector3(400, 120));
        jumpLand.motion = Clip("modelAndre@JumpLand.fbx");

        // ── HardLanding ────────────────────────────────────────────────
        AnimatorState hardLanding = sm.AddState("HardLanding", new Vector3(400, 220));
        hardLanding.motion = Clip("modelAndre@HardLanding.fbx");

        // ─────────────────────────────────────────────────────────────
        // TRANSITIONS — Layer 0
        // ─────────────────────────────────────────────────────────────

        // Locomotion → Jump  (Jump bool set to true on Space press)
        Transition(locomotion, jump, 0.1f)
            .AddCondition(AnimatorConditionMode.If, 0, "Jump");

        // Jump → FreeFall  (FreeFall set true after FallTimeout 0.15 s)
        Transition(jump, freeFall, 0.1f)
            .AddCondition(AnimatorConditionMode.If, 0, "FreeFall");

        // Jump → JumpLand  (quick landing before FreeFall kicks in)
        var qland = jump.AddTransition(jumpLand);
        qland.hasExitTime = false;
        qland.duration = 0.15f;
        qland.AddCondition(AnimatorConditionMode.If,    0, "Grounded");
        qland.AddCondition(AnimatorConditionMode.IfNot, 0, "HardLanding");

        // FreeFall → JumpLand  (normal landing)
        var fl = freeFall.AddTransition(jumpLand);
        fl.hasExitTime = false;
        fl.duration = 0.2f;
        fl.AddCondition(AnimatorConditionMode.If,    0, "Grounded");
        fl.AddCondition(AnimatorConditionMode.IfNot, 0, "HardLanding");

        // FreeFall → HardLanding  (heavy landing)
        var fhl = freeFall.AddTransition(hardLanding);
        fhl.hasExitTime = false;
        fhl.duration = 0.1f;
        fhl.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        fhl.AddCondition(AnimatorConditionMode.If, 0, "HardLanding");

        // JumpLand → Locomotion  (clip completes)
        var jll = jumpLand.AddTransition(locomotion);
        jll.hasExitTime = true;
        jll.exitTime    = 0.9f;
        jll.duration    = 0.15f;

        // JumpLand → HardLanding  (landing evaluated as hard after clip start)
        Transition(jumpLand, hardLanding, 0.1f)
            .AddCondition(AnimatorConditionMode.If, 0, "HardLanding");

        // HardLanding → Locomotion  (bool auto-resets after HardLandingDuration)
        Transition(hardLanding, locomotion, 0.2f)
            .AddCondition(AnimatorConditionMode.IfNot, 0, "HardLanding");

        // Locomotion → HardLanding  (edge case: landed from shallow fall)
        Transition(locomotion, hardLanding, 0.1f)
            .AddCondition(AnimatorConditionMode.If, 0, "HardLanding");
    }

    // ─────────────────────────────────────────────────────────────────
    // LAYER 1 — UPPER BODY
    // ─────────────────────────────────────────────────────────────────

    static void BuildUpperBodyLayer(AnimatorController ctrl, AnimatorStateMachine sm)
    {
        sm.entryPosition   = new Vector3(-200,  20);
        sm.anyStatePosition = new Vector3(-200, 120);
        sm.exitPosition    = new Vector3(-200, 220);

        // Empty pass-through (no motion = layer has zero influence on lower body via mask)
        AnimatorState empty = sm.AddState("Empty", new Vector3(100, 20));
        empty.motion = null;
        sm.defaultState = empty;

        // Punch
        AnimatorState punch = sm.AddState("Punch", new Vector3(350, -60));
        punch.motion = Clip("modelAndre@Punch.fbx");

        // Kick
        AnimatorState kick = sm.AddState("Kick", new Vector3(350, 20));
        kick.motion = Clip("modelAndre@Kick.fbx");

        // Emote  (Wave — expandable via Emote index param later)
        AnimatorState emote = sm.AddState("Emote", new Vector3(350, 100));
        emote.motion = Clip("modelAndre@Wave.fbx");

        // ── Transitions ───────────────────────────────────────────────

        // Empty → Punch
        Transition(empty, punch, 0.1f)
            .AddCondition(AnimatorConditionMode.If, 0, "Attack");

        // Punch → Empty (on finish)
        var pe = punch.AddTransition(empty);
        pe.hasExitTime = true;
        pe.exitTime    = 0.85f;
        pe.duration    = 0.1f;

        // Punch → Kick (combo: Attack again before Punch ends)
        Transition(punch, kick, 0.15f)
            .AddCondition(AnimatorConditionMode.If, 0, "Attack");

        // Kick → Empty
        var ke = kick.AddTransition(empty);
        ke.hasExitTime = true;
        ke.exitTime    = 0.85f;
        ke.duration    = 0.1f;

        // Empty → Emote
        Transition(empty, emote, 0.2f)
            .AddCondition(AnimatorConditionMode.If, 0, "Emote");

        // Emote → Empty (on finish)
        var ee = emote.AddTransition(empty);
        ee.hasExitTime = true;
        ee.exitTime    = 0.9f;
        ee.duration    = 0.25f;
    }

    // ─────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Loads the first non-preview AnimationClip from an FBX sub-asset.</summary>
    static AnimationClip Clip(string fileName)
    {
        string path = ClipRoot + fileName;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is AnimationClip c && !c.name.StartsWith("__"))
                return c;

        Debug.LogWarning($"[Andre] Clip not found in: {path}");
        return null;
    }

    /// <summary>Adds an instant (no exit time) transition between two states.</summary>
    static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float blendDuration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = blendDuration;
        return t;
    }

    /// <summary>Creates and saves the upper-body AvatarMask.</summary>
    static AvatarMask BuildUpperBodyMask()
    {
        var mask = new AvatarMask { name = "UpperBody_Mask" };

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root,         false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body,         true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head,         true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg,      false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg,     false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm,      true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,     true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,  true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK,   false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK,  false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK,   false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK,  false);

        if (File.Exists(Application.dataPath + MaskPath.Substring("Assets".Length)))
            AssetDatabase.DeleteAsset(MaskPath);
        AssetDatabase.CreateAsset(mask, MaskPath);
        return mask;
    }
}
