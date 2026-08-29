using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private sealed class AchievementDefinition
        {
            public int Number;
            public string Id = string.Empty;
            public string Title = string.Empty;
            public string Description = string.Empty;
            public long[] Thresholds = Array.Empty<long>();
            public string[] TierLabels = Array.Empty<string>();
            public string IconResource = string.Empty;
            public string Glyph = string.Empty;
            public Color Accent;

            public string BadgeId => "achievement_badge_" + Number.ToString("00");
        }

        private sealed class AchievementRuntimeState
        {
            public long Progress;
            public int CompletedTiers;
            public bool Complete;
            public bool BadgeUnlocked;
            public bool BadgeCollected;
        }

        private sealed class AchievementBadgeVisual
        {
            public GameObject Root;
            public Image Ring;
            public RawImage Art;
            public Text Glyph;
            public Text Number;
        }

        private static readonly AchievementDefinition[] AchievementCatalog =
        {
            Achievement(1, "mission_control", "MISSION CONTROL", "Complete missions",
                new long[] { 7, 50, 210, 300 }, null, "", "M", Hex("36CFFF")),
            Achievement(2, "coin_collector", "COIN COLLECTOR", "Collect G Coins overall",
                new long[] { 10000, 25000, 50000, 100000 }, null, "", "G", Hex("FFB82E")),
            Achievement(3, "master_of_crystals", "MASTER OF CRYSTALS", "Collect Crystals overall",
                new long[] { 10, 25, 50, 100 }, null, "UI/Shop/Store/Revive/revive_bundle_0", "", Hex("32D9FF")),
            Achievement(4, "no_acrobatics", "NO ACROBATICS", "Best one-run score without jumping or rolling",
                new long[] { 75000, 300000, 750000, 1500000 },
                new[] { "75K", "300K", "750K", "1.5M" }, "", "×", Hex("FF5252")),
            Achievement(5, "gravity_master", "GRAVITY MASTER", "Travel distance while running on the ceiling",
                new long[] { 500, 2000, 10000, 25000 },
                new[] { "500 m", "2 km", "10 km", "25 km" }, "UI/Discs/core_runner", "", Hex("35CFFF")),
            Achievement(6, "born_survivor", "BORN SURVIVOR", "Survive continuously in one run",
                new long[] { 120, 300, 600, 1200 },
                new[] { "2 min", "5 min", "10 min", "20 min" }, "", "S", Hex("C7D5FF")),
            Achievement(7, "into_the_void", "INTO THE VOID", "Travel total lifetime distance",
                new long[] { 25000, 100000, 500000, 2000000 },
                new[] { "25 km", "100 km", "500 km", "2,000 km" }, "UI/Discs/void_circuit", "", Hex("9C4DFF")),
            Achievement(8, "power_hungry", "POWER HUNGRY", "Collect power-ups overall",
                new long[] { 100, 1000, 5000, 10000 }, null, "", "ϟ", Hex("C74DFF")),
            Achievement(9, "magnetic_force", "MAGNETIC FORCE", "Upgrade Magnet",
                new long[] { 2, 3, 4, 6 }, new[] { "Lv.2", "Lv.3", "Lv.4", "Max Lv.6" },
                "UI/Powerups/magnet", "", Hex("F55464")),
            Achievement(10, "wall_walker", "WALL WALKER", "Upgrade Wall Walk",
                new long[] { 2, 3, 4, 6 }, new[] { "Lv.2", "Lv.3", "Lv.4", "Max Lv.6" },
                "UI/Powerups/wall_walk", "", Hex("A857FF")),
            Achievement(11, "speed_demon", "SPEED DEMON", "Upgrade Speed Boost",
                new long[] { 2, 3, 4, 6 }, new[] { "Lv.2", "Lv.3", "Lv.4", "Max Lv.6" },
                "UI/Powerups/speed_boost", "", Hex("60F154")),
            Achievement(12, "shield_master", "SHIELD MASTER", "Upgrade Shield",
                new long[] { 2, 3, 4, 6 }, new[] { "Lv.2", "Lv.3", "Lv.4", "Max Lv.6" },
                "UI/Powerups/shield", "", Hex("2ABFFF")),
            Achievement(13, "untouchable", "UNTOUCHABLE", "Upgrade Invulnerability",
                new long[] { 2, 3, 4, 6 }, new[] { "Lv.2", "Lv.3", "Lv.4", "Max Lv.6" },
                "UI/Powerups/invulnerability", "", Hex("FFD532")),
            Achievement(14, "time_lord", "TIME LORD", "Upgrade Time Increase",
                new long[] { 1, 2, 3 }, new[] { "Lv.1", "Lv.2", "Max Lv.3" },
                "UI/Powerups/timezone", "", Hex("FFB52D")),
            Achievement(15, "everything_is_mine", "EVERYTHING IS MINE", "Max out power-ups",
                new long[] { 1, 3, 5, 6 }, new[] { "1 MAX", "3 MAX", "5 MAX", "6 MAX" },
                "", "★", Hex("F5C542")),
            Achievement(16, "magnet_riches", "MAGNET RICHES", "Collect G Coins using Magnet",
                new long[] { 500, 1000, 2500, 10000 }, null, "UI/Powerups/magnet", "", Hex("FF8A34")),
            Achievement(17, "second_chance", "SECOND CHANCE", "Revive with Crystals",
                new long[] { 1, 10, 20, 50 }, null, "UI/Shop/Store/Revive/revive_bundle_0", "", Hex("4CCEFF"))
        };

        private readonly AchievementRuntimeState[] achievementStates =
            Enumerable.Range(0, 17).Select(_ => new AchievementRuntimeState()).ToArray();
        private readonly Queue<int> achievementUnlockQueue = new();
        private readonly HashSet<int> achievementUnlockQueued = new();
        private DatabaseReference realtimeAchievementPlayerReference;
        private string realtimeAchievementUserId = string.Empty;
        private bool achievementInitialSnapshotApplied;
        private bool achievementUnlockAnimationPlaying;
        private bool achievementCollectionAcknowledgementInFlight;

        private GameObject achievementCollectionOverlay;
        private CanvasGroup achievementCollectionCanvasGroup;
        private RectTransform achievementCollectionBadgeHost;
        private Text achievementCollectionNameText;
        private Text achievementCollectionStatusText;
        private Button achievementCollectionContinueButton;
        private int activeAchievementUnlockIndex = -1;

        private static AchievementDefinition Achievement(int number, string id, string title,
            string description, long[] thresholds, string[] tierLabels, string iconResource,
            string glyph, Color accent)
        {
            return new AchievementDefinition
            {
                Number = number,
                Id = id,
                Title = title,
                Description = description,
                Thresholds = thresholds,
                TierLabels = tierLabels ?? thresholds.Select(FormatAchievementNumber).ToArray(),
                IconResource = iconResource,
                Glyph = glyph,
                Accent = accent
            };
        }

        private async Task StartAchievementRealtimeSyncAsync()
        {
            StopAchievementRealtimeSync();
            if (realtimeDatabase == null || auth == null || auth.CurrentUser == null)
                return;

            await LoadAchievementCatalogAsync();
            realtimeAchievementUserId = auth.CurrentUser.UserId;
            realtimeAchievementPlayerReference = realtimeDatabase.RootReference
                .Child("players").Child(realtimeAchievementUserId);
            realtimeAchievementPlayerReference.ValueChanged += HandleAchievementPlayerChanged;
            achievementInitialSnapshotApplied = false;

            try
            {
                var snapshot = await realtimeAchievementPlayerReference.GetValueAsync();
                if (snapshot != null && snapshot.Exists)
                {
                    await SeedAchievementMetricsAsync(snapshot);
                    ApplyAchievementPlayerSnapshot(snapshot);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Achievement synchronization delayed: " + exception.Message);
                RefreshAchievementUI();
            }
        }

        private async Task LoadAchievementCatalogAsync()
        {
            try
            {
                var snapshot = await realtimeDatabase.RootReference.Child("achievementCatalog")
                    .GetValueAsync();
                if (snapshot == null || !snapshot.Exists)
                    return;
                foreach (var entry in snapshot.Children)
                {
                    var definition = AchievementCatalog.FirstOrDefault(value =>
                        string.Equals(value.Id, entry.Key, StringComparison.Ordinal));
                    if (definition == null)
                        continue;
                    var thresholds = SnapshotLongArray(entry.Child("thresholds"));
                    var labels = SnapshotStringArray(entry.Child("tierLabels"));
                    if (thresholds.Length is > 0 and <= 4)
                    {
                        definition.Thresholds = thresholds;
                        definition.TierLabels = labels.Length == thresholds.Length
                            ? labels : thresholds.Select(FormatAchievementNumber).ToArray();
                    }
                    var title = entry.Child("title").Value?.ToString();
                    var description = entry.Child("description").Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(title))
                        definition.Title = title.Trim();
                    if (!string.IsNullOrWhiteSpace(description))
                        definition.Description = description.Trim();
                }
                RefreshAchievementCatalogText();
                RefreshAchievementUI();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Remote achievement catalog unavailable; using bundled fallback. " +
                                 exception.Message);
            }
        }

        private void StopAchievementRealtimeSync()
        {
            if (realtimeAchievementPlayerReference != null)
                realtimeAchievementPlayerReference.ValueChanged -= HandleAchievementPlayerChanged;
            realtimeAchievementPlayerReference = null;
            realtimeAchievementUserId = string.Empty;
            achievementInitialSnapshotApplied = false;
        }

        private async Task SeedAchievementMetricsAsync(DataSnapshot player)
        {
            if (realtimeAchievementPlayerReference == null || player == null)
                return;
            var metrics = player.Child("achievementMetrics");
            var updates = new Dictionary<string, object>();
            if (!metrics.Child("lifetimeCoinsCollected").Exists)
                updates["achievementMetrics/lifetimeCoinsCollected"] =
                    Math.Max(0L, bootstrapState.LifetimeCoinsCollected);
            if (!metrics.Child("totalDistanceMeters").Exists)
                updates["achievementMetrics/totalDistanceMeters"] = Math.Max(0L, bootstrapState.TotalDistanceMeters);
            if (!metrics.Child("crystalsCollected").Exists)
                updates["achievementMetrics/crystalsCollected"] = 0L;
            if (!metrics.Child("noAcrobaticsBestScore").Exists)
                updates["achievementMetrics/noAcrobaticsBestScore"] = 0L;
            if (!metrics.Child("ceilingDistanceMeters").Exists)
                updates["achievementMetrics/ceilingDistanceMeters"] = 0L;
            if (!metrics.Child("longestRunSeconds").Exists)
                updates["achievementMetrics/longestRunSeconds"] = 0L;
            if (!metrics.Child("magnetCoinsCollected").Exists)
                updates["achievementMetrics/magnetCoinsCollected"] = 0L;
            if (!metrics.Child("crystalRevives").Exists)
                updates["achievementMetrics/crystalRevives"] = 0L;
            if (updates.Count == 0)
                return;
            updates["achievementMetrics/updatedAt"] = ServerValue.Timestamp;
            await realtimeAchievementPlayerReference.UpdateChildrenAsync(updates);
        }

        private void HandleAchievementPlayerChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                Debug.LogWarning("Achievement listener: " + args.DatabaseError.Message);
                return;
            }
            if (args.Snapshot == null || !args.Snapshot.Exists || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != realtimeAchievementUserId)
                return;
            ApplyAchievementPlayerSnapshot(args.Snapshot);
        }

        private void ApplyAchievementPlayerSnapshot(DataSnapshot player)
        {
            for (var i = 0; i < AchievementCatalog.Length; i++)
            {
                var definition = AchievementCatalog[i];
                var serverProgress = player.Child("achievementProgress/" + definition.Id);
                var progress = serverProgress.Child("progress").Exists
                    ? DatabaseLong(serverProgress.Child("progress").Value, 0L)
                    : AchievementMetricFromSnapshot(player, definition);
                var completedTiers = serverProgress.Child("completedTiers").Exists
                    ? (int)DatabaseLong(serverProgress.Child("completedTiers").Value, 0L)
                    : CompletedAchievementTierCount(definition, progress);
                var badge = player.Child("badges/" + definition.BadgeId);

                achievementStates[i].Progress = Math.Max(0L, progress);
                achievementStates[i].CompletedTiers = Mathf.Clamp(completedTiers, 0, definition.Thresholds.Length);
                achievementStates[i].Complete = achievementStates[i].CompletedTiers >= definition.Thresholds.Length;
                achievementStates[i].BadgeUnlocked = badge.Child("unlocked").Exists
                                                      && SnapshotBool(badge.Child("unlocked").Value);
                achievementStates[i].BadgeCollected = badge.Child("collected").Exists
                                                       && SnapshotBool(badge.Child("collected").Value);

                var pending = player.Child("pendingBadgeUnlocks/" + definition.BadgeId);
                var pendingStatus = pending.Child("status").Value?.ToString() ?? string.Empty;
                if (achievementStates[i].BadgeUnlocked && !achievementStates[i].BadgeCollected
                    && string.Equals(pendingStatus, "pending", StringComparison.Ordinal)
                    && achievementUnlockQueued.Add(i))
                    achievementUnlockQueue.Enqueue(i);
            }

            achievementInitialSnapshotApplied = true;
            RefreshAchievementUI();
            RefreshProfileBadgeCollectionUI();
            TryPlayNextAchievementUnlock();
        }

        private static long AchievementMetricFromSnapshot(DataSnapshot player,
            AchievementDefinition definition)
        {
            var metrics = player.Child("achievementMetrics");
            return definition.Number switch
            {
                1 => DatabaseLong(player.Child("missions/completedCount").Value, 0L),
                2 => DatabaseLong(metrics.Child("lifetimeCoinsCollected").Value, 0L),
                3 => DatabaseLong(metrics.Child("crystalsCollected").Value, 0L),
                4 => DatabaseLong(metrics.Child("noAcrobaticsBestScore").Value, 0L),
                5 => DatabaseLong(metrics.Child("ceilingDistanceMeters").Value, 0L),
                6 => DatabaseLong(metrics.Child("longestRunSeconds").Value, 0L),
                7 => DatabaseLong(metrics.Child("totalDistanceMeters").Value, 0L),
                8 => SumCollectibles(player.Child("collectibles")),
                9 => DatabaseLong(player.Child("upgrades/magnet/level").Value, 0L),
                10 => DatabaseLong(player.Child("upgrades/wall_walk/level").Value, 0L),
                11 => DatabaseLong(player.Child("upgrades/speed_boost/level").Value, 0L),
                12 => DatabaseLong(player.Child("upgrades/shield/level").Value, 0L),
                13 => DatabaseLong(player.Child("upgrades/invulnerability/level").Value, 0L),
                14 => DatabaseLong(player.Child("upgrades/timezone/level").Value, 0L),
                15 => CountMaxedPowerups(player.Child("upgrades")),
                16 => DatabaseLong(metrics.Child("magnetCoinsCollected").Value, 0L),
                17 => DatabaseLong(metrics.Child("crystalRevives").Value, 0L),
                _ => 0L
            };
        }

        private static long SumCollectibles(DataSnapshot collectibles)
        {
            if (collectibles == null || !collectibles.Exists)
                return 0L;
            return collectibles.Children.Sum(child =>
                Math.Max(0L, DatabaseLong(child.Child("totalCollected").Value, 0L)));
        }

        private static long CountMaxedPowerups(DataSnapshot upgrades)
        {
            if (upgrades == null || !upgrades.Exists)
                return 0L;
            long count = 0L;
            for (var i = 0; i < PowerupIds.Length; i++)
            {
                var level = DatabaseLong(upgrades.Child(PowerupIds[i] + "/level").Value, 0L);
                if (level >= PowerupMaxLevel(i))
                    count++;
            }
            return count;
        }

        private static int CompletedAchievementTierCount(AchievementDefinition definition, long progress)
            => definition.Thresholds.Count(threshold => progress >= threshold);

        private static bool SnapshotBool(object value)
        {
            if (value is bool boolean)
                return boolean;
            return value != null && bool.TryParse(value.ToString(), out var parsed) && parsed;
        }

        private void TryPlayNextAchievementUnlock()
        {
            if (!achievementInitialSnapshotApplied || achievementUnlockAnimationPlaying
                || achievementUnlockQueue.Count == 0)
                return;
            var index = achievementUnlockQueue.Dequeue();
            if (index < 0 || index >= AchievementCatalog.Length || achievementStates[index].BadgeCollected)
            {
                achievementUnlockQueued.Remove(index);
                TryPlayNextAchievementUnlock();
                return;
            }
            if (achievementCollectionOverlay == null)
                BuildAchievementCollectionOverlay();
            StartCoroutine(AnimateAchievementCollection(index));
        }

        private IEnumerator AnimateAchievementCollection(int index)
        {
            achievementUnlockAnimationPlaying = true;
            achievementCollectionAcknowledgementInFlight = false;
            activeAchievementUnlockIndex = index;
            var definition = AchievementCatalog[index];
            RebuildAchievementCollectionBadge(index);
            achievementCollectionNameText.text = "#" + definition.Number + "  " + definition.Title;
            achievementCollectionStatusText.text = "Badge unlocked";
            achievementCollectionStatusText.color = definition.Accent;
            achievementCollectionContinueButton.interactable = false;
            achievementCollectionOverlay.transform.SetAsLastSibling();
            achievementCollectionOverlay.SetActive(true);
            achievementCollectionCanvasGroup.alpha = 0f;
            achievementCollectionBadgeHost.localScale = Vector3.one * 0.12f;

            const float duration = 0.58f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                achievementCollectionCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                var scale = t < 0.72f
                    ? Mathf.Lerp(0.12f, 1.12f, Mathf.SmoothStep(0f, 1f, t / 0.72f))
                    : Mathf.Lerp(1.12f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.72f) / 0.28f));
                achievementCollectionBadgeHost.localScale = Vector3.one * scale;
                yield return null;
            }
            achievementCollectionCanvasGroup.alpha = 1f;
            achievementCollectionBadgeHost.localScale = Vector3.one;
            achievementCollectionContinueButton.interactable = true;
        }

        private async void AcknowledgeAchievementBadgeCollection()
        {
            if (achievementCollectionAcknowledgementInFlight || activeAchievementUnlockIndex < 0
                || activeAchievementUnlockIndex >= AchievementCatalog.Length
                || realtimeAchievementPlayerReference == null)
                return;
            achievementCollectionAcknowledgementInFlight = true;
            achievementCollectionContinueButton.interactable = false;
            achievementCollectionStatusText.text = "Saving badge…";
            var index = activeAchievementUnlockIndex;
            var definition = AchievementCatalog[index];
            try
            {
                var updates = new Dictionary<string, object>
                {
                    { "badges/" + definition.BadgeId + "/collected", true },
                    { "badges/" + definition.BadgeId + "/collectedAt", ServerValue.Timestamp },
                    { "pendingBadgeUnlocks/" + definition.BadgeId + "/status", "acknowledged" },
                    { "pendingBadgeUnlocks/" + definition.BadgeId + "/acknowledgedAt", ServerValue.Timestamp }
                };
                await realtimeAchievementPlayerReference.UpdateChildrenAsync(updates);
                achievementStates[index].BadgeCollected = true;
                achievementUnlockQueued.Remove(index);
                RefreshAchievementUI();
                RefreshProfileBadgeCollectionUI();
                yieldAchievementCollectionClose = StartCoroutine(CloseAchievementCollectionOverlay());
            }
            catch (Exception exception)
            {
                achievementCollectionStatusText.text = "Could not save · tap to retry";
                achievementCollectionStatusText.color = Coral;
                achievementCollectionContinueButton.interactable = true;
                Debug.LogWarning("Badge collection acknowledgement delayed: " + exception.Message);
            }
            finally
            {
                achievementCollectionAcknowledgementInFlight = false;
            }
        }

        private Coroutine yieldAchievementCollectionClose;

        private IEnumerator CloseAchievementCollectionOverlay()
        {
            for (var elapsed = 0f; elapsed < 0.22f; elapsed += Time.unscaledDeltaTime)
            {
                achievementCollectionCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.22f);
                yield return null;
            }
            achievementCollectionOverlay.SetActive(false);
            achievementCollectionCanvasGroup.alpha = 1f;
            activeAchievementUnlockIndex = -1;
            achievementUnlockAnimationPlaying = false;
            yieldAchievementCollectionClose = null;
            TryPlayNextAchievementUnlock();
        }

        private void BuildAchievementCollectionOverlay()
        {
            var host = safeRoot != null ? safeRoot : transform as RectTransform;
            achievementCollectionOverlay = new GameObject("Achievement Badge Collection Overlay");
            achievementCollectionOverlay.transform.SetParent(host != null ? host : transform, false);
            var overlayRect = achievementCollectionOverlay.AddComponent<RectTransform>();
            Stretch(overlayRect);
            var blocker = achievementCollectionOverlay.AddComponent<Image>();
            blocker.color = new Color(0.002f, 0.004f, 0.016f, 0.96f);
            blocker.raycastTarget = true;
            achievementCollectionCanvasGroup = achievementCollectionOverlay.AddComponent<CanvasGroup>();

            var panel = CreateCard("Collected Badge Panel", achievementCollectionOverlay.transform,
                Vector2.zero, new Vector2(900f, 1060f), Hex("070A1A"), 42);
            AddGraphicOutline(panel.GetComponent<Image>(), new Color(NeonPurple.r, NeonPurple.g,
                NeonPurple.b, 0.92f), 2.5f);
            var title = MakeText(panel.transform, "YOU COLLECTED\nTHE FOLLOWING", 43,
                FontStyle.Bold, Color.white, new Vector2(0f, 380f), new Vector2(760f, 130f),
                TextAnchor.MiddleCenter, 3);
            AddGraphicOutline(title, Hex("361B71"), 2.5f);
            MakeText(panel.transform, "ACHIEVEMENT BADGE", 19, FontStyle.Bold, Hex("C8A7FF"),
                new Vector2(0f, 292f), new Vector2(440f, 34f), TextAnchor.MiddleCenter, 2);

            var hostObject = new GameObject("Collected badge animation host");
            hostObject.transform.SetParent(panel.transform, false);
            achievementCollectionBadgeHost = hostObject.AddComponent<RectTransform>();
            SetRect(achievementCollectionBadgeHost, new Vector2(0f, 54f), new Vector2(390f, 390f));

            achievementCollectionNameText = MakeText(panel.transform, string.Empty, 31, FontStyle.Bold,
                Color.white, new Vector2(0f, -205f), new Vector2(760f, 80f),
                TextAnchor.MiddleCenter, 2);
            achievementCollectionStatusText = MakeText(panel.transform, "Badge unlocked", 20,
                FontStyle.Bold, Cyan, new Vector2(0f, -268f), new Vector2(600f, 42f),
                TextAnchor.MiddleCenter, 1);
            achievementCollectionContinueButton = MakeButton(panel.transform, "TAP TO CONTINUE",
                new Vector2(0f, -404f), new Vector2(590f, 96f), Hex("3B1775"), Color.white, 27,
                AcknowledgeAchievementBadgeCollection);
            AddGraphicOutline(achievementCollectionContinueButton.GetComponent<Image>(),
                new Color(NeonPurple.r, NeonPurple.g, NeonPurple.b, 0.92f), 2f);
            achievementCollectionContinueButton.gameObject.AddComponent<ButtonGlow>();
            achievementCollectionOverlay.SetActive(false);
        }

        private void RebuildAchievementCollectionBadge(int index)
        {
            for (var i = achievementCollectionBadgeHost.childCount - 1; i >= 0; i--)
                Destroy(achievementCollectionBadgeHost.GetChild(i).gameObject);
            BuildAchievementBadgeVisual(achievementCollectionBadgeHost, index, 350f, false);
        }

        private AchievementBadgeVisual BuildAchievementBadgeVisual(Transform parent, int index,
            float size, bool locked)
        {
            var definition = AchievementCatalog[Mathf.Clamp(index, 0, AchievementCatalog.Length - 1)];
            var accent = locked ? Hex("455168") : definition.Accent;
            var root = CreateCard("Badge " + definition.Number + " · " + definition.Title, parent,
                Vector2.zero, new Vector2(size, size), new Color(accent.r, accent.g, accent.b, 0.16f),
                Mathf.RoundToInt(size * 0.5f));
            root.GetComponent<Image>().raycastTarget = false;
            AddGraphicOutline(root.GetComponent<Image>(), new Color(accent.r, accent.g, accent.b, 0.94f),
                Mathf.Max(1.5f, size * 0.018f));
            var inner = CreateCard("Badge inner field", root.transform, Vector2.zero,
                new Vector2(size * 0.79f, size * 0.79f), Hex("050A18"),
                Mathf.RoundToInt(size * 0.395f));
            inner.GetComponent<Image>().raycastTarget = false;
            AddGraphicOutline(inner.GetComponent<Image>(), new Color(accent.r, accent.g, accent.b, 0.56f),
                Mathf.Max(1f, size * 0.01f));

            var visual = new AchievementBadgeVisual
            {
                Root = root,
                Ring = root.GetComponent<Image>()
            };
            var texture = string.IsNullOrEmpty(definition.IconResource)
                ? null : Resources.Load<Texture2D>(definition.IconResource);
            if (texture != null)
            {
                visual.Art = MakeTextureImage("Badge art", inner.transform, texture, Vector2.zero,
                    new Vector2(size * 0.62f, size * 0.62f));
                visual.Art.color = locked ? new Color(0.35f, 0.39f, 0.48f, 0.72f) : Color.white;
                var aspect = visual.Art.GetComponent<AspectRatioFitter>();
                if (aspect != null)
                    aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }
            else
            {
                visual.Glyph = MakeText(inner.transform,
                    string.IsNullOrEmpty(definition.Glyph) ? definition.Number.ToString() : definition.Glyph,
                    Mathf.RoundToInt(size * 0.30f), FontStyle.Bold, accent, Vector2.zero,
                    new Vector2(size * 0.64f, size * 0.58f), TextAnchor.MiddleCenter, 1);
                AddGraphicOutline(visual.Glyph, Hex("01030A"), Mathf.Max(1f, size * 0.008f));
            }
            visual.Number = MakeText(root.transform, "#" + definition.Number,
                Mathf.RoundToInt(size * 0.085f), FontStyle.Bold, locked ? Hex("758197") : Color.white,
                new Vector2(0f, -size * 0.34f), new Vector2(size * 0.55f, size * 0.13f),
                TextAnchor.MiddleCenter, 1);
            return visual;
        }

        private async Task IncrementAchievementMetricAsync(string metric, long amount,
            bool keepMaximum = false)
        {
            if (amount <= 0L || realtimeAchievementPlayerReference == null)
                return;
            try
            {
                await realtimeAchievementPlayerReference.Child("achievementMetrics/" + metric)
                    .RunTransaction(value =>
                    {
                        var current = Math.Max(0L, DatabaseLong(value.Value, 0L));
                        value.Value = keepMaximum ? Math.Max(current, amount) : current + amount;
                        return TransactionResult.Success(value);
                    }, true);
                await realtimeAchievementPlayerReference.Child("achievementMetrics/updatedAt")
                    .SetValueAsync(ServerValue.Timestamp);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Achievement metric " + metric + " delayed: " + exception.Message);
            }
        }

        private void RecordAchievementRunTotals(long coins, long distanceMeters)
        {
            _ = IncrementAchievementMetricAsync("lifetimeCoinsCollected", Math.Max(0L, coins));
            _ = IncrementAchievementMetricAsync("totalDistanceMeters", Math.Max(0L, distanceMeters));
        }

        /// <summary>
        /// Gameplay reports the run-only values required by Born Survivor, Gravity Master and
        /// No Acrobatics. The server remains responsible for tier completion and badge unlocks.
        /// </summary>
        public void RecordAchievementRunTelemetry(long score, long runDurationSeconds,
            long ceilingDistanceMeters, long jumps, long rolls)
        {
            _ = IncrementAchievementMetricAsync("longestRunSeconds", Math.Max(0L, runDurationSeconds), true);
            _ = IncrementAchievementMetricAsync("ceilingDistanceMeters", Math.Max(0L, ceilingDistanceMeters));
            if (jumps <= 0L && rolls <= 0L)
                _ = IncrementAchievementMetricAsync("noAcrobaticsBestScore", Math.Max(0L, score), true);
        }

        /// <summary>
        /// Preferred one-call bridge for the runner's end-of-run event. It records the detailed
        /// mission signals, achievement-only signals and the existing wallet/statistics update
        /// in the correct order. Values are totals for the run that just ended.
        /// </summary>
        public void RecordCompletedRun(long score, long coinsCollected, float distanceMeters,
            long runDurationSeconds, long ceilingDistanceMeters, long leftLaneCoins,
            long middleLaneCoins, long rightLaneCoins, long magnetCoins, long jumps,
            long rolls, long laneChanges, long chestsOpened, long chestsCollected,
            bool usedRevive, bool usedPowerup, bool diedWithinFirst60Seconds)
        {
            var distance = Math.Max(0L, (long)Math.Round(distanceMeters));
            RecordMissionRunTelemetry(leftLaneCoins, middleLaneCoins, rightLaneCoins,
                magnetCoins, jumps, rolls, laneChanges, chestsOpened, chestsCollected,
                Math.Max(0L, coinsCollected), distance, usedRevive, usedPowerup,
                diedWithinFirst60Seconds);
            RecordAchievementRunTelemetry(score, runDurationSeconds, ceilingDistanceMeters,
                jumps, rolls);
            RecordRunStatistics(score, coinsCollected, distanceMeters);
        }

        public void RecordAchievementCrystalsCollected(long amount)
            => _ = IncrementAchievementMetricAsync("crystalsCollected", Math.Max(0L, amount));

        private void RecordAchievementMagnetCoins(long amount)
            => _ = IncrementAchievementMetricAsync("magnetCoinsCollected", Math.Max(0L, amount));

        private void RecordAchievementCrystalRevive()
            => _ = IncrementAchievementMetricAsync("crystalRevives", 1L);

        private static string FormatAchievementNumber(long value)
        {
            if (value >= 1000000L)
                return (value / 1000000f).ToString("0.#") + "M";
            if (value >= 1000L)
                return (value / 1000f).ToString("0.#") + "K";
            return value.ToString("N0");
        }
    }
}
