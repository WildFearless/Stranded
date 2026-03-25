# Mini Turret Notes

This documents the turret feature added for `Stranded`, without depending on the current player code.

## Files

- `code/Weapons/MiniTurret.cs`

## Intended Scene Hierarchy

```text
TurretRoot
├─ BaseBlock
└─ YawPivot
   └─ CannonBlock
      └─ Muzzle
```

Important:

- `BaseBlock` should never be assigned as `YawPivot`.
- `YawPivot` is the middle rotating block.
- `CannonBlock` is the side cannon/barrel object.
- `Muzzle` is a small empty object at the barrel tip.

## MiniTurret Properties

- `YawPivot`: object that rotates left/right
- `Cannon`: object that handles the final aim direction
- `Muzzle`: optional beam/fire origin
- `TargetTag`: tag used to validate targets, currently `npc`
- `Range`: max search and fire distance
- `YawTurnSpeed`: horizontal turning speed
- `CannonTurnSpeed`: cannon aim speed
- `FireRate`: shots per second behavior
- `Damage`: damage per hit
- `TargetAimHeight`: vertical aim offset on targets
- `BeamParticle`: current tracer beam particle path
- `BloodImpactPrefab`: prefab-based impact effect for NPC hits
- `FireSound`: shot sound
- `IdleSweepAngle`: idle scan width
- `IdleSweepSpeed`: idle scan speed
- `SearchBeamColor`: debug search beam color

## How It Works

- While idle, the turret sweeps left/right using the `YawPivot`.
- During idle, it performs a forward trace from the muzzle/cannon direction.
- If that search trace hits a valid NPC hierarchy, it becomes the target.
- Once a target is locked, the yaw pivot rotates horizontally and the cannon rotates to the final 3D aim.
- The turret checks line of sight before firing.
- When firing, it traces again for the actual impact point and damage.

## Important Implementation Decisions

- Target acquisition is trace-based, not scene-wide scanning.
- Idle sweep uses local rotations captured on start so the turret returns to its authored pose.
- The base object is never meant to rotate in code.
- NPC resolution walks up from the hit object to find `WanderersNpc`, because traces often hit child collider objects.

## Known Visual Behavior

- The firing beam is allowed to visually extend to the turret aim point when hitting an NPC so it does not look short against the target collider.
- The search beam is currently a debug line, not a prefab or particle beam.
- The blood effect is now prefab-based instead of string-path particle spawning.

## Reuse Notes

If this turret is reused in another project:

- keep the same hierarchy pattern
- keep target acquisition trace-based
- prefer prefab-based hit effects
- treat the turret as a world prop, not an equippable weapon
