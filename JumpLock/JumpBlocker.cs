using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace JumpLock;

/// <summary>
/// Owns the game hooks. While <see cref="Active"/> is true, the game is told the jump
/// keybind isn't pressed, so the character simply never starts a jump.
/// Nothing is sent to the server; we only filter local input.
/// </summary>
public sealed unsafe class JumpBlocker : IDisposable
{
    // GeneralAction sheet row for "Jump". If hotbar blocking stops working after a patch,
    // check this row ID in the GeneralAction sheet (e.g. with /xldata).
    private const uint JumpGeneralActionId = 2;

    // Native funcs return a 1-byte bool, so marshal as byte to be safe.
    private delegate byte InputIdDelegate(InputData* self, InputId id);
    private delegate byte UseActionDelegate(
        ActionManager* self, ActionType type, uint actionId, ulong targetId,
        uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted);

    private readonly Configuration config;
    private readonly Hook<InputIdDelegate>? pressedHook;
    private readonly Hook<InputIdDelegate>? downHook;
    private readonly Hook<InputIdDelegate>? heldHook;
    private readonly Hook<UseActionDelegate>? useActionHook;

    /// <summary>Set every frame by the plugin from manual + auto state.</summary>
    public bool Active { get; set; }

    /// <summary>True if the core keybind hooks were installed. Shown in the UI.</summary>
    public bool InputHooksOk => pressedHook != null && downHook != null && heldHook != null;
    public bool ActionHookOk => useActionHook != null;

    public JumpBlocker(Configuration config)
    {
        this.config = config;

        pressedHook = TryHook<InputIdDelegate>("IsInputIdPressed",
            (nint)InputData.MemberFunctionPointers.IsInputIdPressed, PressedDetour);
        downHook = TryHook<InputIdDelegate>("IsInputIdDown",
            (nint)InputData.MemberFunctionPointers.IsInputIdDown, DownDetour);
        heldHook = TryHook<InputIdDelegate>("IsInputIdHeld",
            (nint)InputData.MemberFunctionPointers.IsInputIdHeld, HeldDetour);
        useActionHook = TryHook<UseActionDelegate>("ActionManager.UseAction",
            (nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
    }

    private static Hook<T>? TryHook<T>(string name, nint address, T detour) where T : Delegate
    {
        try
        {
            if (address == 0)
            {
                Plugin.Log.Warning($"[JumpLock] Could not resolve {name}; that part of the block is disabled.");
                return null;
            }

            var hook = Plugin.GameInterop.HookFromAddress(address, detour);
            hook.Enable();
            return hook;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"[JumpLock] Failed to hook {name}.");
            return null;
        }
    }

    private bool IsJumpInput(InputId id)
        => id == InputId.JUMP || (config.BlockGamepadJump && id == InputId.PAD_JUMPANDCANCELCAST);

    private byte PressedDetour(InputData* self, InputId id)
        => Active && IsJumpInput(id) ? (byte)0 : pressedHook!.Original(self, id);

    private byte DownDetour(InputData* self, InputId id)
        => Active && IsJumpInput(id) ? (byte)0 : downHook!.Original(self, id);

    private byte HeldDetour(InputData* self, InputId id)
        => Active && IsJumpInput(id) ? (byte)0 : heldHook!.Original(self, id);

    private byte UseActionDetour(
        ActionManager* self, ActionType type, uint actionId, ulong targetId,
        uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        if (Active && config.BlockHotbarJump && type == ActionType.GeneralAction && actionId == JumpGeneralActionId)
            return 0; // report "not used"

        return useActionHook!.Original(self, type, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    public void Dispose()
    {
        pressedHook?.Dispose();
        downHook?.Dispose();
        heldHook?.Dispose();
        useActionHook?.Dispose();
    }
}
