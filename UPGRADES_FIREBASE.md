# ME Upgrades integration

The UPGRADES page is a runtime Unity UI built for the existing 1080 × 1920 design surface. The parent `AspectRatioFitter`, `CanvasScaler`, and safe-area root keep the same layout intact on narrow phones, tall phones, tablets, and desktop Game views.

## Realtime Database setup

1. In the Firebase project `gravity-half-dead`, create a Realtime Database.
2. The client currently uses `https://gravity-half-dead-default-rtdb.firebaseio.com` in `GravityHalfDeadApp.Firebase.cs`. If Firebase creates the database in a regional host such as `europe-west1.firebasedatabase.app`, replace that one URL.
3. Deploy the owner-only validation rules with `firebase deploy --only database` when you are ready. The rules file is `database.rules.json` and is registered by `firebase.json`.

The Unity package `com.google.firebase.database` 13.15.0 is stored beside the existing Firebase packages in `GooglePackages` and referenced by `Packages/manifest.json`.

## Player schema

```text
players/{firebaseUid}
  playerId: "GHD-..."
  coins: 25000
  upgrades/{upgradeId}
    name: "SHIELD"
    level: 0..6
    updatedAt: server timestamp
  collectibles/{upgradeId}
    totalCollected: 42
    lastCollectedAt: server timestamp
  characters/{characterId}/upgrades/{upgradeId}/level  # reserved for future overrides
  updatedAt: server timestamp
```

The player root has one live `ValueChanged` listener. Purchases use a transaction on that root, so the coin deduction and level increase are atomic and immediately appear on every signed-in session. The first connection seeds Realtime Database from the current Firestore bootstrap state; successful purchases are also mirrored back to the existing Firestore fields for compatibility.

## Gameplay collectible connection

- Call `RecordPowerupCollected("shield")` (or another upgrade id) when a collectible is acquired during a run. It increments the lifetime collectible counter without changing the purchased level.
- Call `GetPowerupDurationSeconds(powerupId, baseDurationSeconds)` when activating an in-run power-up. It applies Timezone's `+10%` duration per purchased Timezone level.
- The supported ids are `shield`, `speed_boost`, `invulnerability`, `magnet`, `wall_walk`, and `timezone`.

The five supplied images are loaded from `Resources/UI/Powerups/{upgradeId}`. Timezone also looks for `Resources/UI/Powerups/timezone`; until that asset is added, the UI renders a procedural golden clock fallback.
