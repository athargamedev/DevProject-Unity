using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Rebuilds modelAndre_Animator.controller from scratch with:
///   - 1D Speed locomotion blend tree (Idle / Walk / Run / Sprint)
///   - Air states  (Jump / Falling / Land)
///   - Action states (Attack / Death / Emote)
///   - All transitions wired to parameters
///
/// Run via:  Tools → Network Game → Rebuild modelAndre Animator
/// Safe to re-run: asset GUID is preserved so scene/prefab references stay intact.
/// </summary>
public static class ModelAndreAnimatorBuilder
{
    private const string ControllerPath =
        "Assets/Network_Game/ThirdPersonController/Character/Animations/Mixamo/modelAndre/modelAndre_Animator.controller";

    private const string ClipFolder =
        "Assets/Network_Game/ThirdPersonController/Character/Animations/Mixamo/modelAndre/";

    [MenuItem("Tools/Network Game/Rebuild modelAndre Animator")]
    public static void Build()
    {
        // ── Load or create controller (keeps GUID on re-runs) ─────────
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ── Repair null state machine (leftover from a previous bad run) ─
        // AnimatorControllerLayer is a struct, so we must read the array,
        // mutate the struct, then write the whole array back.
        var layers = ctrl.layers;
        if (layers[0].stateMachine == null)
        {
            var repairSm = new AnimatorStateMachine { name = "Base Layer" };
            repairSm.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(repairSm, ctrl);
            layers[0].stateMachine = repairSm;
            ctrl.layers = layers;           // write-back required for struct array
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }

        // ── Remove stale BlendTree sub-assets from any previous run ───
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
                     .Where(o => o is BlendTree).ToArray())
            AssetDatabase.RemoveObjectFromAsset(sub);

        // ── Parameters ────────────────────────────────────────────────
        while (ctrl.parameters.Length > 0)
            ctrl.RemoveParameter(0);

        ctrl.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Jump",       AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Attack",     AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Death",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hit",        AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Emote",      AnimatorControllerParameterType.Trigger);

        // ── Clear Base Layer state machine ────────────────────────────
        var sm = ctrl.layers[0].stateMachine;
        foreach (var s in sm.states.ToArray())
            sm.RemoveState(s.state);
        foreach (var t in sm.anyStateTransitions.ToArray())
            sm.RemoveAnyStateTransition(t);

