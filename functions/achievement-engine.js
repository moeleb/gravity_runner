"use strict";

const fallbackAchievementCatalog = Object.freeze([
  { n: 1, id: "mission_control", thresholds: [7, 50, 210, 300] },
  { n: 2, id: "coin_collector", thresholds: [10000, 25000, 50000, 100000] },
  { n: 3, id: "master_of_crystals", thresholds: [10, 25, 50, 100] },
  { n: 4, id: "no_acrobatics", thresholds: [75000, 300000, 750000, 1500000] },
  { n: 5, id: "gravity_master", thresholds: [500, 2000, 10000, 25000] },
  { n: 6, id: "born_survivor", thresholds: [120, 300, 600, 1200] },
  { n: 7, id: "into_the_void", thresholds: [25000, 100000, 500000, 2000000] },
  { n: 8, id: "power_hungry", thresholds: [100, 1000, 5000, 10000] },
  { n: 9, id: "magnetic_force", thresholds: [2, 3, 4, 6] },
  { n: 10, id: "wall_walker", thresholds: [2, 3, 4, 6] },
  { n: 11, id: "speed_demon", thresholds: [2, 3, 4, 6] },
  { n: 12, id: "shield_master", thresholds: [2, 3, 4, 6] },
  { n: 13, id: "untouchable", thresholds: [2, 3, 4, 6] },
  { n: 14, id: "time_lord", thresholds: [1, 2, 3] },
  { n: 15, id: "everything_is_mine", thresholds: [1, 3, 5, 6] },
  { n: 16, id: "magnet_riches", thresholds: [500, 1000, 2500, 10000] },
  { n: 17, id: "second_chance", thresholds: [1, 10, 20, 50] },
]);

function normalizeAchievementCatalog(rawCatalog) {
  if (!rawCatalog || typeof rawCatalog !== "object") return fallbackAchievementCatalog;
  const remote = Object.entries(rawCatalog).map(([id, value]) => {
    const rawThresholds = value && value.thresholds;
    const thresholdValues = Array.isArray(rawThresholds)
      ? rawThresholds
      : rawThresholds && typeof rawThresholds === "object"
        ? Object.keys(rawThresholds).sort((a, b) => Number(a) - Number(b))
          .map((key) => rawThresholds[key])
        : [];
    return {
      n: Number(value && value.number),
      id,
      thresholds: thresholdValues.map(numberValue),
    };
  }).filter((item) => Number.isInteger(item.n)
    && item.n >= 1 && item.n <= 17
    && /^[a-z0-9_]{1,64}$/.test(item.id)
    && item.thresholds.length >= 1 && item.thresholds.length <= 4
    && item.thresholds.every((threshold, index, all) => threshold > 0
      && (index === 0 || threshold > all[index - 1])));

  const numbers = new Set(remote.map((item) => item.n));
  const expectedIds = new Set(fallbackAchievementCatalog.map((item) => item.id));
  if (remote.length !== 17 || numbers.size !== 17
      || remote.some((item) => !expectedIds.has(item.id)))
    return fallbackAchievementCatalog;
  return remote.sort((a, b) => a.n - b.n);
}

function computeAchievementUpdates(player, catalog, now) {
  const updates = {};
  for (const achievement of catalog) {
    const progress = achievementMetric(player, achievement.n);
    const completedTiers = achievement.thresholds.filter((threshold) => progress >= threshold).length;
    const complete = completedTiers === achievement.thresholds.length;
    const current = valueAt(player, `achievementProgress/${achievement.id}`) || {};
    if (numberValue(current.progress) !== progress
        || numberValue(current.completedTiers) !== completedTiers
        || numberValue(current.tierCount) !== achievement.thresholds.length
        || Boolean(current.complete) !== complete) {
      updates[`achievementProgress/${achievement.id}`] = {
        number: achievement.n,
        progress,
        completedTiers,
        tierCount: achievement.thresholds.length,
        complete,
        updatedAt: now,
      };
    }

    const badgeId = `achievement_badge_${String(achievement.n).padStart(2, "0")}`;
    const badge = valueAt(player, `badges/${badgeId}`) || {};
    if (complete && badge.unlocked !== true) {
      updates[`badges/${badgeId}`] = {
        achievementId: achievement.id,
        achievementNumber: achievement.n,
        unlocked: true,
        collected: false,
        unlockedAt: now,
      };
      updates[`pendingBadgeUnlocks/${badgeId}`] = {
        achievementId: achievement.id,
        achievementNumber: achievement.n,
        status: "pending",
        unlockedAt: now,
      };
    } else if (!complete && badge.unlocked === true) {
      // A badge cannot survive if the authoritative tier calculation is incomplete.
      updates[`badges/${badgeId}`] = null;
      updates[`pendingBadgeUnlocks/${badgeId}`] = null;
    }
  }
  return updates;
}

function achievementMetric(player, number) {
  const metrics = player.achievementMetrics || {};
  const levels = player.upgrades || {};
  switch (number) {
    case 1: return numberValue(valueAt(player, "missions/completedCount"));
    case 2: return numberValue(metrics.lifetimeCoinsCollected);
    case 3: return numberValue(metrics.crystalsCollected);
    case 4: return numberValue(metrics.noAcrobaticsBestScore);
    case 5: return numberValue(metrics.ceilingDistanceMeters);
    case 6: return numberValue(metrics.longestRunSeconds);
    case 7: return numberValue(metrics.totalDistanceMeters);
    case 8:
      return Object.values(player.collectibles || {}).reduce(
        (sum, item) => sum + numberValue(item && item.totalCollected), 0);
    case 9: return numberValue(valueAt(levels, "magnet/level"));
    case 10: return numberValue(valueAt(levels, "wall_walk/level"));
    case 11: return numberValue(valueAt(levels, "speed_boost/level"));
    case 12: return numberValue(valueAt(levels, "shield/level"));
    case 13: return numberValue(valueAt(levels, "invulnerability/level"));
    case 14: return numberValue(valueAt(levels, "timezone/level"));
    case 15:
      return ["shield", "speed_boost", "invulnerability", "magnet", "wall_walk", "timezone"]
        .reduce((count, id) => count + (numberValue(valueAt(levels, `${id}/level`))
          >= (id === "timezone" ? 3 : 6) ? 1 : 0), 0);
    case 16: return numberValue(metrics.magnetCoinsCollected);
    case 17: return numberValue(metrics.crystalRevives);
    default: return 0;
  }
}

function valueAt(root, path) {
  return String(path).split("/").reduce(
    (value, key) => value && typeof value === "object" ? value[key] : undefined, root);
}

function numberValue(value) {
  const number = Number(value || 0);
  return Number.isFinite(number) ? Math.max(0, Math.floor(number)) : 0;
}

module.exports = {
  achievementMetric,
  computeAchievementUpdates,
  fallbackAchievementCatalog,
  normalizeAchievementCatalog,
};
