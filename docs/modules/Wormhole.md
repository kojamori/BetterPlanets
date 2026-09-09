# Making a Wormhole

First off, a wormhole in this mod is a type of planet that teleports you to another wormhole.

There are two methods of entering a wormhole you can choose between when creating one.

1. Periapsis entry
2. Enclosure entry

## Periapsis Entry
A rocket is teleported to the other wormhole WHEN:
1. they are in an orbit
2. they reach their periapsis
3. their periapsis height is below the "Entry Height".

e.g.

```json
"CUSTOM_DATA": 
{
  "WORMHOLE_DATA": 
  {
    "isWormhole": true,
    "targetBodyCodeName": "SomeOtherWormhole",
    "entryType": "Periapsis",
    "entryHeight": 3000.0
  }
}
```

## Enclosure Entry
A rocket is teleported to the other wormhole WHEN:
1. the entire rocket is below the "Entry Height" and fully enclosed within the radius of the entry height (which is relative to the radius of the wormhole).

You can set the entry height to 0 so that you have to physically be inside the wormhole.

You can also make it higher than 0 (e.g. 100), so that your rocket has to be below a height of at least 100m above the wormhole.
You can also make it lower than 0 (e.g. -100), so that your rocket has to be 100m deep inside the wormhole.

e.g.

```json
"CUSTOM_DATA": 
{
  "WORMHOLE_DATA": 
  {
    "isWormhole": true,
    "targetBodyCodeName": "SomeOtherWormhole",
    "entryType": "Enclosure",
    "entryHeight": 0.0
  }
}
```

## How Teleportation Works

When a rocket enters a wormhole, it is teleported to the target wormhole using a **chord model**. Imagine drawing a straight line (chord) from where the rocket enters, through the center of the wormhole, and out the other side. The rocket exits the target wormhole at the point where that chord would emerge.

- If you fly **straight down** into a wormhole (head-on), you exit the target wormhole on the **exact opposite side** (180° away from your entry angle).
- If you fly in at a **shallow angle** (grazing), you exit near the **same angular position** you entered, since the chord barely crosses the center.
- If you fly in at an angle **somewhere in between**, the exit point shifts proportionally.

Your **speed is preserved** through the wormhole. Your **velocity direction is also preserved** in the global frame, you keep moving the same way you were before. This means your trajectory on the other side depends on how you entered.

## Properties Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `isWormhole` | bool | `false` | Whether this planet acts as a wormhole. Must be `true` for any wormhole behavior. |
| `shouldTeleport` | bool | `true` | Whether this wormhole actively teleports rockets. Set to `false` to make a **receive-only** wormhole (other wormholes can target it, but it won't send rockets anywhere). Useful for one-way wormholes. |
| `targetBodyCodeName` | string | `null` | The `codeName` of the destination wormhole planet. The target must also have `WORMHOLE_DATA` with `isWormhole: true`, or this wormhole will disable itself. |
| `entryType` | string | `"Enclosure"` | The entry method. Either `"Periapsis"` or `"Enclosure"`. |
| `entryHeight` | float | `30000.0` | The height threshold for triggering the wormhole, in meters. Meaning depends on entry type (see above). |

## One-Way Wormholes

By default, wormholes are **two-way**, if Wormhole A targets Wormhole B, and Wormhole B targets Wormhole A, rockets can travel in both directions.

To create a **one-way** wormhole, set `shouldTeleport` to `false` on the destination wormhole. It will still act as a valid target, but won't teleport any rockets that enter it.

e.g. Wormhole A (sends rockets to B):
```json
"CUSTOM_DATA": 
{
  "WORMHOLE_DATA": 
  {
    "isWormhole": true,
    "targetBodyCodeName": "WormholeB",
    "entryType": "Enclosure",
    "entryHeight": 0.0
  }
}
```

Wormhole B (receive-only, does not teleport):
```json
"CUSTOM_DATA": 
{
  "WORMHOLE_DATA": 
  {
    "isWormhole": true,
    "shouldTeleport": false
  }
}
```

## Wormhole Networks

You can create wormhole chains or networks by having multiple wormholes target different destinations. For example:

- **A → B → C → A** creates a one-way loop.
- **A ↔ B** and **C ↔ D** creates two independent pairs.
- **A → B**, **A → C** is **not possible**, each wormhole can only have one target. But B and C can both target A.

## Notes

- There is a **5-second cooldown** after each teleport to prevent rapid ping-ponging between wormholes.
- After teleporting, the rocket must **leave the entry zone** of the destination wormhole before it can be teleported again by that wormhole. This provides an additional layer of protection against accidental re-entry.
- If timewarp is active, it will be **automatically stopped** before the teleport occurs.
- If the teleported rocket is the **player's active rocket**, a **screen fade** effect plays during the teleport for a smooth visual transition.
- The target wormhole **must** have valid `WORMHOLE_DATA` with `isWormhole: true`. If the target planet doesn't exist or isn't configured as a wormhole, a warning will be logged and teleportation will be disabled for that wormhole.