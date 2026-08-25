# Boosters shop integration

The Boosters screen is constructed at runtime by `GravityHalfDeadApp.ShopUI.cs`. It uses the
existing responsive 1080 x 1920 safe-area composition, the existing top coin/multiplier/settings
HUD, a vertically scrollable card viewport under BOOSTS, a fixed STORE/BOOSTS footer, and the
standard red exit button. Each booster card uses three non-overlapping proportional columns for
artwork, copy, and purchase controls. Text is bounded and scales down within its own column so long
titles and descriptions remain readable on narrow phones and tablets. STORE is intentionally empty
until products are added later.

## Realtime Database schema

Realtime Database is the authoritative shop state. Every signed-in player owns this branch:

```text
players/{uid}
  coins: 18397
  boosters
    gravity_disc/inventory: 12
    headstart/inventory: 18
    mega_headstart/inventory: 9
    multiplier_booster/inventory: 27
  dailyAds
    boosters
      gravity_disc
        count: 2
        localDate: "2026-08-25"
        timezoneOffsetMinutes: 180
        lastResetAt: <server timestamp>
        lastWatchAt: <server timestamp>
      headstart
        count: 1
        localDate: "2026-08-25"
        timezoneOffsetMinutes: 180
      mega_headstart
        count: 3
        localDate: "2026-08-25"
        timezoneOffsetMinutes: 180
      multiplier_booster
        count: 0
        localDate: "2026-08-25"
        timezoneOffsetMinutes: 180
```

Coin purchases run as a single root transaction, so coin deduction and inventory increment either
both commit or neither commits. A rewarded grant also runs as one transaction. Each booster has its
own independent `3`-ads-per-local-day allowance, so watching an ad for one booster does not consume
an ad for another booster. The screen listens to the player branch and updates coin balance,
inventory, counters, and button states live. Firestore receives a compatibility mirror in
`booster_inventory` and `daily_rewarded_ads`.

Deploy `database.rules.json` before testing on a device:

```bash
firebase deploy --only database
```

## Google rewarded test ads

Google Mobile Ads for Unity 11.4.0 is installed through OpenUPM. The app automatically adds
`GoogleMobileAdsTestRewardedProvider`, initializes the SDK on the main thread, preloads one rewarded
ad, and loads the next ad after close/failure. It uses Google's official rewarded test units:

- Android: `ca-app-pub-3940256099942544/5224354917`
- iOS: `ca-app-pub-3940256099942544/1712485313`

The matching Google sample app IDs live in
`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`. These test IDs are intentionally
not connected to a monetized AdMob account. Replace the Android/iOS app IDs and both platform ad-unit
IDs before publishing a production build.

Use these placement IDs in the adapter:

- `shop_booster_gravity_disc`
- `shop_booster_headstart`
- `shop_booster_mega_headstart`
- `shop_booster_multiplier_booster`

Invoke the completion callback with `true` only from the SDK's verified rewarded-completion event.
Cancellation, failure, and skipped ads must invoke it with `false`. There is no combined ad counter
in the header. Every booster button displays and persists its own daily count. The UI disables
WATCH AD when the provider is unavailable; when that specific booster reaches `3/3`, only its
button is disabled and its label changes to `RESET TOMORROW`.

For production anti-cheat, enable the ad network's server-side verification and move the final grant
to a trusted backend. The client transaction protects consistency but cannot prove that a modified
client truly watched an ad.

## Local-midnight reset

Each booster stores its own local date, while the UTC offset comes from the player's device. While
the app is open the monitor re-evaluates the device clock at least once per minute and schedules its
final wait to the exact local midnight boundary. Every booster counter then resets to `0/3` and
persists the new date with a Firebase server timestamp. If the app is closed at midnight, the reset
occurs during the next shop sync before an ad can be claimed.

## Artwork

Transparent booster artwork is loaded from:

```text
Assets/Resources/UI/Shop/Boosters/gravity_disc.png
Assets/Resources/UI/Shop/Boosters/headstart.png
Assets/Resources/UI/Shop/Boosters/mega_headstart.png
Assets/Resources/UI/Shop/Boosters/multiplier_booster.png
```
