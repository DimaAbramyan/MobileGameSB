# Color Palette

This document defines the color palettes used for environment assets and the rules for applying them.

Colors should be selected according to the visual depth of an object:

- distant objects are darker, less saturated, and lower in contrast;
- closer background objects may use stronger contrast and clearer color separation;
- background elements must not compete in brightness or saturation with the player, enemies, projectiles, or gameplay effects.

---

## Background 1 — Far Background

The farthest environment layer.

Used for:
- deep space;
- large distant nebulae;
- background clouds;
- empty space between closer parallax layers.

Requirements:
- low saturation;
- low contrast;
- no pure RGB colors;
- brighter colors should mainly appear inside nebulae;
- most of the image should remain within the darker half of the palette.

### Red / Brown Nebula

Primary red-brown palette:

`#261214`
`#301617`
`#391A1A`
`#431E1D`
`#4C2320`
`#552823`
`#5E2D27`
`#67332B`
`#70392F`
`#783F34`
`#80463A`
`#874D40`
`#8E5547`
`#955D4F`
`#9C6658`
`#A36F61`

General direction:

🟥 `#261214` → 🟥 `#552823` → 🟥 `#80463A` → 🟥 `#A36F61`

The lightest colors should not occupy large portions of the image.

---

### Blue / Violet Nebula

Cold alternative to the red background.

`#161225`
`#1C162F`
`#221A39`
`#272043`
`#2C264D`
`#302D57`
`#323661`
`#34406B`
`#374A75`
`#3B557F`
`#416089`
`#486B92`
`#51769B`
`#5B82A4`
`#668EAD`
`#729AB6`

General direction:

🟪 `#161225` → 🟪 `#272043` → 🟦 `#34406B`
→ 🟦 `#486B92` → 🩵 `#729AB6`

Dark areas should lean toward violet.
Midtones should transition into blue.
The brightest areas may shift toward a muted cold cyan-blue.

---

## Parallax 2

Middle-distance background layer.

Used for:
- more defined clouds;
- dust formations;
- small asteroid structures;
- elements where parallax motion should already be noticeable.

This layer should be slightly more contrasted than Background 1.

Palette: TBD.

---

## Parallax 3

Closest background environment layer.

Used for:
- nearby clouds;
- large debris;
- asteroids;
- stronger environment silhouettes.

Shapes may be more readable than in Parallax 2, but this layer must still remain visually subordinate to gameplay objects.

Palette: TBD.

---

## Planets

Used for:
- planets;
- moons;
- large spherical celestial objects;
- distant astronomical bodies.

Planets may use their own local palettes, but they should remain visually related to the level environment.

Palette: TBD.

---

## Megastructures / Stations

Used for:
- space stations;
- abandoned structures;
- massive ships;
- artificial background objects.

Main surfaces should remain darker than gameplay objects.

Local light sources are allowed, but they should stay muted.

Palette: TBD.

---

## Stars / Space Dust

Shared palette for small background particles.

Used independently of the main level color theme.

Colors should remain low-saturation.
Pure white should be used very rarely.

Palette: TBD.

---

## Hot Debris / Distant Lights

Used for:
- distant hot fragments;
- sparks;
- isolated lights;
- small heat sources.

General direction:

🟥 → 🟧 → 🟨

Example colors:

`#5A2A1F`
`#73402A`
`#925033`
`#B85C2B`
`#C5743E`
`#D98736`
`#DF9850`
`#E8B85A`
`#EBC072`
`#F2D98A`

Yellow tones should be used very sparingly and only for the hottest points.

---

## General Rules

1. Background palettes should avoid pure colors such as `#FF0000`, `#00FF00`, `#0000FF`, `#FFFF00`, and other fully saturated values.

2. The farther an object is from the player, the lower its:
   - contrast;
   - saturation;
   - brightness range.

3. Brightness should not be used as the main way to indicate depth.
   Closer background layers should primarily become more readable through shape and separation, not simply through higher brightness.

4. The brightest colors in each palette are accent colors and should not occupy a large portion of the image.

5. Gameplay objects always have priority over environment elements in terms of readability.

6. New palettes should be defined in this document before being used for large-scale asset generation.