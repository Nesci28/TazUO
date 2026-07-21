---
title: ApiCombatDpsSnapshot
description: Observed combat damage snapshot for one target.
---

## Class Description
Observed combat damage snapshot for one target. `Total` contains all damage shown by the server.
`Mine` and `Others` contain whole hits attributed from matching combat events. `Unknown` contains
damage for which the protocol did not expose enough source information.

DPS is encounter damage divided by the observed interval from the first hit to the latest hit.
For a single hit, whose observed interval is zero, a one-second denominator is used.
`AttributionCoverage` is the attributed share of `TotalDamage`, from `0.0` to `1.0`.

## Properties
### `TargetSerial`

**Type:** `uint`

### `MineDps`

**Type:** `double`

### `OthersDps`

**Type:** `double`

### `UnknownDps`

**Type:** `double`

### `TotalDps`

**Type:** `double`

### `MineDamage`

**Type:** `long`

### `OthersDamage`

**Type:** `long`

### `UnknownDamage`

**Type:** `long`

### `TotalDamage`

**Type:** `long`

### `HitCount`

**Type:** `int`

### `ElapsedSeconds`

**Type:** `double`

### `AttributionCoverage`

**Type:** `double`

### `HasData`

**Type:** `bool`

## Fields
*No fields found.*

## Enums
*No enums found.*

## Methods
*No methods found.*
