# CHARACTERS screen integration

The runtime-built Unity Canvas screen is implemented by:

- `Assets/Scripts/App/GravityHalfDeadApp.CharactersUI.cs` — responsive 1080×1920 reference layout, 3×4 roster, featured character, selection and lock states.
- `Assets/Scripts/App/GravityHalfDeadApp.Characters.cs` — preview, selection, UI refresh, and unlock button flow.
- `Assets/Scripts/App/GravityHalfDeadApp.CharactersRealtime.cs` — Realtime Database initialization, listeners, atomic purchases, and the legacy Firestore mirror.

## Database schema

Each authenticated player owns one Realtime Database node:

```text
players/{uid}
  playerId: "GHD-ABC123"
  coins: 75000
  selectedCharacter: "jax"
  characters
    nova
      name: "NOVA"
      unlocked: true
      unlockCost: 0
      updatedAt: 1787558400000
    jax
      name: "JAX"
      unlocked: true
      unlockCost: 30000
      unlockedAt: 1787558400000
      updatedAt: 1787558400000
  updatedAt: 1787558400000
```

The first 12 character IDs are `nova`, `orbit`, `jax`, `raze`, `echo`, `regalia`, `kairo`, `luna`, `volt`, `mako`, `glitch`, and `ember`. Nova is the starter. Each other character costs 30,000 coins.

## Purchase and synchronization flow

1. Firebase Auth supplies `{uid}`.
2. On game bootstrap, the app starts a realtime listener on `players/{uid}`. Existing Firestore character data is used only to seed Realtime Database the first time.
3. Unlocking uses a transaction on the player node. The transaction verifies the character is locked and the latest server coin balance is sufficient, deducts 30,000 coins, unlocks the character, and selects it atomically.
4. The listener refreshes the roster, featured art, selected checkmark, top-screen coin balance, and selected avatar on every connected session.
5. Character selection updates `selectedCharacter` immediately. A Firestore mirror is retained for compatibility with the existing profile code.

## Unity layout and assets

The app already uses a `CanvasScaler` with a 1080×1920 portrait reference and a safe-area root. The character screen uses fixed reference-space proportions inside that scaled canvas, so it remains intact on 320, 375, 768, and 1024+ pixel widths without per-device prefabs. Character textures are loaded from:

```text
Assets/Resources/UI/Characters/{characterId}
```

The screen is runtime-built, so no scene or prefab needs manual wiring. The bottom CHARACTERS/UPGRADES/SHOP navigation is shared with the existing ME screen, and the red X exits back to the game.

## Deploying rules

`firebase.json` points Realtime Database to `database.rules.json`. Deploy rules from the project root with the Firebase CLI when ready:

```bash
firebase deploy --only database
```

The app code does not deploy rules automatically.
