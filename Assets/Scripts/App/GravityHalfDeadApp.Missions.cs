using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private const int ActiveMissionCount = 3;
        private const int MissionAdActionSlot = 1;
        private const long MissionSkipCoinCost = 5000L;
        // Keep the established placement prefix because the Google rewarded-ad adapter
        // whitelists it. The reward now completes the mission even though the identifier
        // retains its backward-compatible name.
        private const string MissionAdPlacementPrefix = "mission_remove_";

        private sealed class MissionDefinition
        {
            public string Id = string.Empty;
            public string TitleTemplate = string.Empty;
            public string Scope = "overall";
            public bool Enabled = true;
            public bool RequiresUpgradeableItem;
            public long[] Targets = Array.Empty<long>();
            public long[] LateTargets = Array.Empty<long>();
            public string[] Parameters = Array.Empty<string>();
        }

        private sealed class PlayerMissionSlot
        {
            public string InstanceId = string.Empty;
            public string DefinitionId = string.Empty;
            public string Title = string.Empty;
            public string Parameter = string.Empty;
            public string Status = "active";
            public long Target = 1L;
            public long Progress;
            public long Tier = 1L;

            public bool IsResolved => !string.Equals(Status, "active", StringComparison.Ordinal);
        }

        private readonly struct MissionProgressUpdate
        {
            public readonly string DefinitionId;
            public readonly long Value;
            public readonly bool SetValue;
            public readonly string Parameter;

            public MissionProgressUpdate(string definitionId, long value, bool setValue = false,
                string parameter = "")
            {
                DefinitionId = definitionId;
                Value = Math.Max(0L, value);
                SetValue = setValue;
                Parameter = parameter ?? string.Empty;
            }
        }

        private readonly List<MissionDefinition> missionPool = new();
        private readonly PlayerMissionSlot[] activeMissionSlots = new PlayerMissionSlot[ActiveMissionCount];
        private DatabaseReference realtimeMissionPlayerReference;
        private DatabaseReference realtimeMissionStateReference;
        private string realtimeMissionUserId = string.Empty;
        private long missionSetNumber = 1L;
        private long missionTier = 1L;
        private long missionCompletedCount;
        private int missionSetsPerTier = 3;
        private int missionMaximumTier = 6;
        private bool missionActionInFlight;
        private bool missionSetGenerationInFlight;
        private Coroutine missionAdvanceCoroutine;
        private readonly HashSet<string> missionRunDistinctPowerups = new(StringComparer.Ordinal);
        private Task missionRunResetTask = Task.CompletedTask;

        private async Task StartMissionRealtimeSyncAsync()
        {
            StopMissionRealtimeSync();
            if (realtimeDatabase == null || auth == null || auth.CurrentUser == null)
                return;

            realtimeMissionUserId = auth.CurrentUser.UserId;
            realtimeMissionPlayerReference = realtimeDatabase.RootReference
                .Child("players").Child(realtimeMissionUserId);
            realtimeMissionStateReference = realtimeMissionPlayerReference.Child("missions");
            realtimeMissionStateReference.ValueChanged += HandleMissionStateChanged;

            await LoadMissionPoolAsync();
            try
            {
                var snapshot = await realtimeMissionStateReference.GetValueAsync();
                if (MissionSnapshotHasThreeSlots(snapshot))
                    ApplyMissionStateSnapshot(snapshot);
                else
                {
                    missionCompletedCount = Math.Max(0L, bootstrapState.CompletedMissions);
                    missionSetNumber = 0L;
                    await GenerateNextMissionSetAsync(true);
                }
            }
            catch (Exception exception)
            {
                SetMissionStatus("MISSION SYNC DELAYED · USING THE SAFE LOCAL POOL", Muted);
                Debug.LogWarning("Mission state initialization delayed: " + exception.Message);
                if (!MissionSlotsArePopulated())
                    CreateLocalMissionSet();
            }
        }

        private void StopMissionRealtimeSync()
        {
            if (realtimeMissionStateReference != null)
                realtimeMissionStateReference.ValueChanged -= HandleMissionStateChanged;
            realtimeMissionStateReference = null;
            realtimeMissionPlayerReference = null;
            realtimeMissionUserId = string.Empty;
            missionActionInFlight = false;
            missionSetGenerationInFlight = false;
            missionRunResetTask = Task.CompletedTask;
            if (missionAdvanceCoroutine != null)
            {
                StopCoroutine(missionAdvanceCoroutine);
                missionAdvanceCoroutine = null;
            }
        }

        private async Task LoadMissionPoolAsync()
        {
            missionPool.Clear();
            try
            {
                var snapshot = await realtimeDatabase.RootReference.Child("missionPool").GetValueAsync();
                if (snapshot != null && snapshot.Exists)
                {
                    missionSetsPerTier = Mathf.Max(1,
                        (int)DatabaseLong(snapshot.Child("setsPerTier").Value, 3L));
                    missionMaximumTier = Mathf.Max(1,
                        (int)DatabaseLong(snapshot.Child("maximumTier").Value, 6L));
                    foreach (var behavior in snapshot.Child("behaviors").Children)
                    {
                        var definition = ParseMissionDefinition(behavior);
                        if (definition != null)
                            missionPool.Add(definition);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Firebase mission pool unavailable; using bundled fallback. " +
                                 exception.Message);
            }

            if (missionPool.Count < ActiveMissionCount)
            {
                missionPool.Clear();
                missionPool.AddRange(BuildFallbackMissionPool());
            }
        }

        private static MissionDefinition ParseMissionDefinition(DataSnapshot snapshot)
        {
            var id = snapshot.Key ?? string.Empty;
            var template = snapshot.Child("titleTemplate").Value?.ToString() ?? string.Empty;
            var targets = SnapshotLongArray(snapshot.Child("targets"));
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(template) || targets.Length == 0)
                return null;

            return new MissionDefinition
            {
                Id = id,
                TitleTemplate = template,
                Scope = snapshot.Child("scope").Value?.ToString() ?? "overall",
                Enabled = !snapshot.Child("enabled").Exists ||
                          string.Equals(snapshot.Child("enabled").Value?.ToString(), "True",
                              StringComparison.OrdinalIgnoreCase),
                RequiresUpgradeableItem = snapshot.Child("requiresUpgradeableItem").Exists &&
                                          string.Equals(snapshot.Child("requiresUpgradeableItem").Value?.ToString(),
                                              "True", StringComparison.OrdinalIgnoreCase),
                Targets = targets,
                LateTargets = SnapshotLongArray(snapshot.Child("lateTargets")),
                Parameters = SnapshotStringArray(snapshot.Child("parameters"))
            };
        }

        private static long[] SnapshotLongArray(DataSnapshot snapshot)
        {
            var values = new List<long>();
            if (snapshot == null || !snapshot.Exists)
                return values.ToArray();
            foreach (var child in snapshot.Children)
                values.Add(Math.Max(0L, DatabaseLong(child.Value, 0L)));
            return values.Where(value => value > 0L).ToArray();
        }

        private static string[] SnapshotStringArray(DataSnapshot snapshot)
        {
            var values = new List<string>();
            if (snapshot == null || !snapshot.Exists)
                return values.ToArray();
            foreach (var child in snapshot.Children)
            {
                var value = child.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }
            return values.ToArray();
        }

        private void HandleMissionStateChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                SetMissionStatus("MISSION SYNC DELAYED", Muted);
                Debug.LogWarning("Mission listener: " + args.DatabaseError.Message);
                return;
            }
            if (args.Snapshot == null || !args.Snapshot.Exists || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != realtimeMissionUserId)
                return;
            ApplyMissionStateSnapshot(args.Snapshot);
        }

        private void ApplyMissionStateSnapshot(DataSnapshot snapshot)
        {
            var previousSetNumber = missionSetNumber;
            var previousSetResolved = MissionSetIsResolved();
            missionSetNumber = Math.Max(1L, DatabaseLong(snapshot.Child("setNumber").Value, 1L));
            missionTier = Math.Max(1L, DatabaseLong(snapshot.Child("tier").Value, 1L));
            missionCompletedCount = Math.Max(0L,
                DatabaseLong(snapshot.Child("completedCount").Value, bootstrapState.CompletedMissions));

            for (var i = 0; i < ActiveMissionCount; i++)
                activeMissionSlots[i] = ParsePlayerMissionSlot(snapshot.Child("active/" + i));

            bootstrapState.CompletedMissions = missionCompletedCount;
            bootstrapState.ScoreMultiplier = ScoreMultiplierForCompletedMissions(missionCompletedCount);
            RefreshHomeHeaderDynamicValues();

            var shouldAnimate = previousSetNumber > 0L && missionSetNumber > previousSetNumber
                                && previousSetResolved && gameMissionsContent != null
                                && gameMissionsContent.activeInHierarchy;
            if (shouldAnimate)
                BeginMissionSetTransition();
            else
                RefreshMissionUI();

            if (MissionSetIsResolved())
                ScheduleNextMissionSet();
        }

        private static PlayerMissionSlot ParsePlayerMissionSlot(DataSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Exists)
                return null;
            return new PlayerMissionSlot
            {
                InstanceId = snapshot.Child("instanceId").Value?.ToString() ?? string.Empty,
                DefinitionId = snapshot.Child("definitionId").Value?.ToString() ?? string.Empty,
                Title = snapshot.Child("title").Value?.ToString() ?? "MISSION",
                Parameter = snapshot.Child("parameter").Value?.ToString() ?? string.Empty,
                Status = snapshot.Child("status").Value?.ToString() ?? "active",
                Target = Math.Max(1L, DatabaseLong(snapshot.Child("target").Value, 1L)),
                Progress = Math.Max(0L, DatabaseLong(snapshot.Child("progress").Value, 0L)),
                Tier = Math.Max(1L, DatabaseLong(snapshot.Child("tier").Value, 1L))
            };
        }

        private static bool MissionSnapshotHasThreeSlots(DataSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Exists)
                return false;
            for (var i = 0; i < ActiveMissionCount; i++)
            {
                if (!snapshot.Child("active/" + i + "/instanceId").Exists)
                    return false;
            }
            return true;
        }

        private bool MissionSlotsArePopulated()
            => activeMissionSlots.All(slot => slot != null && !string.IsNullOrWhiteSpace(slot.InstanceId));

        private bool MissionSetIsResolved()
            => MissionSlotsArePopulated() && activeMissionSlots.All(slot => slot.IsResolved);

        private void ScheduleNextMissionSet()
        {
            if (missionAdvanceCoroutine == null && !missionSetGenerationInFlight)
                missionAdvanceCoroutine = StartCoroutine(AdvanceMissionSetAfterCheckDelay());
        }

        private IEnumerator AdvanceMissionSetAfterCheckDelay()
        {
            // Leave the completed cards on-screen long enough for all three green ticks to read
            // clearly before the right-out / left-in replacement animation begins.
            yield return new WaitForSecondsRealtime(1.25f);
            missionAdvanceCoroutine = null;
            if (MissionSetIsResolved())
                _ = GenerateNextMissionSetAsync(false);
        }

        private async Task GenerateNextMissionSetAsync(bool initial)
        {
            if (missionSetGenerationInFlight || missionPool.Count < ActiveMissionCount)
                return;
            missionSetGenerationInFlight = true;
            var expectedSetNumber = missionSetNumber;
            var nextSetNumber = initial ? 1L : expectedSetNumber + 1L;
            var nextTier = MissionTierForSet(nextSetNumber);
            var generated = GenerateRandomMissionSlots(nextSetNumber, nextTier);

            try
            {
                if (realtimeMissionStateReference == null)
                {
                    ApplyLocalGeneratedSet(nextSetNumber, nextTier, generated);
                    return;
                }

                var committed = false;
                var result = await realtimeMissionStateReference.RunTransaction(mutableMissions =>
                {
                    committed = false;
                    var hasThree = MutableMissionSetHasThreeSlots(mutableMissions);
                    var currentSet = DatabaseLong(mutableMissions.Child("setNumber").Value, 0L);
                    if (initial && hasThree)
                        return TransactionResult.Abort();
                    if (!initial && (currentSet != expectedSetNumber || !MutableMissionSetIsResolved(mutableMissions)))
                        return TransactionResult.Abort();

                    mutableMissions.Child("setNumber").Value = nextSetNumber;
                    mutableMissions.Child("tier").Value = nextTier;
                    if (mutableMissions.Child("completedCount").Value == null)
                        mutableMissions.Child("completedCount").Value = missionCompletedCount;
                    mutableMissions.Child("active").Value = null;
                    for (var i = 0; i < ActiveMissionCount; i++)
                        WriteMissionSlot(mutableMissions.Child("active/" + i), generated[i]);
                    mutableMissions.Child("updatedAt").Value = ServerValue.Timestamp;
                    committed = true;
                    return TransactionResult.Success(mutableMissions);
                }, true);

                if (result != null && result.Exists)
                    ApplyMissionStateSnapshot(result);
                if (committed)
                    _ = MirrorMissionStateToFirestoreAsync();
            }
            catch (Exception exception)
            {
                SetMissionStatus("NEW MISSIONS COULD NOT SYNC · TRYING AGAIN", Muted);
                Debug.LogWarning("Mission set generation delayed: " + exception.Message);
                if (initial && !MissionSlotsArePopulated())
                    ApplyLocalGeneratedSet(nextSetNumber, nextTier, generated);
            }
            finally
            {
                missionSetGenerationInFlight = false;
                RefreshMissionUI();
            }
        }

        private static bool MutableMissionSetHasThreeSlots(MutableData missions)
        {
            for (var i = 0; i < ActiveMissionCount; i++)
            {
                if (missions.Child("active/" + i + "/instanceId").Value == null)
                    return false;
            }
            return true;
        }

        private static bool MutableMissionSetIsResolved(MutableData missions)
        {
            if (!MutableMissionSetHasThreeSlots(missions))
                return false;
            for (var i = 0; i < ActiveMissionCount; i++)
            {
                var status = missions.Child("active/" + i + "/status").Value?.ToString() ?? "active";
                if (string.Equals(status, "active", StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static void WriteMissionSlot(MutableData target, PlayerMissionSlot mission)
        {
            target.Child("instanceId").Value = mission.InstanceId;
            target.Child("definitionId").Value = mission.DefinitionId;
            target.Child("title").Value = mission.Title;
            target.Child("parameter").Value = mission.Parameter;
            target.Child("target").Value = mission.Target;
            target.Child("progress").Value = 0L;
            target.Child("status").Value = "active";
            target.Child("tier").Value = mission.Tier;
            target.Child("assignedAt").Value = ServerValue.Timestamp;
        }

        private void CreateLocalMissionSet()
        {
            var setNumber = Math.Max(1L, missionSetNumber);
            var tier = MissionTierForSet(setNumber);
            ApplyLocalGeneratedSet(setNumber, tier, GenerateRandomMissionSlots(setNumber, tier));
        }

        private void ApplyLocalGeneratedSet(long setNumber, long tier, PlayerMissionSlot[] generated)
        {
            missionSetNumber = setNumber;
            missionTier = tier;
            for (var i = 0; i < ActiveMissionCount; i++)
                activeMissionSlots[i] = generated[i];
            RefreshMissionUI();
        }

        private long MissionTierForSet(long setNumber)
            => Math.Min(missionMaximumTier,
                Math.Max(1L, 1L + (Math.Max(1L, setNumber) - 1L) / Math.Max(1, missionSetsPerTier)));

        private PlayerMissionSlot[] GenerateRandomMissionSlots(long setNumber, long tier)
        {
            var random = new System.Random(Guid.NewGuid().GetHashCode() ^ (int)setNumber);
            var previousIds = new HashSet<string>(activeMissionSlots
                .Where(slot => slot != null).Select(slot => slot.DefinitionId));
            var candidates = missionPool.Where(IsMissionDefinitionEligible)
                .Where(definition => !previousIds.Contains(definition.Id)).ToList();
            if (candidates.Count < ActiveMissionCount)
                candidates = missionPool.Where(IsMissionDefinitionEligible).ToList();
            Shuffle(candidates, random);

            var generated = new PlayerMissionSlot[ActiveMissionCount];
            for (var i = 0; i < ActiveMissionCount; i++)
            {
                var definition = candidates[i % candidates.Count];
                var parameter = definition.Parameters.Length == 0
                    ? string.Empty
                    : definition.Parameters[random.Next(definition.Parameters.Length)];
                var target = MissionTargetForTier(definition, tier, random);
                generated[i] = new PlayerMissionSlot
                {
                    InstanceId = Guid.NewGuid().ToString("N"),
                    DefinitionId = definition.Id,
                    Parameter = parameter,
                    Target = target,
                    Progress = 0L,
                    Status = "active",
                    Tier = tier,
                    Title = FormatMissionTitle(definition, target, parameter)
                };
            }
            return generated;
        }

        private bool IsMissionDefinitionEligible(MissionDefinition definition)
        {
            if (definition == null || !definition.Enabled || definition.Targets.Length == 0)
                return false;
            return !definition.RequiresUpgradeableItem || PlayerHasUpgradeableItem();
        }

        private bool PlayerHasUpgradeableItem()
        {
            for (var i = 0; i < PowerupIds.Length; i++)
            {
                if (bootstrapState.PowerupLevels[i] < PowerupMaxLevel(i))
                    return true;
            }
            return false;
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }

        private static long MissionTargetForTier(MissionDefinition definition, long tier,
            System.Random random)
        {
            var index = (int)Math.Max(0L, tier - 1L);
            if (index < definition.Targets.Length)
                return definition.Targets[index];
            if (definition.LateTargets.Length > 0)
                return definition.LateTargets[random.Next(definition.LateTargets.Length)];
            return definition.Targets[^1];
        }

        private static string FormatMissionTitle(MissionDefinition definition, long target,
            string parameter)
        {
            var targetLabel = IsDistanceMission(definition.Id)
                ? FormatMissionDistance(target)
                : target.ToString("N0", CultureInfo.InvariantCulture);
            return definition.TitleTemplate.Replace("{target}", targetLabel)
                .Replace("{parameter}", parameter ?? string.Empty);
        }

        private static bool IsDistanceMission(string definitionId)
            => definitionId.IndexOf("distance", StringComparison.Ordinal) >= 0
               || definitionId == "run_without_powerup";

        private static string FormatMissionDistance(long meters)
            => meters >= 1000L
                ? (meters / 1000f).ToString(meters % 1000L == 0L ? "0" : "0.0",
                    CultureInfo.InvariantCulture) + " km"
                : meters.ToString("N0", CultureInfo.InvariantCulture) + " m";

        public void ReportMissionProgress(string definitionId, long amount = 1L,
            string parameter = "")
        {
            if (string.IsNullOrWhiteSpace(definitionId) || amount <= 0L)
                return;
            _ = ApplyMissionProgressAsync(new[]
            {
                new MissionProgressUpdate(definitionId, amount, false, parameter)
            });
        }

        public void BeginMissionRun()
        {
            missionRunDistinctPowerups.Clear();
            missionRunResetTask = ResetIncompleteOneRunMissionProgressAsync();
        }

        private void ReportPowerupMissionCollected(string powerupId)
        {
            var parameter = powerupId switch
            {
                "magnet" => "Magnet",
                "shield" => "Shield",
                "coin_booster" => "Coin Booster",
                _ => string.Empty
            };
            var updates = new List<MissionProgressUpdate>
            {
                new("collect_powerups_one_run", 1L),
                new("collect_powerups_overall", 1L)
            };
            if (!string.IsNullOrEmpty(parameter))
                updates.Add(new MissionProgressUpdate("collect_same_powerup_one_run", 1L,
                    false, parameter));
            _ = ApplyMissionProgressAsync(updates);
        }

        public void RecordMissionPowerupUsed(string powerupId)
        {
            if (string.IsNullOrWhiteSpace(powerupId))
                return;
            var updates = new List<MissionProgressUpdate>();
            if (string.Equals(powerupId, "speed_boost", StringComparison.Ordinal))
                updates.Add(new MissionProgressUpdate("use_speed_booster", 1L));
            if (missionRunDistinctPowerups.Add(powerupId))
                updates.Add(new MissionProgressUpdate("different_powerups_one_run",
                    missionRunDistinctPowerups.Count, true));
            if (updates.Count > 0)
                _ = ApplyMissionProgressAsync(updates);
        }

        private void ReportBasicRunMissionProgress(long score, long coinsCollected,
            long distanceMeters)
        {
            _ = ApplyMissionProgressAsync(new[]
            {
                new MissionProgressUpdate("collect_coins_one_run", coinsCollected, true),
                new MissionProgressUpdate("collect_coins_overall", coinsCollected),
                new MissionProgressUpdate("run_distance_one_run", distanceMeters, true),
                new MissionProgressUpdate("run_distance_overall", distanceMeters),
                new MissionProgressUpdate("reach_score_one_run", score, true),
                new MissionProgressUpdate("complete_runs", 1L)
            });
        }

        /// <summary>
        /// Flush the counters that the runner collects during one run. Call this before
        /// RecordRunStatistics so the current mission set receives every run-scoped signal.
        /// Values are per-run totals; overall counters are incremented by those totals.
        /// </summary>
        public void RecordMissionRunTelemetry(long leftLaneCoins, long middleLaneCoins,
            long rightLaneCoins, long magnetCoins, long jumps, long rolls, long laneChanges,
            long chestsOpened, long chestsCollected, long runCoins, long distanceMeters, bool usedRevive,
            bool usedPowerup, bool diedWithinFirst60Seconds)
        {
            var updates = new List<MissionProgressUpdate>
            {
                new("collect_coins_left_lane", leftLaneCoins, true),
                new("collect_coins_middle_lane", middleLaneCoins, true),
                new("collect_coins_right_lane", rightLaneCoins, true),
                new("collect_coins_magnet_active", magnetCoins),
                new("jump", jumps),
                new("roll", rolls),
                new("change_lanes", laneChanges),
                new("open_chests", chestsOpened),
                new("collect_chests_during_runs", chestsCollected)
            };
            if (!usedRevive)
            {
                updates.Add(new MissionProgressUpdate("coins_without_revive", runCoins, true));
                updates.Add(new MissionProgressUpdate("distance_without_revive", distanceMeters, true));
            }
            if (!usedPowerup)
                updates.Add(new MissionProgressUpdate("run_without_powerup", distanceMeters, true));
            if (diedWithinFirst60Seconds)
                updates.Add(new MissionProgressUpdate("die_within_60_seconds", 1L, true));
            _ = ApplyMissionProgressAsync(updates.Where(update => update.Value > 0L).ToArray());
            RecordAchievementMagnetCoins(magnetCoins);
        }

        public void RecordMissionReviveByAd()
        {
            _ = ApplyMissionProgressAsync(new[]
            {
                new MissionProgressUpdate("revive_using_ad", 1L),
                new MissionProgressUpdate("watch_rewarded_ads", 1L)
            });
        }

        public void RecordMissionMegaBoostUsed()
            => ReportMissionProgress("use_mega_boost", 1L);

        private async Task ApplyMissionProgressAsync(IReadOnlyList<MissionProgressUpdate> updates)
        {
            if (updates == null || updates.Count == 0 || realtimeMissionPlayerReference == null)
                return;
            try
            {
                await missionRunResetTask;
                var completedInTransaction = 0L;
                var result = await realtimeMissionPlayerReference.RunTransaction(mutablePlayer =>
                {
                    completedInTransaction = 0L;
                    var missions = mutablePlayer.Child("missions");
                    for (var i = 0; i < ActiveMissionCount; i++)
                    {
                        var slot = missions.Child("active/" + i);
                        var status = slot.Child("status").Value?.ToString() ?? "active";
                        if (!string.Equals(status, "active", StringComparison.Ordinal))
                            continue;
                        var definitionId = slot.Child("definitionId").Value?.ToString() ?? string.Empty;
                        var slotParameter = slot.Child("parameter").Value?.ToString() ?? string.Empty;
                        foreach (var update in updates)
                        {
                            if (!string.Equals(update.DefinitionId, definitionId, StringComparison.Ordinal)
                                || (!string.IsNullOrEmpty(update.Parameter)
                                    && !string.Equals(update.Parameter, slotParameter,
                                        StringComparison.OrdinalIgnoreCase)))
                                continue;
                            var current = Math.Max(0L, DatabaseLong(slot.Child("progress").Value, 0L));
                            var next = update.SetValue ? update.Value : current + update.Value;
                            var target = Math.Max(1L, DatabaseLong(slot.Child("target").Value, 1L));
                            slot.Child("progress").Value = Math.Min(target, next);
                            if (next >= target)
                            {
                                slot.Child("status").Value = "completed";
                                slot.Child("resolvedAt").Value = ServerValue.Timestamp;
                                completedInTransaction++;
                            }
                            break;
                        }
                    }

                    if (completedInTransaction > 0L)
                    {
                        var completedCount = Math.Max(0L,
                            DatabaseLong(missions.Child("completedCount").Value,
                                missionCompletedCount));
                        missions.Child("completedCount").Value = completedCount + completedInTransaction;
                    }
                    missions.Child("updatedAt").Value = ServerValue.Timestamp;
                    return TransactionResult.Success(mutablePlayer);
                }, true);

                if (result != null && result.Exists)
                {
                    ApplyMissionStateSnapshot(result.Child("missions"));
                    if (completedInTransaction > 0L)
                        _ = MirrorMissionStateToFirestoreAsync();
                }
            }
            catch (Exception exception)
            {
                SetMissionStatus("MISSION PROGRESS WILL RETRY WHEN SYNC RETURNS", Muted);
                Debug.LogWarning("Mission progress update delayed: " + exception.Message);
            }
        }

        private async Task ResetIncompleteOneRunMissionProgressAsync()
        {
            if (realtimeMissionStateReference == null)
                return;
            try
            {
                await realtimeMissionStateReference.RunTransaction(mutableMissions =>
                {
                    for (var i = 0; i < ActiveMissionCount; i++)
                    {
                        var slot = mutableMissions.Child("active/" + i);
                        var status = slot.Child("status").Value?.ToString() ?? "active";
                        var definitionId = slot.Child("definitionId").Value?.ToString() ?? string.Empty;
                        var definition = missionPool.FirstOrDefault(value => value.Id == definitionId);
                        if (string.Equals(status, "active", StringComparison.Ordinal)
                            && definition != null && string.Equals(definition.Scope, "run",
                                StringComparison.Ordinal))
                            slot.Child("progress").Value = 0L;
                    }
                    return TransactionResult.Success(mutableMissions);
                }, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("One-run mission reset delayed: " + exception.Message);
            }
        }

        private static bool IsMissionAdActionSlot(int slotIndex)
            => slotIndex == MissionAdActionSlot;

        private async void CompleteMissionWithCoins(int slotIndex)
        {
            if (IsMissionAdActionSlot(slotIndex) || !CanActOnMission(slotIndex)
                || bootstrapState.Coins < MissionSkipCoinCost)
                return;
            missionActionInFlight = true;
            RefreshMissionUI();
            var resolved = false;
            try
            {
                var expectedInstance = activeMissionSlots[slotIndex].InstanceId;
                var result = await realtimeMissionPlayerReference.RunTransaction(mutablePlayer =>
                {
                    resolved = false;
                    var coins = mutablePlayer.Child("coins");
                    var balance = Math.Max(0L, DatabaseLong(coins.Value, bootstrapState.Coins));
                    var slot = mutablePlayer.Child("missions/active/" + slotIndex);
                    if (balance < MissionSkipCoinCost
                        || !string.Equals(slot.Child("instanceId").Value?.ToString(), expectedInstance,
                            StringComparison.Ordinal)
                        || !string.Equals(slot.Child("status").Value?.ToString(), "active",
                            StringComparison.Ordinal))
                        return TransactionResult.Abort();
                    coins.Value = balance - MissionSkipCoinCost;
                    var target = Math.Max(1L, DatabaseLong(slot.Child("target").Value, 1L));
                    slot.Child("progress").Value = target;
                    slot.Child("status").Value = "completed";
                    slot.Child("resolvedAt").Value = ServerValue.Timestamp;
                    var missions = mutablePlayer.Child("missions");
                    var completedCount = Math.Max(0L,
                        DatabaseLong(missions.Child("completedCount").Value, missionCompletedCount));
                    missions.Child("completedCount").Value = completedCount + 1L;
                    mutablePlayer.Child("missions/updatedAt").Value = ServerValue.Timestamp;
                    mutablePlayer.Child("updatedAt").Value = ServerValue.Timestamp;
                    resolved = true;
                    return TransactionResult.Success(mutablePlayer);
                }, true);

                if (resolved && result != null && result.Exists)
                {
                    bootstrapState.Coins = Math.Max(0L,
                        DatabaseLong(result.Child("coins").Value, bootstrapState.Coins));
                    ApplyMissionStateSnapshot(result.Child("missions"));
                    RefreshPowerupUI();
                    if (gameCoinAmountText != null)
                        gameCoinAmountText.text = bootstrapState.Coins.ToString("N0");
                    SetMissionStatus("MISSION COMPLETE", Green);
                    _ = MirrorMissionStateToFirestoreAsync(true);
                    ReportMissionProgress("spend_coins", MissionSkipCoinCost);
                }
            }
            catch (Exception exception)
            {
                SetMissionStatus("MISSION COMPLETION FAILED · NO G COINS WERE SPENT", Muted);
                Debug.LogWarning("Mission coin completion delayed: " + exception.Message);
            }
            finally
            {
                missionActionInFlight = false;
                RefreshMissionUI();
            }
        }

        private void WatchMissionRemovalAd(int slotIndex)
        {
            if (!IsMissionAdActionSlot(slotIndex) || !CanActOnMission(slotIndex))
                return;
            var placementId = MissionAdPlacementPrefix + activeMissionSlots[slotIndex].InstanceId;
            if (rewardedAdProvider == null || !rewardedAdProvider.IsRewardedAdReady(placementId))
            {
                SetMissionStatus("REWARDED AD IS NOT READY YET", Muted);
                RefreshMissionUI();
                return;
            }

            missionActionInFlight = true;
            RefreshMissionUI();
            rewardedAdProvider.ShowRewardedAd(placementId, completed =>
            {
                if (completed)
                    _ = ResolveMissionWithAdAsync(slotIndex);
                else
                {
                    missionActionInFlight = false;
                    SetMissionStatus("AD CLOSED · THE MISSION WAS NOT COMPLETED", Muted);
                    RefreshMissionUI();
                }
            });
        }

        private async Task ResolveMissionWithAdAsync(int slotIndex)
        {
            var resolved = false;
            try
            {
                if (slotIndex < 0 || slotIndex >= ActiveMissionCount
                    || realtimeMissionPlayerReference == null
                    || activeMissionSlots[slotIndex] == null
                    || activeMissionSlots[slotIndex].IsResolved)
                    return;
                var expectedInstance = activeMissionSlots[slotIndex].InstanceId;
                var result = await realtimeMissionPlayerReference.RunTransaction(mutablePlayer =>
                {
                    resolved = false;
                    var slot = mutablePlayer.Child("missions/active/" + slotIndex);
                    if (!string.Equals(slot.Child("instanceId").Value?.ToString(), expectedInstance,
                            StringComparison.Ordinal)
                        || !string.Equals(slot.Child("status").Value?.ToString(), "active",
                            StringComparison.Ordinal))
                        return TransactionResult.Abort();
                    var target = Math.Max(1L, DatabaseLong(slot.Child("target").Value, 1L));
                    slot.Child("progress").Value = target;
                    slot.Child("status").Value = "completed";
                    slot.Child("resolvedAt").Value = ServerValue.Timestamp;
                    var missions = mutablePlayer.Child("missions");
                    var completedCount = Math.Max(0L,
                        DatabaseLong(missions.Child("completedCount").Value, missionCompletedCount));
                    missions.Child("completedCount").Value = completedCount + 1L;
                    mutablePlayer.Child("missions/updatedAt").Value = ServerValue.Timestamp;
                    resolved = true;
                    return TransactionResult.Success(mutablePlayer);
                }, true);
                if (resolved && result != null && result.Exists)
                {
                    ApplyMissionStateSnapshot(result.Child("missions"));
                    SetMissionStatus("MISSION COMPLETE", Green);
                    ReportMissionProgress("watch_rewarded_ads", 1L);
                    _ = MirrorMissionStateToFirestoreAsync();
                }
            }
            catch (Exception exception)
            {
                SetMissionStatus("MISSION COMPLETION COULD NOT SYNC", Muted);
                Debug.LogWarning("Mission ad completion delayed: " + exception.Message);
            }
            finally
            {
                missionActionInFlight = false;
                RefreshMissionUI();
            }
        }

        private bool CanActOnMission(int slotIndex)
            => slotIndex >= 0 && slotIndex < ActiveMissionCount && !missionActionInFlight
               && realtimeMissionPlayerReference != null && activeMissionSlots[slotIndex] != null
               && !activeMissionSlots[slotIndex].IsResolved;

        private async Task MirrorMissionStateToFirestoreAsync(bool includeCoins = false)
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            var active = new Dictionary<string, object>();
            for (var i = 0; i < ActiveMissionCount; i++)
            {
                var slot = activeMissionSlots[i];
                if (slot == null)
                    continue;
                active[i.ToString(CultureInfo.InvariantCulture)] = new Dictionary<string, object>
                {
                    { "instance_id", slot.InstanceId },
                    { "definition_id", slot.DefinitionId },
                    { "title", slot.Title },
                    { "target", slot.Target },
                    { "progress", slot.Progress },
                    { "status", slot.Status },
                    { "tier", slot.Tier }
                };
            }

            var update = new Dictionary<string, object>
            {
                { "mission_set_number", missionSetNumber },
                { "mission_tier", missionTier },
                { "active_missions", active },
                { "completed_missions", missionCompletedCount },
                { "score_multiplier", ScoreMultiplierForCompletedMissions(missionCompletedCount) },
                { "mission_progress_updated_at", FieldValue.ServerTimestamp }
            };
            if (includeCoins)
                update["coins"] = bootstrapState.Coins;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId)
                    .SetAsync(update, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Firestore mission mirror delayed: " + exception.Message);
            }
        }

        private static List<MissionDefinition> BuildFallbackMissionPool()
        {
            var pool = new List<MissionDefinition>();
            AddMission(pool, "collect_coins_one_run", "Collect {target} G Coins in one run", "run",
                new long[] { 100, 250, 500, 750, 1000 }, new long[] { 500, 750, 1000 });
            AddMission(pool, "collect_coins_overall", "Collect {target} G Coins", "overall",
                new long[] { 1000, 3000, 5000, 10000, 15000, 20000 },
                new long[] { 5000, 10000, 15000, 20000 });
            AddMission(pool, "collect_powerups_one_run", "Collect {target} power-ups in one run", "run",
                new long[] { 3, 5, 7, 10 }, new long[] { 5, 7, 10 });
            AddMission(pool, "collect_powerups_overall", "Collect {target} power-ups", "overall",
                new long[] { 10, 20, 30, 50, 75 }, new long[] { 20, 30, 50, 75 });
            AddMission(pool, "watch_rewarded_ads", "Watch {target} rewarded ads", "overall",
                new long[] { 1, 2, 3, 5, 10 }, new long[] { 3, 5, 10 });
            AddMission(pool, "die_within_60_seconds", "Die within the first 60 seconds", "run",
                new long[] { 1 });
            AddMission(pool, "collect_coins_left_lane", "Collect {target} G Coins from the left lane", "run",
                new long[] { 50, 100, 200, 300 });
            AddMission(pool, "collect_coins_middle_lane", "Collect {target} G Coins from the middle lane", "run",
                new long[] { 50, 100, 200, 300 });
            AddMission(pool, "collect_coins_right_lane", "Collect {target} G Coins from the right lane", "run",
                new long[] { 50, 100, 200, 300 });
            AddMission(pool, "collect_same_powerup_one_run", "Collect {target} {parameter}s in one run", "run",
                new long[] { 2, 3, 4 }, null, new[] { "Magnet", "Shield", "Coin Booster" });
            AddMission(pool, "use_speed_booster", "Use the Speed Booster {target} times", "overall",
                new long[] { 1, 2, 3 }, new long[] { 1, 2, 3 });
            AddMission(pool, "use_mega_boost", "Use Mega Boost {target} times", "overall",
                new long[] { 1, 2 });
            AddMission(pool, "open_chests", "Open {target} chests", "overall",
                new long[] { 1, 3, 5, 10 }, new long[] { 3, 5, 10 });
            AddMission(pool, "run_distance_one_run", "Run {target} in one run", "run",
                new long[] { 1000, 2000, 3000, 5000, 7000 }, new long[] { 3000, 5000, 7000 });
            AddMission(pool, "run_distance_overall", "Run a total of {target}", "overall",
                new long[] { 5000, 10000, 20000, 30000, 50000 },
                new long[] { 10000, 20000, 30000, 50000 });
            AddMission(pool, "reach_score_one_run", "Score {target} points in one run", "run",
                new long[] { 10000, 25000, 50000, 100000, 250000 },
                new long[] { 50000, 100000, 250000 });
            AddMission(pool, "collect_coins_magnet_active", "Collect {target} G Coins while Magnet is active",
                "overall", new long[] { 100, 250, 500, 1000 }, new long[] { 250, 500, 1000 });
            AddMission(pool, "revive_using_ad", "Revive using an ad {target} times", "overall",
                new long[] { 1, 2, 3, 5 }, new long[] { 1, 2, 3, 5 });
            AddMission(pool, "complete_runs", "Complete {target} runs", "overall",
                new long[] { 3, 5, 10, 20 }, new long[] { 5, 10, 20 });
            AddMission(pool, "change_lanes", "Change lanes {target} times", "overall",
                new long[] { 50, 100, 250, 500 }, new long[] { 100, 250, 500 });
            AddMission(pool, "different_powerups_one_run", "Use {target} different power-ups in one run", "run",
                new long[] { 2, 3, 4 });
            AddMission(pool, "coins_without_revive", "Collect {target} G Coins without reviving", "run",
                new long[] { 250, 500, 1000 }, new long[] { 500, 1000 });
            AddMission(pool, "distance_without_revive", "Run {target} without reviving", "run",
                new long[] { 1000, 2000, 3000, 5000 }, new long[] { 2000, 3000, 5000 });
            AddMission(pool, "run_without_powerup", "Run {target} without using a power-up", "run",
                new long[] { 500, 1000, 2000 }, new long[] { 1000, 2000 });
            AddMission(pool, "collect_chests_during_runs", "Collect {target} chests during runs", "overall",
                new long[] { 1, 2, 3, 5 });
            AddMission(pool, "spend_coins", "Spend {target} G Coins", "overall",
                new long[] { 500, 1500, 3000, 5000, 10000 },
                new long[] { 1500, 3000, 5000, 10000 });
            AddMission(pool, "upgrade_something", "Upgrade any item or power-up once", "overall",
                new long[] { 1 }, null, null, true);
            AddMission(pool, "jump", "Jump {target} times", "overall",
                new long[] { 25, 50, 100, 250, 500 }, new long[] { 100, 250, 500 });
            AddMission(pool, "roll", "Roll {target} times", "overall",
                new long[] { 20, 50, 100, 200, 400 }, new long[] { 100, 200, 400 });
            return pool;
        }

        private static void AddMission(ICollection<MissionDefinition> pool, string id,
            string titleTemplate, string scope, long[] targets, long[] lateTargets = null,
            string[] parameters = null, bool requiresUpgradeableItem = false)
        {
            pool.Add(new MissionDefinition
            {
                Id = id,
                TitleTemplate = titleTemplate,
                Scope = scope,
                Targets = targets ?? Array.Empty<long>(),
                LateTargets = lateTargets ?? Array.Empty<long>(),
                Parameters = parameters ?? Array.Empty<string>(),
                RequiresUpgradeableItem = requiresUpgradeableItem
            });
        }
    }
}
