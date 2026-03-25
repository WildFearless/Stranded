# Viewmodel Notes

This documents the viewmodel-side ideas used here, without relying on the current `PlayerActions` implementation.

## First-Person Weapon Setup

The ready-to-use s&box first-person weapons are viewmodels, not world models.

General layout:

```text
Player
└─ Camera
   └─ ViewModelRoot
      ├─ WeaponViewModel
      └─ ArmsRenderer
```

Rules:

- use the `v_` model for first-person animation
- do not use the `w_` model for viewmodel testing
- if using arms, the arms bone-merge onto the weapon renderer
- the weapon renderer is the main animated model

## Important Asset Distinction

- `v_*` = first-person animated viewmodel
- `w_*` = world/third-person model

This mattered for the M4 setup:

- `w_m4a1` rendered but was not the right asset for first-person animated behavior
- the correct testing path was the viewmodel asset

## Muzzle Flash Direction

Using a prefab for muzzle flash was more reliable than a raw particle string path.

Reason:

- a prefab can already contain the correct `ParticleEffect` setup
- it is easier to place and iterate in the editor
- it avoids older scene-particle constructor patterns

## Camera Recoil Idea

The general recoil approach that worked here was:

- keep the weapon/viewmodel recoil animation on the gun
- add a separate camera recoil offset on top of the real camera
- recover that offset over time

That means weapon recoil and camera recoil are separate layers.

## Reuse Notes

If rebuilding from scratch in another project:

- keep viewmodel logic separate from world weapon logic
- prefer prefab-based effects for muzzle flash and impact effects
- keep camera recoil as its own layer, not baked into the weapon animation only