        // ── Locomotion blend tree (1D on Speed) ───────────────────────
        // Thresholds match ThirdPersonController defaults:
        //   0.0 = idle,  2.0 = MoveSpeed,  4.0 = run,  5.5 ≈ SprintSpeed
        BlendTree tree;
        var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out tree, 0);
        tree.blendType              = BlendTreeType.Simple1D;
        tree.blendParameter         = "Speed";
        tree.useAutomaticThresholds = false;
        AddClipChild(tree, "modelAndre@Idle.fbx",   0f);
        AddClipChild(tree, "modelAndre@Walk.fbx",   2f);
        AddClipChild(tree, "modelAndre@Run.fbx",    4f);
        AddClipChild(tree, "modelAndre@Sprint.fbx", 5.5f);

        // Re-fetch sm — CreateBlendTreeInController may have internally refreshed the layer
        sm = ctrl.layers[0].stateMachine;
        sm.defaultState = locomotion;

        // ── Air states ────────────────────────────────────────────────
        var jumpState    = MakeState(sm, "Jump",    "modelAndre@Jump.fbx",        new Vector3(350, -80));
        var fallingState = MakeState(sm, "Falling", "modelAndre@FallingIdle.fbx", new Vector3(350,  40));
        var landState    = MakeState(sm, "Land",    "modelAndre@JumpLand.fbx",    new Vector3(350, 160));

        // ── Action states ─────────────────────────────────────────────
        var attackState = MakeState(sm, "Attack", "modelAndre@Punch.fbx", new Vector3(600, -80));
        var deathState  = MakeState(sm, "Death",  "modelAndre@Death.fbx", new Vector3(600,  40));
        var emoteState  = MakeState(sm, "Emote",  "modelAndre@Wave.fbx",  new Vector3(600, 160));

        // ── Transitions ───────────────────────────────────────────────

        // AnyState → Jump  (Jump trigger, won't re-enter itself mid-air)
        var anyJump = sm.AddAnyStateTransition(jumpState);
        Configure(anyJump, hasExit: false, exit: 0f, dur: 0.1f, selfOk: false);
        anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        // Locomotion → Falling  (walked off edge)
        var locoFall = locomotion.AddTransition(fallingState);
        Configure(locoFall, hasExit: false, exit: 0f, dur: 0.1f);
        locoFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");

        // Jump → Falling  (still airborne after apex)
        Configure(jumpState.AddTransition(fallingState), hasExit: true, exit: 0.75f, dur: 0.1f);

        // Jump → Land  (landed before apex)
        var jumpLand = jumpState.AddTransition(landState);
        Configure(jumpLand, hasExit: false, exit: 0f, dur: 0.1f);
        jumpLand.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // Falling → Land
        var fallLand = fallingState.AddTransition(landState);
        Configure(fallLand, hasExit: false, exit: 0f, dur: 0.1f);
        fallLand.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // Land → Locomotion
        Configure(landState.AddTransition(locomotion), hasExit: true, exit: 0.9f, dur: 0.1f);

        // AnyState → Attack
        var anyAttack = sm.AddAnyStateTransition(attackState);
        Configure(anyAttack, hasExit: false, exit: 0f, dur: 0.1f, selfOk: false);
        anyAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        // Attack → Locomotion
        Configure(attackState.AddTransition(locomotion), hasExit: true, exit: 0.85f, dur: 0.15f);

        // AnyState → Death  (terminal state)
        var anyDeath = sm.AddAnyStateTransition(deathState);
        Configure(anyDeath, hasExit: false, exit: 0f, dur: 0.1f, selfOk: false);
        anyDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");

        // AnyState → Emote
        var anyEmote = sm.AddAnyStateTransition(emoteState);
        Configure(anyEmote, hasExit: false, exit: 0f, dur: 0.1f, selfOk: false);
        anyEmote.AddCondition(AnimatorConditionMode.If, 0, "Emote");

        // Emote → Locomotion
        Configure(emoteState.AddTransition(locomotion), hasExit: true, exit: 0.85f, dur: 0.15f);

        // ── Save ──────────────────────────────────────────────────────
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int stateCount = ctrl.layers[0].stateMachine.states.Length;
        Debug.Log($"[ModelAndreAnimatorBuilder] Controller rebuilt. " +
                  $"States: {stateCount} | Params: Speed, IsGrounded, Jump, Attack, Death, Hit, Emote\n" +
                  $"Locomotion blend tree: Idle(0) / Walk(2) / Run(4) / Sprint(5.5)");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static AnimatorState MakeState(AnimatorStateMachine sm, string name, string fbxFile, Vector3 pos)
    {
        var state = sm.AddState(name, pos);
        state.motion = LoadClip(fbxFile);
        return state;
    }

    private static void Configure(AnimatorStateTransition t,
        bool hasExit, float exit, float dur, bool selfOk = true)
    {
        t.hasExitTime          = hasExit;
        t.exitTime             = exit;
        t.duration             = dur;
        t.canTransitionToSelf  = selfOk;
    }

    private static void AddClipChild(BlendTree tree, string fbxFile, float threshold)
    {
        var clip = LoadClip(fbxFile);
        if (clip != null)
            tree.AddChild(clip, threshold);
        else
            Debug.LogWarning($"[ModelAndreAnimatorBuilder] Clip not found: {fbxFile}");
    }

    private static AnimationClip LoadClip(string fbxFile)
    {
        string path = ClipFolder + fbxFile;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        Debug.LogWarning($"[ModelAndreAnimatorBuilder] No clip found in: {path}");
        return null;
    }
}
