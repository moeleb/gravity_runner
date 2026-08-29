# Achievements and badges

The Unity client displays 17 achievements in one `ScrollRect`. It never displays or grants
tier rewards. A badge is unlocked only when every threshold for its achievement has completed.

## Realtime Database structure

```text
achievementCatalog/{achievementId}/
  number
  title
  description
  thresholds[]
  tierLabels[]

players/{uid}/
  achievementMetrics/
    lifetimeCoinsCollected
    totalDistanceMeters
    crystalsCollected
    noAcrobaticsBestScore
    ceilingDistanceMeters
    longestRunSeconds
    magnetCoinsCollected
    crystalRevives
    updatedAt
  achievementProgress/{achievementId}/
    number
    progress
    completedTiers
    tierCount
    complete
    updatedAt
  badges/achievement_badge_01/
    achievementId
    achievementNumber
    unlocked
    collected
    unlockedAt
    collectedAt
  pendingBadgeUnlocks/achievement_badge_01/
    achievementId
    achievementNumber
    status
    unlockedAt
    acknowledgedAt
```

`syncAchievementProgress` in `functions/index.js` owns `achievementProgress`, badge creation,
and the pending animation event. Database rules make server-created progress immutable to a
normal client. The client may only change a server-created badge from `collected: false` to
`collected: true`, after the collection animation's **Tap to continue** action.

The tier thresholds and labels are read from the authenticated, read-only
`achievementCatalog`. The bundled C# and Function catalogs are fallbacks for an offline or
unseeded database; no tier reward fields are stored or rendered.

## Gameplay reporting

Existing systems already feed missions, lifetime coins, total distance, collected power-ups,
power-up upgrade levels, Magnet coins, and Crystal revives.

The runner should preferably call this single end-of-run bridge:

```csharp
RecordCompletedRun(
    score,
    coinsCollected,
    distanceMeters,
    runDurationSeconds,
    ceilingDistanceMeters,
    leftLaneCoins,
    middleLaneCoins,
    rightLaneCoins,
    magnetCoins,
    jumps,
    rolls,
    laneChanges,
    chestsOpened,
    chestsCollected,
    usedRevive,
    usedPowerup,
    diedWithinFirst60Seconds);
```

For integrations that already call `RecordRunStatistics`, the two specialized APIs remain
available:

```csharp
RecordAchievementRunTelemetry(
    score,
    runDurationSeconds,
    ceilingDistanceMeters,
    jumps,
    rolls);

RecordAchievementCrystalsCollected(amount);
```

The server uses `jumps == 0 && rolls == 0` before updating the No Acrobatics best score.

## Deployment

```bash
firebase database:set /achievementCatalog achievement-catalog.seed.json \
  --force --disable-triggers --instance gravity-half-dead-default-rtdb
firebase deploy --only database
firebase deploy --only functions:syncAchievementProgress
```

Realtime Database rules can deploy on the Spark plan. Deploying a Cloud Function requires the
Firebase project to have Cloud Build enabled, which Firebase currently restricts to the Blaze
plan. Unity still compiles and the complete function source remains ready for that deployment.

The pure evaluator can be verified without Firebase emulators (or Java) using:

```bash
npm run test:achievements --prefix functions
```
