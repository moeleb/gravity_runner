# Gravity Store catalog and purchase data

The Store button and both green currency `+` buttons now use the same Store screen:

- G Coin `+` opens the **G COINS** catalog.
- Gravity Core `+` opens the **REVIVE CORES** catalog.
- The normal bottom **SHOP** card still opens **BOOSTS** first.

## Firestore catalog

Create the `storeCatalog` collection using the documents in
`store-catalog.seed.json`. Each document has:

```text
product_id       Exact Google Play / App Store consumable product id
reward_type      coins | gravity_cores
reward_amount    Currency granted after a confirmed purchase
sort_order       Display order
art_variant      coins: 0..5 | gravity_cores: 0..4 (unique bundle artwork)
enabled           true to show the bundle
```

The artwork is local and transparent so Firebase only needs to select its
variant. Coin art lives at `Resources/UI/Shop/Store/Coins/coin_bundle_0..5`;
revive art lives at `Resources/UI/Shop/Store/Revive/revive_bundle_0..4`.

Do **not** add `$0.99`, `$4.99`, or another player-facing price to Firebase.
The client reads `Product.metadata.localizedPriceString` from Unity IAP so the
store controls currency, tax, region, and future price changes.

The product IDs in Google Play Console and App Store Connect must exactly match
the IDs in `store-catalog.seed.json`, and every item must be a consumable.

## Dynamic popularity

Every persisted purchase creates one idempotent document at:

```text
users/{uid}/purchases/{sha256(storeTransactionId)}
```

`aggregateStorePurchase` increments `storeProductMetrics/{productId}` and writes
the current winners to `storePopularity/current`:

```text
coin_product_id
coin_purchase_count
gravity_core_product_id
gravity_core_purchase_count
```

The UI shows `MOST POPULAR` only for those two live winners. With no purchases,
no ribbon is shown.

## Deploy backend configuration

From the Unity project root, after reviewing the changes:

```bash
firebase deploy --only firestore:rules,database,functions:aggregateStorePurchase
```

The code in this workspace was not deployed automatically.

## Production purchase security

The Unity client persists grants idempotently before confirming a consumable,
so interrupted purchases are retried safely. Before public release, move the
receipt validation and currency grant into a trusted server endpoint that
validates Google Play purchase tokens / Apple transactions. Client-created
purchase records are suitable for development and UI integration, but must not
be treated as the final anti-fraud boundary.
