"use strict";

const assert = require("node:assert/strict");
const {
  computeAchievementUpdates,
  fallbackAchievementCatalog,
  normalizeAchievementCatalog,
} = require("./achievement-engine");

const now = 1724673600000;

const emptyUpdates = computeAchievementUpdates({}, fallbackAchievementCatalog, now);
assert.equal(Object.keys(emptyUpdates).filter((key) => key.startsWith("achievementProgress/")).length, 17);
assert.equal(Object.keys(emptyUpdates).filter((key) => key.startsWith("badges/")).length, 0);

const completePlayer = {
  missions: { completedCount: 300 },
  achievementMetrics: {
    lifetimeCoinsCollected: 100000,
    crystalsCollected: 100,
    noAcrobaticsBestScore: 1500000,
    ceilingDistanceMeters: 25000,
    longestRunSeconds: 1200,
    totalDistanceMeters: 2000000,
    magnetCoinsCollected: 10000,
    crystalRevives: 50,
  },
  collectibles: {
    shield: { totalCollected: 2000 },
    speed_boost: { totalCollected: 2000 },
    invulnerability: { totalCollected: 2000 },
    magnet: { totalCollected: 2000 },
    wall_walk: { totalCollected: 1000 },
    timezone: { totalCollected: 1000 },
  },
  upgrades: {
    shield: { level: 6 },
    speed_boost: { level: 6 },
    invulnerability: { level: 6 },
    magnet: { level: 6 },
    wall_walk: { level: 6 },
    timezone: { level: 3 },
  },
};
const completeUpdates = computeAchievementUpdates(completePlayer, fallbackAchievementCatalog, now);
assert.equal(Object.keys(completeUpdates).filter((key) => key.startsWith("badges/")).length, 17);
assert.equal(Object.keys(completeUpdates).filter((key) => key.startsWith("pendingBadgeUnlocks/")).length, 17);
assert.deepEqual(completeUpdates["badges/achievement_badge_17"], {
  achievementId: "second_chance",
  achievementNumber: 17,
  unlocked: true,
  collected: false,
  unlockedAt: now,
});
assert.equal(completeUpdates["achievementProgress/everything_is_mine"].completedTiers, 4);

const alreadyCollected = JSON.parse(JSON.stringify(completePlayer));
alreadyCollected.achievementProgress = {};
alreadyCollected.badges = {
  achievement_badge_17: {
    achievementId: "second_chance",
    achievementNumber: 17,
    unlocked: true,
    collected: true,
    unlockedAt: now - 1,
    collectedAt: now,
  },
};
const collectedUpdates = computeAchievementUpdates(alreadyCollected, fallbackAchievementCatalog, now);
assert.equal(Object.hasOwn(collectedUpdates, "badges/achievement_badge_17"), false);

assert.equal(normalizeAchievementCatalog({ invalid: true }), fallbackAchievementCatalog);
console.log("achievement-engine: all 17 achievement and badge tests passed");
