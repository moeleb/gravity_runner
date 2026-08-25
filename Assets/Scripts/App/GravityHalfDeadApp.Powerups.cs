using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private DatabaseReference realtimePowerupPlayerReference;
        private string realtimePowerupUserId = string.Empty;
        private bool powerupPurchaseInFlight;

        private void RefreshPowerupUI()
        {
            if (gameCoinAmountText != null)
                gameCoinAmountText.text = bootstrapState.Coins.ToString("N0");
            if (mePowerupWalletText == null)
                return;
            mePowerupWalletText.text = bootstrapState.Coins.ToString("N0");
            mePowerupWalletText.color = Cream;
            for (var i = 0; i < PowerupIds.Length; i++)
            {
                var level = bootstrapState.PowerupLevels[i];
                if (mePowerupDurationTexts[i] != null)
                    mePowerupDurationTexts[i].text = PowerupBenefitLabel(i, level);
                var maxLevel = PowerupMaxLevel(i);
                var maxed = level >= maxLevel;
                var cost = PowerupCostForLevel(i, level);
                var canAfford = bootstrapState.Coins >= cost;
                for (var segment = 0; segment < maxLevel; segment++)
                {
                    if (mePowerupSegments[i, segment] != null)
                    {
                        mePowerupSegments[i, segment].color = segment < level
                            ? Hex("35CFFF")
                            : segment == level && !maxed && canAfford
                                ? Hex("56E75B")
                                : Hex("343A49");
                    }
                }

                if (mePowerupCostTexts[i] != null)
                {
                    mePowerupCostTexts[i].text = maxed ? "MAX" : cost.ToString("N0");
                    mePowerupCostTexts[i].color = maxed ? Muted : Cream;
                }
                if (mePowerupButtons[i] != null)
                {
                    mePowerupButtons[i].interactable = !maxed && !powerupPurchaseInFlight && canAfford;
                    var buttonImage = mePowerupButtons[i].GetComponent<Image>();
                    if (buttonImage != null)
                    {
                        var accent = PowerupAccentColor(i);
                        buttonImage.color = maxed
                            ? Hex("26334B")
                            : canAfford ? Color.Lerp(Hex("06101E"), accent, 0.48f) : Hex("28313F");
                    }
                    if (mePowerupButtonLabelTexts[i] != null)
                    {
                        mePowerupButtonLabelTexts[i].text = maxed ? "MAXED" : "UPGRADE";
                        mePowerupButtonLabelTexts[i].color = maxed || !canAfford
                            ? Hex("A6AFBC") : PowerupAccentColor(i);
                    }
                }
            }
        }

        private async void UpgradePowerup(string powerupId)
        {
            var index = Array.IndexOf(PowerupIds, powerupId);
            if (index < 0 || powerupPurchaseInFlight)
                return;
            var level = bootstrapState.PowerupLevels[index];
            var maxLevel = PowerupMaxLevel(index);
            if (level >= maxLevel)
                return;
            var cost = PowerupCostForLevel(index, level);
            if (bootstrapState.Coins < cost)
            {
                RefreshPowerupUI();
                return;
            }

            if (realtimePowerupPlayerReference == null || auth == null || auth.CurrentUser == null)
            {
                SetPowerupStatus("REALTIME SYNC IS NOT READY", Hex("FF82C8"));
                return;
            }

            powerupPurchaseInFlight = true;
            RefreshPowerupUI();
            var purchaseResult = "SYNC_ERROR";
            var paidCost = 0L;
            var finalStatus = string.Empty;
            var finalStatusColor = Muted;
            try
            {
                await realtimePowerupPlayerReference.RunTransaction(mutablePlayer =>
                {
                    var mutableCoins = mutablePlayer.Child("coins");
                    var mutableLevel = mutablePlayer.Child("upgrades/" + powerupId + "/level");
                    var currentCoins = DatabaseLong(mutableCoins.Value, bootstrapState.Coins);
                    var currentLevel = Mathf.Clamp((int)DatabaseLong(mutableLevel.Value, level), 0, maxLevel);
                    if (currentLevel >= maxLevel)
                    {
                        purchaseResult = "MAX";
                        return TransactionResult.Abort();
                    }

                    var transactionCost = PowerupCostForLevel(index, currentLevel);
                    if (currentCoins < transactionCost)
                    {
                        cost = transactionCost;
                        purchaseResult = "NOT_ENOUGH";
                        return TransactionResult.Abort();
                    }

                    mutableCoins.Value = currentCoins - transactionCost;
                    mutableLevel.Value = (long)(currentLevel + 1);
                    mutablePlayer.Child("upgrades/" + powerupId + "/name").Value = PowerupNames[index];
                    mutablePlayer.Child("upgrades/" + powerupId + "/updatedAt").Value = ServerValue.Timestamp;
                    mutablePlayer.Child("updatedAt").Value = ServerValue.Timestamp;
                    paidCost = transactionCost;
                    purchaseResult = "SUCCESS";
                    return TransactionResult.Success(mutablePlayer);
                }, true);

                if (purchaseResult == "NOT_ENOUGH")
                {
                    // The real-time balance can be newer than the local balance. Keep the wallet
                    // intact and let the listener disable the button instead of showing an overlay.
                    finalStatus = string.Empty;
                }
                else if (purchaseResult == "MAX")
                    finalStatus = PowerupNames[index] + " IS ALREADY MAXED";
                else if (purchaseResult == "SUCCESS")
                {
                    finalStatus = PowerupNames[index] + " UPGRADED";
                    finalStatusColor = PowerupAccentColor(index);
                    _ = SyncPowerupsToLegacyFirestoreAsync();
                    ReportMissionProgress("upgrade_something", 1L);
                    if (paidCost > 0L)
                        ReportMissionProgress("spend_coins", paidCost);
                }
            }
            catch (Exception exception)
            {
                finalStatus = "UPGRADE COULD NOT SYNC · TRY AGAIN";
                finalStatusColor = Hex("FF82C8");
                Debug.LogWarning("Realtime power-up upgrade failed: " + exception.Message);
            }
            finally
            {
                powerupPurchaseInFlight = false;
                RefreshPowerupUI();
                if (!string.IsNullOrEmpty(finalStatus))
                    SetPowerupStatus(finalStatus, finalStatusColor);
            }
        }

        private async Task StartPowerupRealtimeSyncAsync()
        {
            StopPowerupRealtimeSync();
            if (realtimeDatabase == null || auth == null || auth.CurrentUser == null)
                return;

            realtimePowerupUserId = auth.CurrentUser.UserId;
            realtimePowerupPlayerReference = realtimeDatabase.RootReference
                .Child("players").Child(realtimePowerupUserId);
            realtimePowerupPlayerReference.ValueChanged += HandlePowerupRealtimeValueChanged;

            try
            {
                var snapshot = await realtimePowerupPlayerReference.GetValueAsync();
                if (!snapshot.Exists)
                    await realtimePowerupPlayerReference.SetValueAsync(BuildPowerupRealtimeSeed());
                else
                {
                    ApplyPowerupRealtimeSnapshot(snapshot);
                    if (!snapshot.Child("highScore").Exists)
                        await realtimePowerupPlayerReference.Child("highScore")
                            .SetValueAsync(Math.Max(0L, bootstrapState.EndlessHighScore));
                    if (!snapshot.Child("gravityCores").Exists)
                        await realtimePowerupPlayerReference.Child("gravityCores")
                            .SetValueAsync(Math.Max(0L, bootstrapState.GravityCores));
                }
            }
            catch (Exception exception)
            {
                SetPowerupStatus("OFFLINE PROGRESSION · RETRYING SYNC", Muted);
                Debug.LogWarning("Realtime power-up initialization delayed: " + exception.Message);
            }
        }

        private void StopPowerupRealtimeSync()
        {
            if (realtimePowerupPlayerReference != null)
                realtimePowerupPlayerReference.ValueChanged -= HandlePowerupRealtimeValueChanged;
            realtimePowerupPlayerReference = null;
            realtimePowerupUserId = string.Empty;
        }

        private void HandlePowerupRealtimeValueChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                SetPowerupStatus("REALTIME SYNC ERROR · USING LOCAL STATE", Hex("FF82C8"));
                Debug.LogWarning("Realtime power-up listener: " + args.DatabaseError.Message);
                return;
            }
            if (args.Snapshot == null || !args.Snapshot.Exists || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != realtimePowerupUserId)
                return;
            ApplyPowerupRealtimeSnapshot(args.Snapshot);
        }

        private void ApplyPowerupRealtimeSnapshot(DataSnapshot snapshot)
        {
            var coinSnapshot = snapshot.Child("coins");
            if (coinSnapshot.Exists)
                bootstrapState.Coins = Math.Max(0L, DatabaseLong(coinSnapshot.Value, bootstrapState.Coins));
            var highScoreSnapshot = snapshot.Child("highScore");
            if (highScoreSnapshot.Exists)
                bootstrapState.EndlessHighScore = Math.Max(0L,
                    DatabaseLong(highScoreSnapshot.Value, bootstrapState.EndlessHighScore));
            var gravityCoreSnapshot = snapshot.Child("gravityCores");
            if (gravityCoreSnapshot.Exists)
                bootstrapState.GravityCores = Math.Max(0L,
                    DatabaseLong(gravityCoreSnapshot.Value, bootstrapState.GravityCores));

            for (var i = 0; i < PowerupIds.Length; i++)
            {
                var levelSnapshot = snapshot.Child("upgrades/" + PowerupIds[i] + "/level");
                if (levelSnapshot.Exists)
                    bootstrapState.PowerupLevels[i] = Mathf.Clamp(
                        (int)DatabaseLong(levelSnapshot.Value, bootstrapState.PowerupLevels[i]),
                        0, PowerupMaxLevel(i));
            }
            RefreshPowerupUI();
            RefreshHomeHeaderDynamicValues();
        }

        private Dictionary<string, object> BuildPowerupRealtimeSeed()
        {
            var upgrades = new Dictionary<string, object>();
            for (var i = 0; i < PowerupIds.Length; i++)
            {
                upgrades[PowerupIds[i]] = new Dictionary<string, object>
                {
                    { "name", PowerupNames[i] },
                    { "level", (long)bootstrapState.PowerupLevels[i] },
                    { "updatedAt", ServerValue.Timestamp }
                };
            }
            return new Dictionary<string, object>
            {
                { "playerId", bootstrapState.PlayerId },
                { "coins", bootstrapState.Coins },
                { "gravityCores", Math.Max(0L, bootstrapState.GravityCores) },
                { "highScore", Math.Max(0L, bootstrapState.EndlessHighScore) },
                { "upgrades", upgrades },
                { "updatedAt", ServerValue.Timestamp }
            };
        }

        private async Task SyncPowerupsToLegacyFirestoreAsync()
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "coins", bootstrapState.Coins },
                        { "powerups", PowerupDictionary() },
                        { "powerups_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Legacy Firestore power-up mirror delayed: " + exception.Message);
            }
        }

        public float GetPowerupDurationSeconds(string powerupId, float baseDurationSeconds)
        {
            if (baseDurationSeconds <= 0f)
                return 0f;
            var timezoneIndex = Array.IndexOf(PowerupIds, "timezone");
            var timezoneLevel = timezoneIndex >= 0 ? bootstrapState.PowerupLevels[timezoneIndex] : 0;
            return baseDurationSeconds * (1f + timezoneLevel * 0.10f);
        }

        public void RecordPowerupCollected(string powerupId)
        {
            var powerupIndex = Array.IndexOf(PowerupIds, powerupId);
            if (powerupIndex < 0 && !string.Equals(powerupId, "coin_booster",
                    StringComparison.Ordinal))
                return;
            if (powerupIndex >= 0 && realtimePowerupPlayerReference != null)
                _ = IncrementPowerupCollectibleAsync(powerupId);
            ReportPowerupMissionCollected(powerupId);
        }

        private async Task SetRealtimeCoinBalanceAsync(long coinBalance)
        {
            if (realtimePowerupPlayerReference == null)
                return;
            try
            {
                await realtimePowerupPlayerReference.Child("coins").SetValueAsync(Math.Max(0L, coinBalance));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Realtime coin balance sync delayed: " + exception.Message);
            }
        }

        private async Task AddRealtimeCoinsAsync(long amount)
        {
            if (amount <= 0L || realtimePowerupPlayerReference == null)
                return;
            try
            {
                await realtimePowerupPlayerReference.Child("coins").RunTransaction(mutableCoins =>
                {
                    mutableCoins.Value = Math.Max(0L, DatabaseLong(mutableCoins.Value, 0L) + amount);
                    return TransactionResult.Success(mutableCoins);
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Realtime run coin reward sync delayed: " + exception.Message);
            }
        }

        private async Task SetRealtimeHighScoreAsync(long highScore)
        {
            if (realtimePowerupPlayerReference == null)
                return;
            try
            {
                await realtimePowerupPlayerReference.Child("highScore").RunTransaction(mutableScore =>
                {
                    mutableScore.Value = Math.Max(DatabaseLong(mutableScore.Value, 0L), highScore);
                    return TransactionResult.Success(mutableScore);
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Realtime high score sync delayed: " + exception.Message);
            }
        }

        private async Task IncrementPowerupCollectibleAsync(string powerupId)
        {
            try
            {
                var collectibleReference = realtimePowerupPlayerReference.Child("collectibles").Child(powerupId);
                await collectibleReference.RunTransaction(mutableCollectible =>
                {
                    var count = DatabaseLong(mutableCollectible.Child("totalCollected").Value, 0L);
                    mutableCollectible.Child("totalCollected").Value = count + 1L;
                    mutableCollectible.Child("lastCollectedAt").Value = ServerValue.Timestamp;
                    return TransactionResult.Success(mutableCollectible);
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Power-up collectible count sync delayed: " + exception.Message);
            }
        }

        private void SetPowerupStatus(string message, Color color)
        {
            _ = color;
            Debug.LogWarning(message);
            RefreshPowerupUI();
        }

        private static long DatabaseLong(object value, long fallback)
        {
            if (value is long longValue)
                return longValue;
            if (value is int intValue)
                return intValue;
            if (value is double doubleValue)
                return (long)Math.Round(doubleValue);
            return value != null && long.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private static long PowerupCostForLevel(int powerupIndex, int currentLevel)
        {
            var levelIndex = Mathf.Clamp(currentLevel, 0, 5);
            return PowerupIds[powerupIndex] == "timezone"
                ? TimezoneUpgradeCosts[levelIndex]
                : PowerupUpgradeCosts[levelIndex];
        }

        private static int PowerupMaxLevel(int powerupIndex)
            => PowerupIds[powerupIndex] == "timezone" ? 3 : 6;

        private static Color PowerupAccentColor(int powerupIndex)
        {
            return powerupIndex switch
            {
                0 => Hex("18BFFF"),
                1 => Hex("54EA4B"),
                2 => Hex("FFD22F"),
                3 => Hex("FF4D4D"),
                4 => Hex("A857FF"),
                _ => Hex("FFB52D")
            };
        }

        private static string PowerupBenefitLabel(int powerupIndex, int level)
        {
            if (PowerupIds[powerupIndex] == "timezone")
                return "+" + (level * 10) + "% ALL DURATIONS";
            return level <= 0 ? "BASE EFFECT" : PowerupDurations[Mathf.Clamp(level, 0, 6)] + "s EFFECT";
        }

        private Dictionary<string, object> PowerupDictionary()
        {
            var values = new Dictionary<string, object>();
            for (var i = 0; i < PowerupIds.Length; i++)
                values[PowerupIds[i]] = (long)bootstrapState.PowerupLevels[i];
            return values;
        }
    }
}
