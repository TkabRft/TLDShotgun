using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace Shotgun;

// Owns only the bridge between accepted native aim requests and our Animator.
// Native callbacks remain responsible for player state and control-mode changes.
internal static class ShotgunAim
{
    internal enum Direction { None, Raise, Lower }
    private static PlayerAnimation? owner;
    private static GunItem? gun;
    private static Animator? animator;
    private static int shoulderId;
    private static Direction pending;
    private static bool observed;
    private static readonly int RaiseHash = Animator.StringToHash("Base Layer.Aim.Aim_BringUp");
    private static readonly int LowerHash = Animator.StringToHash("Base Layer.Aim.Aim_Lower");
    private static readonly int AimedHash = Animator.StringToHash("Base Layer.Aim.Aim_Idle");
    private static readonly int CloseToAimHash = Animator.StringToHash("Base Layer.Reload.Close.CloseBarrelToAim");
    private static readonly HashSet<int> LoweredHashes = new(new[]
    {
        "Base Layer.Locomotion.Idle.Idle", "Base Layer.Locomotion.Idle.Rare Idle",
        "Base Layer.Locomotion.Walk.Walk", "Base Layer.Locomotion.Walk.Walk>Sprint",
        "Base Layer.Locomotion.Sprint.Sprint"
    }.Select(Animator.StringToHash));

    internal static void Request(PlayerAnimation player, Direction direction)
    {
        var currentAnimator = Main.Instance?.Configuration?.Refs?.ModdedAnimator;
        var currentGun = GunItemInHands();
        var shoulder = player.m_EquippedFirstPersonWeaponShoulder;
        if (currentAnimator == null || !currentAnimator.enabled || currentGun == null
            || currentGun.m_GunType != GunTypeCustom || shoulder == null
            || !shoulder.name.StartsWith(ShotgunFPHPrefabName)) return;

        if (owner != player || gun != currentGun || animator != currentAnimator || shoulderId != shoulder.GetInstanceID())
        {
            Reset("owner changed");
            owner = player; gun = currentGun; animator = currentAnimator; shoulderId = shoulder.GetInstanceID();
        }
        if (pending == direction) return; // Native methods can be called again while their state is still transitional.
        pending = direction;
        observed = false;
        animator.ResetTrigger("Trigger_Generic_Aim");
        animator.ResetTrigger("Trigger_Generic_Aim_Cancel");
        Trace("request " + direction);

        if (direction == Direction.Raise && !animator.IsInTransition(0)
            && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == CloseToAimHash)
        {
            animator.SetBool("AimConfirmed", true);
            Complete(direction, "reload shortcut");
            return;
        }
        animator.SetTrigger(direction == Direction.Raise ? "Trigger_Generic_Aim" : "Trigger_Generic_Aim_Cancel");
        Observe();
    }

    private static bool IsCurrentOwner() => owner != null && animator != null && animator.enabled
        && owner == GameManager.GetPlayerAnimationComponent() && gun != null && gun == GunItemInHands()
        && owner.m_EquippedFirstPersonWeaponShoulder != null
        && owner.m_EquippedFirstPersonWeaponShoulder.GetInstanceID() == shoulderId
        && animator == Main.Instance?.Configuration?.Refs?.ModdedAnimator;

    private static bool ExpectedState(Direction direction) => owner != null && owner.m_State ==
        (direction == Direction.Raise ? PlayerAnimation.State.ToAiming : PlayerAnimation.State.FromAiming);

    internal static void AnimationComplete(AnimationEventProxy proxy, Direction direction)
    {
        if (!IsCurrentOwner() || proxy.gameObject != animator!.gameObject || pending != direction || !ExpectedState(direction))
        {
            Trace("ignored event " + direction);
            return;
        }
        // Events from an old clip can arrive during a blend in the opposite direction.
        int source = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        int target = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).fullPathHash : source;
        int expected = direction == Direction.Raise ? RaiseHash : LowerHash;
        if (source != expected && target != expected)
        {
            Trace("ignored unrelated clip event " + direction);
            return;
        }
        Complete(direction, "animation event");
    }

    internal static void Tick()
    {
        if (owner == null) return;
        if (!IsCurrentOwner()) { Reset("owner unavailable"); return; }
        if (pending == Direction.None) return;
        if (!ExpectedState(pending)) { Interrupt(owner!, "native action changed"); return; }
        Observe();
        if (!observed || animator!.IsInTransition(0)) return;
        int state = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        if (pending == Direction.Raise ? state == AimedHash : LoweredHashes.Contains(state))
            Complete(pending, "recovered missing completion");
    }

    private static void Observe()
    {
        if (animator == null || pending == Direction.None) return;
        int expected = pending == Direction.Raise ? RaiseHash : LowerHash;
        observed |= animator.GetCurrentAnimatorStateInfo(0).fullPathHash == expected
            || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == expected);
    }

    private static void Complete(Direction direction, string reason)
    {
        if (pending != direction || !IsCurrentOwner() || !ExpectedState(direction)) return;
        Trace(reason + " " + direction);
        pending = Direction.None; // Clear before invoking the native callback, which can reenter weapon code.
        observed = false;
        animator!.ResetTrigger("Trigger_Generic_Aim");
        animator.ResetTrigger("Trigger_Generic_Aim_Cancel");
        if (direction == Direction.Raise) owner!.OnAnimationEvent_Generic_Aim_Complete();
        else owner!.OnAnimationEvent_Generic_Aim_Cancel_Complete();
    }

    internal static void Interrupt(PlayerAnimation player, string reason)
    {
        if (owner != player) return;
        Reset(reason);
    }

    internal static void Reset(string reason)
    {
        if (pending != Direction.None) Trace("reset " + reason);
        if (animator != null)
        {
            animator.ResetTrigger("Trigger_Generic_Aim");
            animator.ResetTrigger("Trigger_Generic_Aim_Cancel");
        }
        pending = Direction.None; observed = false;
        owner = null; gun = null; animator = null; shoulderId = 0;
    }

    private static void Trace(string message)
    {
        if (Settings.Instance?.AimDiagnostics != true) return;
        string states = animator == null ? "no animator" :
            $"current={animator.GetCurrentAnimatorStateInfo(0).fullPathHash}@{animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F3} " +
            $"next={animator.GetNextAnimatorStateInfo(0).fullPathHash} blend={animator.IsInTransition(0)}";
        MelonLogger.Msg($"[Shotgun aim] {message}; pending={pending}; native={owner?.m_State}; control={GameManager.GetPlayerManagerComponent()?.m_ControlMode}; {states}");
    }
}
