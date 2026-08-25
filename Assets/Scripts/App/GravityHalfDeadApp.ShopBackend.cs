using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private const string ShopAdPlacementPrefix = "shop_booster_";
        private readonly long[] shopBoosterInventory = new long[BoosterCount];
        private readonly int[] shopBoosterDailyAdCounts = new int[BoosterCount];
        private readonly string[] shopBoosterDailyAdLocalDates = new string[BoosterCount];
        private DatabaseReference realtimeShopPlayerReference;
        private string realtimeShopUserId = string.Empty;
        private bool shopTransactionInFlight;
        private Coroutine shopLocalMidnightMonitor;
        private IGravityRewardedAdProvider rewardedAdProvider;

        /// <summary>
        /// Register the project's rewarded-ad SDK adapter after it initializes.
        /// Registering null safely disables every WATCH AD button.
        /// </summary>
        public void RegisterRewardedAdProvider(IGravityRewardedAdProvider provider)
        {
            rewardedAdProvider = provider;
            RefreshShopUI();
            RefreshMissionUI();
        }

        public void UnregisterRewardedAdProvider(IGravityRewardedAdProvider provider)
        {
            if (ReferenceEquals(rewardedAdProvider, provider))
                rewardedAdProvider = null;
            RefreshShopUI();
            RefreshMissionUI();
        }

        private async Task StartShopRealtimeSyncAsync()
        {
            StopShopRealtimeSync();
            if (realtimeDatabase == null || auth == null || auth.CurrentUser == null)
                return;

            realtimeShopUserId = auth.CurrentUser.UserId;
            realtimeShopPlayerReference = realtimeDatabase.RootReference
                .Child("players").Child(realtimeShopUserId);
            realtimeShopPlayerReference.ValueChanged += HandleShopRealtimeValueChanged;

            try
            {
                var snapshot = await realtimeShopPlayerReference.GetValueAsync();
                if (snapshot.Exists)
                    ApplyShopRealtimeSnapshot(snapshot);
                await EnsureShopRealtimeDefaultsAsync(snapshot);
                await ResetDailyAdsIfNeededAsync();
                StartShopLocalMidnightMonitor();
            }
            catch (Exception exception)
            {
                SetShopStatus("SHOP SYNC DELAYED · INVENTORY IS READ-ONLY", Muted);
                Debug.LogWarning("Realtime booster shop initialization delayed: " + exception.Message);
            }
        }

        private void StopShopRealtimeSync()
        {
            if (realtimeShopPlayerReference != null)
                realtimeShopPlayerReference.ValueChanged -= HandleShopRealtimeValueChanged;
            realtimeShopPlayerReference = null;
            realtimeShopUserId = string.Empty;
            if (shopLocalMidnightMonitor != null)
            {
                StopCoroutine(shopLocalMidnightMonitor);
                shopLocalMidnightMonitor = null;
            }
        }

        private void HandleShopRealtimeValueChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                SetShopStatus("REALTIME SHOP SYNC DELAYED", Muted);
                Debug.LogWarning("Realtime booster shop listener: " + args.DatabaseError.Message);
                return;
            }
            if (args.Snapshot == null || !args.Snapshot.Exists || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != realtimeShopUserId)
                return;

            ApplyShopRealtimeSnapshot(args.Snapshot);
        }

        private void ApplyShopRealtimeSnapshot(DataSnapshot snapshot)
        {
            var coins = snapshot.Child("coins");
            if (coins.Exists)
                bootstrapState.Coins = Math.Max(0L, DatabaseLong(coins.Value, bootstrapState.Coins));

            for (var i = 0; i < BoosterCount; i++)
            {
                var inventory = snapshot.Child("boosters/" + BoosterIds[i] + "/inventory");
                if (inventory.Exists)
                    shopBoosterInventory[i] = Math.Max(0L, DatabaseLong(inventory.Value, 0L));
            }

            var today = ShopLocalDateKey();
            var dailyResetNeeded = false;
            for (var i = 0; i < BoosterCount; i++)
            {
                var dailyPath = "dailyAds/boosters/" + BoosterIds[i];
                var dailyCount = snapshot.Child(dailyPath + "/count");
                var localDate = snapshot.Child(dailyPath + "/localDate");
                shopBoosterDailyAdCounts[i] = Mathf.Clamp(
                    (int)DatabaseLong(dailyCount.Value, 0L), 0, DailyRewardedAdLimit);
                shopBoosterDailyAdLocalDates[i] = localDate.Exists
                    ? localDate.Value?.ToString() ?? string.Empty
                    : string.Empty;
                if (string.Equals(shopBoosterDailyAdLocalDates[i], today, StringComparison.Ordinal))
                    continue;

                // Display the new day immediately. The transaction below persists each reset.
                shopBoosterDailyAdCounts[i] = 0;
                dailyResetNeeded = true;
            }

            if (dailyResetNeeded)
                _ = ResetDailyAdsIfNeededAsync();

            if (gameCoinAmountText != null)
                gameCoinAmountText.text = bootstrapState.Coins.ToString("N0");
            RefreshPowerupUI();
            RefreshShopUI();
        }

        private async Task EnsureShopRealtimeDefaultsAsync(DataSnapshot snapshot)
        {
            if (realtimeShopPlayerReference == null)
                return;

            var updates = new Dictionary<string, object>();
            if (snapshot == null || !snapshot.Child("playerId").Exists)
                updates["playerId"] = bootstrapState.PlayerId;
            if (snapshot == null || !snapshot.Child("coins").Exists)
                updates["coins"] = Math.Max(0L, bootstrapState.Coins);

            for (var i = 0; i < BoosterCount; i++)
            {
                var basePath = "boosters/" + BoosterIds[i];
                if (snapshot == null || !snapshot.Child(basePath + "/inventory").Exists)
                    updates[basePath + "/inventory"] = 0L;
                if (snapshot == null || !snapshot.Child(basePath + "/updatedAt").Exists)
                    updates[basePath + "/updatedAt"] = ServerValue.Timestamp;
            }

            for (var i = 0; i < BoosterCount; i++)
            {
                var dailyPath = "dailyAds/boosters/" + BoosterIds[i];
                if (snapshot == null || !snapshot.Child(dailyPath + "/count").Exists)
                    updates[dailyPath + "/count"] = 0L;
                if (snapshot == null || !snapshot.Child(dailyPath + "/localDate").Exists)
                    updates[dailyPath + "/localDate"] = ShopLocalDateKey();
                if (snapshot == null || !snapshot.Child(dailyPath + "/timezoneOffsetMinutes").Exists)
                    updates[dailyPath + "/timezoneOffsetMinutes"] = ShopTimezoneOffsetMinutes();
                if (snapshot == null || !snapshot.Child(dailyPath + "/lastResetAt").Exists)
                    updates[dailyPath + "/lastResetAt"] = ServerValue.Timestamp;
            }

            // Remove the obsolete shared counter after the per-booster records exist.
            if (snapshot != null && snapshot.Child("dailyAds/count").Exists)
                updates["dailyAds/count"] = null;
            if (snapshot != null && snapshot.Child("dailyAds/localDate").Exists)
                updates["dailyAds/localDate"] = null;
            if (snapshot != null && snapshot.Child("dailyAds/timezoneOffsetMinutes").Exists)
                updates["dailyAds/timezoneOffsetMinutes"] = null;
            if (snapshot != null && snapshot.Child("dailyAds/lastResetAt").Exists)
                updates["dailyAds/lastResetAt"] = null;
            if (snapshot != null && snapshot.Child("dailyAds/lastWatchAt").Exists)
                updates["dailyAds/lastWatchAt"] = null;

            if (updates.Count > 0)
                await realtimeShopPlayerReference.UpdateChildrenAsync(updates);
        }

        private void StartShopLocalMidnightMonitor()
        {
            if (shopLocalMidnightMonitor != null)
                StopCoroutine(shopLocalMidnightMonitor);
            shopLocalMidnightMonitor = StartCoroutine(MonitorShopLocalMidnight());
        }

        private IEnumerator MonitorShopLocalMidnight()
        {
            while (true)
            {
                var now = DateTime.Now;
                var secondsToMidnight = Math.Max(0.05d, (now.Date.AddDays(1d) - now).TotalSeconds);
                // Re-evaluate at least once per minute so manual clock/timezone changes are respected,
                // then use the exact remaining interval for the final wait before local midnight.
                yield return new WaitForSecondsRealtime((float)Math.Min(60d, secondsToMidnight + 0.05d));
                var today = ShopLocalDateKey();
                var resetNeeded = false;
                for (var i = 0; i < BoosterCount; i++)
                {
                    if (string.Equals(shopBoosterDailyAdLocalDates[i], today,
                            StringComparison.Ordinal))
                        continue;
                    shopBoosterDailyAdCounts[i] = 0;
                    resetNeeded = true;
                }
                if (resetNeeded)
                {
                    RefreshShopUI();
                    _ = ResetDailyAdsIfNeededAsync();
                }
            }
        }

        private async Task ResetDailyAdsIfNeededAsync()
        {
            if (realtimeShopPlayerReference == null)
                return;

            var localDate = ShopLocalDateKey();
            var reset = false;
            try
            {
                var resetResult = await realtimeShopPlayerReference.Child("dailyAds/boosters")
                    .RunTransaction(mutableBoosterAds =>
                {
                    reset = false;
                    for (var i = 0; i < BoosterCount; i++)
                    {
                        var dailyAds = mutableBoosterAds.Child(BoosterIds[i]);
                        var storedDate = dailyAds.Child("localDate").Value?.ToString() ?? string.Empty;
                        if (string.Equals(storedDate, localDate, StringComparison.Ordinal))
                            continue;

                        dailyAds.Child("count").Value = 0L;
                        dailyAds.Child("localDate").Value = localDate;
                        dailyAds.Child("timezoneOffsetMinutes").Value = ShopTimezoneOffsetMinutes();
                        dailyAds.Child("lastResetAt").Value = ServerValue.Timestamp;
                        reset = true;
                    }

                    if (!reset)
                        return TransactionResult.Abort();
                    return TransactionResult.Success(mutableBoosterAds);
                }, true);
                if (reset)
                {
                    for (var i = 0; i < BoosterCount; i++)
                    {
                        var boosterAds = resetResult?.Child(BoosterIds[i]);
                        if (boosterAds == null || !boosterAds.Exists)
                            continue;
                        shopBoosterDailyAdCounts[i] = Mathf.Clamp(
                            (int)DatabaseLong(boosterAds.Child("count").Value, 0L),
                            0, DailyRewardedAdLimit);
                        shopBoosterDailyAdLocalDates[i] =
                            boosterAds.Child("localDate").Value?.ToString() ?? localDate;
                    }
                    RefreshShopUI();
                    _ = MirrorShopToFirestoreAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Daily rewarded-ad reset delayed: " + exception.Message);
            }
        }

        private async void PurchaseBoosterWithCoins(int index)
        {
            if (index < 0 || index >= BoosterCount || shopTransactionInFlight
                || realtimeShopPlayerReference == null || auth == null || auth.CurrentUser == null)
                return;
            if (bootstrapState.Coins < BoosterCoinCosts[index])
            {
                RefreshShopUI();
                return;
            }

            shopTransactionInFlight = true;
            RefreshShopUI();
            var purchased = false;
            try
            {
                var cost = BoosterCoinCosts[index];
                var result = await realtimeShopPlayerReference.RunTransaction(mutablePlayer =>
                {
                    purchased = false;
                    var coins = mutablePlayer.Child("coins");
                    var inventory = mutablePlayer.Child("boosters/" + BoosterIds[index] + "/inventory");
                    var currentCoins = Math.Max(0L, DatabaseLong(coins.Value, bootstrapState.Coins));
                    if (currentCoins < cost)
                        return TransactionResult.Abort();

                    coins.Value = currentCoins - cost;
                    inventory.Value = Math.Max(0L, DatabaseLong(inventory.Value, 0L)) + 1L;
                    mutablePlayer.Child("boosters/" + BoosterIds[index] + "/updatedAt").Value =
                        ServerValue.Timestamp;
                    mutablePlayer.Child("updatedAt").Value = ServerValue.Timestamp;
                    purchased = true;
                    return TransactionResult.Success(mutablePlayer);
                }, true);

                if (purchased)
                {
                    if (result != null && result.Exists)
                        ApplyShopRealtimeSnapshot(result);
                    SetShopStatus(BoosterTitles[index] + " ADDED TO INVENTORY", BoosterAccent(index));
                    _ = MirrorShopToFirestoreAsync();
                    ReportMissionProgress("spend_coins", BoosterCoinCosts[index]);
                }
            }
            catch (Exception exception)
            {
                SetShopStatus("PURCHASE SYNC DELAYED · NO COINS WERE SPENT", Muted);
                Debug.LogWarning("Booster coin purchase failed: " + exception.Message);
            }
            finally
            {
                shopTransactionInFlight = false;
                RefreshShopUI();
            }
        }

        private void WatchBoosterAd(int index)
        {
            if (index < 0 || index >= BoosterCount || shopTransactionInFlight
                || shopBoosterDailyAdCounts[index] >= DailyRewardedAdLimit)
                return;

            var placementId = ShopAdPlacementPrefix + BoosterIds[index];
            if (rewardedAdProvider == null || !rewardedAdProvider.IsRewardedAdReady(placementId))
            {
                SetShopStatus("REWARDED AD IS NOT READY YET", Muted);
                RefreshShopUI();
                return;
            }

            shopTransactionInFlight = true;
            SetShopStatus("WATCH THE FULL AD TO CLAIM YOUR BOOSTER", BoosterAccent(index));
            RefreshShopUI();
            try
            {
                rewardedAdProvider.ShowRewardedAd(placementId, completed =>
                {
                    if (completed)
                        _ = ClaimRewardedBoosterAsync(index);
                    else
                    {
                        shopTransactionInFlight = false;
                        SetShopStatus("AD CLOSED · NO DAILY USE OR BOOSTER WAS SPENT", Muted);
                        RefreshShopUI();
                    }
                });
            }
            catch (Exception exception)
            {
                shopTransactionInFlight = false;
                SetShopStatus("REWARDED AD COULD NOT OPEN", Muted);
                RefreshShopUI();
                Debug.LogWarning("Rewarded-ad provider failed: " + exception.Message);
            }
        }

        private async Task ClaimRewardedBoosterAsync(int index)
        {
            var granted = false;
            try
            {
                if (realtimeShopPlayerReference == null)
                    return;
                var localDate = ShopLocalDateKey();
                var result = await realtimeShopPlayerReference.RunTransaction(mutablePlayer =>
                {
                    granted = false;
                    var dailyAds = mutablePlayer.Child("dailyAds/boosters/" + BoosterIds[index]);
                    var storedDate = dailyAds.Child("localDate").Value?.ToString() ?? string.Empty;
                    var dateChanged = !string.Equals(storedDate, localDate, StringComparison.Ordinal);
                    var currentCount = Mathf.Clamp((int)DatabaseLong(dailyAds.Child("count").Value, 0L),
                        0, DailyRewardedAdLimit);
                    if (dateChanged)
                        currentCount = 0;
                    if (currentCount >= DailyRewardedAdLimit)
                        return TransactionResult.Abort();

                    var inventory = mutablePlayer.Child("boosters/" + BoosterIds[index] + "/inventory");
                    inventory.Value = Math.Max(0L, DatabaseLong(inventory.Value, 0L)) + 1L;
                    dailyAds.Child("count").Value = (long)(currentCount + 1);
                    dailyAds.Child("localDate").Value = localDate;
                    dailyAds.Child("timezoneOffsetMinutes").Value = ShopTimezoneOffsetMinutes();
                    if (dateChanged)
                        dailyAds.Child("lastResetAt").Value = ServerValue.Timestamp;
                    dailyAds.Child("lastWatchAt").Value = ServerValue.Timestamp;
                    mutablePlayer.Child("boosters/" + BoosterIds[index] + "/updatedAt").Value =
                        ServerValue.Timestamp;
                    mutablePlayer.Child("updatedAt").Value = ServerValue.Timestamp;
                    granted = true;
                    return TransactionResult.Success(mutablePlayer);
                }, true);

                if (granted)
                {
                    if (result != null && result.Exists)
                        ApplyShopRealtimeSnapshot(result);
                    SetShopStatus(BoosterTitles[index] + " REWARD CLAIMED", BoosterAccent(index));
                    _ = MirrorShopToFirestoreAsync();
                    ReportMissionProgress("watch_rewarded_ads", 1L);
                }
                else
                    SetShopStatus("DAILY AD LIMIT REACHED · RETURNS AT LOCAL MIDNIGHT", Muted);
            }
            catch (Exception exception)
            {
                SetShopStatus("REWARD SYNC DELAYED · PLEASE TRY AGAIN", Muted);
                Debug.LogWarning("Rewarded booster claim failed: " + exception.Message);
            }
            finally
            {
                shopTransactionInFlight = false;
                RefreshShopUI();
            }
        }

        private void RefreshShopUI()
        {
            RefreshGravityDiscHeader();
            for (var i = 0; i < BoosterCount; i++)
            {
                var accent = BoosterAccent(i);
                var adCount = shopBoosterDailyAdCounts[i];
                if (shopBoosterInventoryTexts[i] != null)
                    shopBoosterInventoryTexts[i].text = "YOU HAVE: " + shopBoosterInventory[i].ToString("N0");
                if (shopBoosterCostTexts[i] != null)
                    shopBoosterCostTexts[i].text = BoosterCoinCosts[i].ToString("N0");

                var canBuy = !shopTransactionInFlight && realtimeShopPlayerReference != null
                    && bootstrapState.Coins >= BoosterCoinCosts[i];
                if (shopBoosterCoinButtons[i] != null)
                {
                    shopBoosterCoinButtons[i].interactable = canBuy;
                    var image = shopBoosterCoinButtons[i].GetComponent<Image>();
                    if (image != null)
                        image.color = canBuy ? Color.Lerp(Hex("071123"), accent, 0.18f) : Hex("252C38");
                }
                if (shopBoosterCostTexts[i] != null)
                    shopBoosterCostTexts[i].color = canBuy ? Color.white : Hex("9299A5");

                var placementId = ShopAdPlacementPrefix + BoosterIds[i];
                var providerReady = rewardedAdProvider != null
                    && rewardedAdProvider.IsRewardedAdReady(placementId);
                var canWatch = !shopTransactionInFlight && realtimeShopPlayerReference != null
                    && adCount < DailyRewardedAdLimit && providerReady;
                if (shopBoosterAdButtons[i] != null)
                {
                    shopBoosterAdButtons[i].interactable = canWatch;
                    var image = shopBoosterAdButtons[i].GetComponent<Image>();
                    if (image != null)
                        image.color = canWatch ? Color.Lerp(Hex("071123"), accent, 0.28f) : Hex("252C38");
                }
                if (shopBoosterAdButtonTexts[i] != null)
                {
                    shopBoosterAdButtonTexts[i].text = adCount >= DailyRewardedAdLimit
                        ? "RESET TOMORROW"
                        : "▶  WATCH AD · " + adCount + "/" + DailyRewardedAdLimit;
                    shopBoosterAdButtonTexts[i].color = canWatch ? Color.white : Hex("9299A5");
                }
            }
        }

        private void RefreshGravityDiscHeader()
        {
            if (gameGravityDiscAmountText != null)
                gameGravityDiscAmountText.text = Math.Max(0L, shopBoosterInventory[0]).ToString("N0");
        }

        private void SetShopStatus(string message, Color color)
        {
            if (shopStatusText == null)
                return;
            shopStatusText.text = message ?? string.Empty;
            shopStatusText.color = color;
        }

        private async Task MirrorShopToFirestoreAsync()
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "coins", bootstrapState.Coins },
                        { "booster_inventory", BuildBoosterInventoryMap() },
                        { "daily_rewarded_ads", BuildDailyAdsFirestoreMap() },
                        { "booster_shop_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Firestore booster-shop mirror delayed: " + exception.Message);
            }
        }

        private Dictionary<string, object> BuildBoosterInventoryMap()
        {
            var inventory = new Dictionary<string, object>();
            for (var i = 0; i < BoosterCount; i++)
                inventory[BoosterIds[i]] = shopBoosterInventory[i];
            return inventory;
        }

        private Dictionary<string, object> BuildDailyAdsFirestoreMap()
        {
            var dailyAds = new Dictionary<string, object>();
            for (var i = 0; i < BoosterCount; i++)
            {
                dailyAds[BoosterIds[i]] = new Dictionary<string, object>
                {
                    { "count", (long)Mathf.Clamp(shopBoosterDailyAdCounts[i], 0, DailyRewardedAdLimit) },
                    {
                        "local_date",
                        string.IsNullOrEmpty(shopBoosterDailyAdLocalDates[i])
                            ? ShopLocalDateKey()
                            : shopBoosterDailyAdLocalDates[i]
                    },
                    { "timezone_offset_minutes", ShopTimezoneOffsetMinutes() }
                };
            }
            return dailyAds;
        }

        private static Dictionary<string, object> DefaultBoosterInventoryFirestoreMap()
        {
            var inventory = new Dictionary<string, object>();
            for (var i = 0; i < BoosterCount; i++)
                inventory[BoosterIds[i]] = 0L;
            return inventory;
        }

        private static Dictionary<string, object> DefaultDailyAdsFirestoreMap()
        {
            var dailyAds = new Dictionary<string, object>();
            for (var i = 0; i < BoosterCount; i++)
            {
                dailyAds[BoosterIds[i]] = new Dictionary<string, object>
                {
                    { "count", 0L },
                    { "local_date", ShopLocalDateKey() },
                    { "timezone_offset_minutes", ShopTimezoneOffsetMinutes() }
                };
            }
            return dailyAds;
        }

        private static string ShopLocalDateKey()
            => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static long ShopTimezoneOffsetMinutes()
            => (long)Math.Round(DateTimeOffset.Now.Offset.TotalMinutes);
    }
}
