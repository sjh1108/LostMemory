# CL-010 Attack Chain Input Buffer

Created: 2026-04-22

Target scene: `LostMemory/Assets/Scenes/test_khi.unity`

## Goal

CL-010 makes combo chaining depend on an explicit, short-lived input buffer.

The CL-009 controller accepted any attack input during an attack and stored it
as `_queuedAttack`. That made very early repeated clicks guarantee the next
combo step as soon as recovery ended.

## Runtime Rules

- Attack input immediately after attack start is ignored.
- Chain input opens after both conditions are true:
  - `minimumChainInputDelay` has elapsed from attack start.
  - The attack has reached at least the second half of its active duration.
- While attacking, accepted chain input stores a buffer until
  `Time.time + inputBufferDuration`.
- At recovery end, the next combo step starts only when the buffer is still
  valid.
- After recovery ends, direct input inside `comboInputWindow` still starts the
  next combo step.
- If `comboInputWindow` expires, the next direct attack resets to step 1.
- Step 3 does not buffer into step 1. A new step 1 requires a direct input after
  step 3 has ended.

## Initial Tuning

```text
minimumChainInputDelay = 0.12
inputBufferDuration = 0.25
comboInputWindow = 0.6
```

## Implementation Notes

Main file:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs
```

State added:

```text
_chainInputAllowedAt
_bufferedAttackExpiresAt
```

The old `_queuedAttack` boolean was replaced by an expiry timestamp so buffered
input can become invalid before recovery ends.

Damage, hitbox shape, runtime preview, and slash visual behavior were left
unchanged.

## Current Work Log

Date: 2026-04-22

Branch:

```text
feat/S14P31C201-201/cl-공격-연계-입력-버퍼-구현
```

Changed file:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs
```

What changed:

- Replaced the old `_queuedAttack` boolean with timestamp-based buffering.
- Added serialized tuning fields:
  - `minimumChainInputDelay = 0.12f`
  - `inputBufferDuration = 0.25f`
- Kept `comboInputWindow = 0.6f`, now with `Min(0f)`.
- Added runtime state:
  - `_currentComboStep`
  - `_chainInputAllowedAt`
  - `_bufferedAttackExpiresAt`
- `RequestAttack()` now calls `TryBufferAttack()` while an attack is already
  running.
- `RunAttack()` clears stale buffered input when a new attack starts.
- Chain input opens at:

```text
max(
  attackStartedAt + minimumChainInputDelay,
  attackStartedAt + startupDuration + activeDuration * 0.5
)
```

- Valid buffered input is consumed only at recovery end.
- If the buffer expired before recovery end, no next attack starts
  automatically.
- Step 3 ignores chain buffering so it cannot auto-loop into step 1.
- After recovery, direct input inside `comboInputWindow` still chains normally.
- If `comboInputWindow` expires, the next direct input starts from step 1.

Verification performed:

```text
git diff --check -- LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs docs/khi/cl010_attack_chain_input_buffer.md
```

Result:

```text
passed
```

Build check attempted:

```text
dotnet build LostMemory/Assembly-CSharp.csproj --no-restore
```

Result:

```text
timed out after 124 seconds
```

The timed-out `dotnet` worker processes were stopped afterward. No generated
build files were left in git status from that attempt.

Existing unrelated or prior CL-009 working tree changes were left untouched:

```text
M  LostMemory/Assets/InputSystem_Actions.inputactions
D  LostMemory/Assets/TestKhi/TestKhiSceneBootstrap.cs
D  LostMemory/Assets/TestKhi/TestKhiSceneBootstrap.cs.meta
M  LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAttackVisualPresenter.cs
?? docs/khi/cl009_sword_combo_implementation_plan.md
```

Manual checks in Unity (Completed):

- [x] Start attack 1 and click immediately during startup: should not chain.
- [x] Click during late active or recovery: should buffer and start attack 2 at
  recovery end if the buffer is still valid.
- [x] Click too early once, then do not click again: should not chain.
- [x] Click after recovery but before `comboInputWindow` expires: should start the
  next combo step directly.
- [x] Wait longer than `comboInputWindow`, then click: should restart at step 1.
- [x] During attack 3, click repeatedly: should not auto-start step 1 after
  recovery.

Additional adjustments made:
- Increased the `StartupDuration`, `ActiveDuration`, and `RecoveryDuration` of all 3 attack steps by roughly 1.5x - 2.0x to make the attack tempo more deliberate and to improve the feel of the input buffer window.
