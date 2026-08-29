# Gravity Disc collection

The DISC tab contains 12 rideable boards. Every board prefab uses the same 1.8 m deck, centred pivot,
`RiderMount`, collider, and two trail anchors. Generate the prefab assets from Unity with
`Gravity Half Dead > Generate Rideable Discs`.

## Unlock catalogue

| Disc | Unlock |
| --- | --- |
| `core_runner` | Starter / always unlocked |
| `pulse_ring` | 5,000 G Coins |
| `neon_orbit` | 15,000 G Coins |
| `ion_skimmer` | 30,000 G Coins |
| `void_circuit` | 60,000 G Coins |
| `comet_drive` | 10 Disc Shards |
| `prism_halo` | 25 Disc Shards |
| `rift_bloom` | 50 Disc Shards |
| `quantum_crown` | 100 Disc Shards |
| `solar_forge` | Store non-consumable |
| `abyss_engine` | Store non-consumable |
| `aurora_sovereign` | Store non-consumable |

Disc Shards are intended to drop from chests. Add the awarded count to `players/{uid}/discShards`.
Coin and shard unlocks run as one Realtime Database transaction, so currency deduction and unlock
cannot be separated by a connection loss.

## Player Realtime Database shape

```json
{
  "players": {
    "uid": {
      "coins": 30000,
      "discShards": 25,
      "selectedDisc": "neon_orbit",
      "discs": {
        "core_runner": { "unlocked": true, "unlockKind": "starter", "unlockCost": 0 },
        "neon_orbit": { "unlocked": true, "unlockKind": "coins", "unlockCost": 15000 }
      },
      "discPurchases": {
        "sha256-transaction-id": {
          "productId": "gravityhalfdead.disc.solar_forge",
          "discId": "solar_forge",
          "localizedPrice": "$2.99",
          "purchasedAt": 0
        }
      }
    }
  }
}
```

Firestore mirrors `disc_shards`, `selected_disc`, and `unlocked_discs` for the existing player
bootstrap. Realtime Database is authoritative while the collection screen is open.

## Store products

Create these as **non-consumable** products in Google Play Console and App Store Connect:

- `gravityhalfdead.disc.solar_forge`
- `gravityhalfdead.disc.abyss_engine`
- `gravityhalfdead.disc.aurora_sovereign`

The UI displays the localized store price. It does not hardcode money values. The unlock is written
only from the Unity IAP pending-purchase callback and is idempotent by SHA-256 transaction key.

For a production economy, move purchase validation and currency grants to a trusted server/Cloud
Function before launch; client-owner database rules cannot prevent a modified client from writing its
own currency or unlock values.
