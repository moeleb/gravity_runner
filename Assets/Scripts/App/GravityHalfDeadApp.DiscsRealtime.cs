using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private DatabaseReference realtimeDiscPlayerReference;
        private string realtimeDiscUserId = string.Empty;
        private bool discPurchaseInFlight;
        private readonly Dictionary<string, string> discIapPrices = new();

        private async Task StartDiscRealtimeSyncAsync()
        {
            StopDiscRealtimeSync();
            if (realtimeDatabase == null || auth == null || auth.CurrentUser == null)
                return;
            realtimeDiscUserId = auth.CurrentUser.UserId;
            realtimeDiscPlayerReference = realtimeDatabase.RootReference.Child("players").Child(realtimeDiscUserId);
            realtimeDiscPlayerReference.ValueChanged += HandleDiscRealtimeValueChanged;
            try
            {
                var snapshot = await realtimeDiscPlayerReference.GetValueAsync();
                if (!snapshot.Child("discs").Exists)
                    await SeedDiscRealtimeStateAsync();
                else
                {
                    ApplyDiscRealtimeSnapshot(snapshot);
                    await EnsureStarterDiscAsync(snapshot);
                }
            }
            catch (Exception exception)
            {
                SetDiscStatus("DISC SYNC DELAYED · USING LOCAL LOADOUT", true);
                Debug.LogWarning("Realtime disc initialization delayed: " + exception.Message);
            }
        }

        private void StopDiscRealtimeSync()
        {
            if (realtimeDiscPlayerReference != null)
                realtimeDiscPlayerReference.ValueChanged -= HandleDiscRealtimeValueChanged;
            realtimeDiscPlayerReference = null;
            realtimeDiscUserId = string.Empty;
        }

        private async Task SeedDiscRealtimeStateAsync()
        {
            if (realtimeDiscPlayerReference == null)
                return;
            var updates = new Dictionary<string, object>
            {
                { "discShards", bootstrapState.DiscShards },
                { "selectedDisc", IsDiscUnlocked(bootstrapState.SelectedDisc) ? bootstrapState.SelectedDisc : "core_runner" },
                { "updatedAt", ServerValue.Timestamp }
            };
            for (var i = 0; i < DiscCount; i++)
            {
                var id = DiscIds[i];
                updates[$"discs/{id}/name"] = DiscNames[i];
                updates[$"discs/{id}/unlocked"] = IsDiscUnlocked(id);
                updates[$"discs/{id}/unlockKind"] = DiscUnlockKinds[i].ToString().ToLowerInvariant();
                updates[$"discs/{id}/unlockCost"] = DiscUnlockCosts[i];
                updates[$"discs/{id}/updatedAt"] = ServerValue.Timestamp;
            }
            await realtimeDiscPlayerReference.UpdateChildrenAsync(updates);
        }

        private async Task EnsureStarterDiscAsync(DataSnapshot snapshot)
        {
            if (realtimeDiscPlayerReference == null || snapshot == null)
                return;
            var updates = new Dictionary<string, object>();
            if (!DatabaseBool(snapshot.Child("discs/core_runner/unlocked").Value))
            {
                updates["discs/core_runner/name"] = DiscNames[0];
                updates["discs/core_runner/unlocked"] = true;
                updates["discs/core_runner/unlockKind"] = "starter";
                updates["discs/core_runner/unlockCost"] = 0L;
                updates["discs/core_runner/updatedAt"] = ServerValue.Timestamp;
            }
            var selected = snapshot.Child("selectedDisc").Value?.ToString();
            if (string.IsNullOrWhiteSpace(selected) || Array.IndexOf(DiscIds, selected) < 0)
                updates["selectedDisc"] = "core_runner";
            if (updates.Count == 0)
                return;
            updates["updatedAt"] = ServerValue.Timestamp;
            await realtimeDiscPlayerReference.UpdateChildrenAsync(updates);
        }

        private void HandleDiscRealtimeValueChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                SetDiscStatus("DISC SYNC ERROR · LOCAL LOADOUT ACTIVE", true);
                return;
            }
            if (args.Snapshot == null || !args.Snapshot.Exists || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != realtimeDiscUserId)
                return;
            ApplyDiscRealtimeSnapshot(args.Snapshot);
        }

        private void ApplyDiscRealtimeSnapshot(DataSnapshot snapshot)
        {
            if (snapshot.Child("coins").Exists)
                bootstrapState.Coins = Math.Max(0L, DatabaseLong(snapshot.Child("coins").Value, bootstrapState.Coins));
            if (snapshot.Child("discShards").Exists)
                bootstrapState.DiscShards = Math.Max(0L,
                    DatabaseLong(snapshot.Child("discShards").Value, bootstrapState.DiscShards));

            var discs = snapshot.Child("discs");
            if (discs.Exists)
            {
                bootstrapState.UnlockedDiscs.Clear();
                bootstrapState.UnlockedDiscs.Add("core_runner");
                foreach (var id in DiscIds)
                {
                    if (DatabaseBool(discs.Child(id + "/unlocked").Value))
                        bootstrapState.UnlockedDiscs.Add(id);
                }
            }

            var selected = snapshot.Child("selectedDisc").Value?.ToString();
            if (!string.IsNullOrWhiteSpace(selected) && IsDiscUnlocked(selected)
                && Array.IndexOf(DiscIds, selected) >= 0)
            {
                bootstrapState.SelectedDisc = selected;
                GravityDiscLoadout.SelectedDiscId = selected;
                PlayerPrefs.SetString("ghd.selected_disc", selected);
                PlayerPrefs.Save();
            }
            RefreshDiscCollectionUI();
            RefreshMeProfileUI();
            RefreshHomeHeaderDynamicValues();
        }

        private async Task UnlockDiscWithCurrencyAsync(int index)
        {
            if (index <= 0 || index >= DiscCount || realtimeDiscPlayerReference == null || discPurchaseInFlight)
                return;
            var kind = DiscUnlockKinds[index];
            if (kind != DiscUnlockKind.Coins && kind != DiscUnlockKind.Shards)
                return;
            discPurchaseInFlight = true;
            RefreshDiscCollectionUI();
            var result = "SYNC_ERROR";
            try
            {
                await realtimeDiscPlayerReference.RunTransaction(player =>
                {
                    var disc = player.Child("discs/" + DiscIds[index]);
                    if (DatabaseBool(disc.Child("unlocked").Value))
                    {
                        result = "UNLOCKED";
                        return TransactionResult.Abort();
                    }
                    var walletPath = kind == DiscUnlockKind.Coins ? "coins" : "discShards";
                    var fallback = kind == DiscUnlockKind.Coins ? bootstrapState.Coins : bootstrapState.DiscShards;
                    var wallet = player.Child(walletPath);
                    var balance = DatabaseLong(wallet.Value, fallback);
                    if (balance < DiscUnlockCosts[index])
                    {
                        result = "NOT_ENOUGH";
                        return TransactionResult.Abort();
                    }
                    wallet.Value = balance - DiscUnlockCosts[index];
                    disc.Child("name").Value = DiscNames[index];
                    disc.Child("unlocked").Value = true;
                    disc.Child("unlockKind").Value = kind.ToString().ToLowerInvariant();
                    disc.Child("unlockCost").Value = DiscUnlockCosts[index];
                    disc.Child("unlockedAt").Value = ServerValue.Timestamp;
                    disc.Child("updatedAt").Value = ServerValue.Timestamp;
                    player.Child("selectedDisc").Value = DiscIds[index];
                    player.Child("updatedAt").Value = ServerValue.Timestamp;
                    result = "SUCCESS";
                    return TransactionResult.Success(player);
                }, true);
                if (result == "SUCCESS" || result == "UNLOCKED")
                {
                    bootstrapState.UnlockedDiscs.Add(DiscIds[index]);
                    bootstrapState.SelectedDisc = DiscIds[index];
                    previewDiscId = DiscIds[index];
                    GravityDiscLoadout.SelectedDiscId = DiscIds[index];
                    _ = SyncDiscStateToFirestoreAsync();
                }
            }
            catch (Exception exception)
            {
                SetDiscStatus("DISC UNLOCK COULD NOT SYNC · TRY AGAIN", true);
                Debug.LogWarning("Realtime disc unlock failed: " + exception.Message);
            }
            finally
            {
                discPurchaseInFlight = false;
                RefreshDiscCollectionUI();
            }
        }

        private async Task SetSelectedDiscRealtimeAsync(string discId)
        {
            if (realtimeDiscPlayerReference == null || !IsDiscUnlocked(discId))
                return;
            bootstrapState.SelectedDisc = discId;
            GravityDiscLoadout.SelectedDiscId = discId;
            PlayerPrefs.SetString("ghd.selected_disc", discId);
            PlayerPrefs.Save();
            RefreshDiscCollectionUI();
            try
            {
                await realtimeDiscPlayerReference.UpdateChildrenAsync(new Dictionary<string, object>
                {
                    { "selectedDisc", discId },
                    { "updatedAt", ServerValue.Timestamp }
                });
                _ = SyncDiscStateToFirestoreAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Selected disc sync delayed: " + exception.Message);
            }
        }

        private void ConfigureDiscIap()
        {
            if (storeIapProvider == null)
                storeIapProvider = gameObject.GetComponent<GravityIapStoreProvider>()
                                   ?? gameObject.AddComponent<GravityIapStoreProvider>();
            storeIapProvider.ConfigureNonConsumables(DiscStoreProductIds,
                OnDiscLocalizedPrice, FulfillDiscPurchaseAsync, OnDiscIapStatus);
        }

        private void OnDiscLocalizedPrice(string productId, string localizedPrice)
        {
            if (!string.IsNullOrWhiteSpace(productId) && !string.IsNullOrWhiteSpace(localizedPrice))
                discIapPrices[productId] = localizedPrice;
            RefreshDiscCollectionUI();
        }

        private void OnDiscIapStatus(string productId, string message, bool failed)
        {
            // Global store-catalog errors are logged, but never rendered across the collection.
            // The three store cards simply remain disabled with STORE PRICE until products exist.
            if (string.IsNullOrWhiteSpace(productId))
            {
                if (failed && !string.IsNullOrWhiteSpace(message))
                    Debug.LogWarning("Disc IAP catalog: " + message);
                return;
            }
            if (!string.IsNullOrWhiteSpace(productId))
                discPurchaseInFlight = false;
            SetDiscStatus(failed ? "PURCHASE NOT COMPLETED" : "DISC ADDED TO YOUR COLLECTION", failed);
            RefreshDiscCollectionUI();
        }

        private void BeginDiscPurchase(int index)
        {
            if (index < 0 || index >= DiscCount || storeIapProvider == null || discPurchaseInFlight)
                return;
            var productId = DiscStoreProductIds[index];
            if (!storeIapProvider.CanPurchase(productId))
            {
                SetDiscStatus("APP STORE PRICE IS STILL LOADING", true);
                return;
            }
            discPurchaseInFlight = true;
            RefreshDiscCollectionUI();
            storeIapProvider.Purchase(productId);
        }

        private async Task<bool> FulfillDiscPurchaseAsync(string productId, string transactionId,
            string receipt, string localizedPrice)
        {
            _ = receipt;
            var index = Array.IndexOf(DiscStoreProductIds, productId);
            if (index < 0 || auth == null || auth.CurrentUser == null || realtimeDiscPlayerReference == null
                || string.IsNullOrWhiteSpace(transactionId))
                return false;
            var transactionHash = Sha256(transactionId);
            var persisted = false;
            await realtimeDiscPlayerReference.RunTransaction(player =>
            {
                var purchase = player.Child("discPurchases/" + transactionHash);
                if (purchase.Value != null)
                {
                    persisted = true;
                    return TransactionResult.Abort();
                }
                purchase.Child("productId").Value = productId;
                purchase.Child("discId").Value = DiscIds[index];
                purchase.Child("localizedPrice").Value = localizedPrice ?? string.Empty;
                purchase.Child("purchasedAt").Value = ServerValue.Timestamp;
                var disc = player.Child("discs/" + DiscIds[index]);
                disc.Child("name").Value = DiscNames[index];
                disc.Child("unlocked").Value = true;
                disc.Child("unlockKind").Value = "store";
                disc.Child("unlockedAt").Value = ServerValue.Timestamp;
                disc.Child("updatedAt").Value = ServerValue.Timestamp;
                player.Child("selectedDisc").Value = DiscIds[index];
                player.Child("updatedAt").Value = ServerValue.Timestamp;
                persisted = true;
                return TransactionResult.Success(player);
            }, true);
            if (persisted)
            {
                bootstrapState.UnlockedDiscs.Add(DiscIds[index]);
                bootstrapState.SelectedDisc = DiscIds[index];
                previewDiscId = DiscIds[index];
                GravityDiscLoadout.SelectedDiscId = DiscIds[index];
                await SyncDiscStateToFirestoreAsync();
            }
            return persisted;
        }

        private async Task SyncDiscStateToFirestoreAsync()
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                var unlocked = new List<object>();
                foreach (var id in bootstrapState.UnlockedDiscs)
                    unlocked.Add(id);
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "coins", bootstrapState.Coins },
                        { "disc_shards", bootstrapState.DiscShards },
                        { "selected_disc", bootstrapState.SelectedDisc },
                        { "unlocked_discs", unlocked },
                        { "discs_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Firestore disc mirror delayed: " + exception.Message);
            }
        }

        private void SetDiscStatus(string message, bool failed)
        {
            if (meDiscStatusText == null || string.IsNullOrWhiteSpace(message))
                return;
            var compact = message.Trim().ToUpperInvariant();
            meDiscStatusText.text = compact.Length <= 54 ? compact : compact[..54];
            meDiscStatusText.color = failed ? Hex("FF7EBB") : Hex("78A8CA");
        }

        private static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}
