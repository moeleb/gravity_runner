using System;
using System.Collections.Generic;
using System.Linq;
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
        private enum StoreCurrencyKind
        {
            Coins,
            GravityCore
        }

        private sealed class StoreOffer
        {
            public string ProductId;
            public StoreCurrencyKind CurrencyKind;
            public long RewardAmount;
            public int SortOrder;
            public int ArtVariant;
            public bool Enabled;
        }

        private readonly List<StoreOffer> storeOffers = new();
        private readonly Dictionary<string, string> storeIapPrices = new();
        private ListenerRegistration storeCatalogListener;
        private ListenerRegistration storePopularityListener;
        private GravityIapStoreProvider storeIapProvider;
        private bool storeCatalogIsRemote;
        private string storePopularCoinProductId = string.Empty;
        private string storePopularCoreProductId = string.Empty;
        private string storePurchaseInFlightProductId = string.Empty;

        private void EnsureFallbackStoreOffers()
        {
            if (storeOffers.Count > 0)
                return;

            // These are an offline layout fallback only. Firebase storeCatalog replaces this
            // list in real time. No real-money price is kept here or in Firebase; Unity IAP
            // always supplies the localized App Store / Google Play price.
            storeOffers.AddRange(new[]
            {
                OfflineStoreOffer("gravityhalfdead.coins.7500", StoreCurrencyKind.Coins, 7500L, 10, 0),
                OfflineStoreOffer("gravityhalfdead.coins.40000", StoreCurrencyKind.Coins, 40000L, 20, 1),
                OfflineStoreOffer("gravityhalfdead.coins.90000", StoreCurrencyKind.Coins, 90000L, 30, 2),
                OfflineStoreOffer("gravityhalfdead.coins.200000", StoreCurrencyKind.Coins, 200000L, 40, 3),
                OfflineStoreOffer("gravityhalfdead.coins.550000", StoreCurrencyKind.Coins, 550000L, 50, 4),
                OfflineStoreOffer("gravityhalfdead.coins.1250000", StoreCurrencyKind.Coins, 1250000L, 60, 5),
                OfflineStoreOffer("gravityhalfdead.cores.5", StoreCurrencyKind.GravityCore, 5L, 110, 0),
                OfflineStoreOffer("gravityhalfdead.cores.15", StoreCurrencyKind.GravityCore, 15L, 120, 1),
                OfflineStoreOffer("gravityhalfdead.cores.40", StoreCurrencyKind.GravityCore, 40L, 130, 2),
                OfflineStoreOffer("gravityhalfdead.cores.100", StoreCurrencyKind.GravityCore, 100L, 140, 3),
                OfflineStoreOffer("gravityhalfdead.cores.250", StoreCurrencyKind.GravityCore, 250L, 150, 4)
            });
        }

        private static StoreOffer OfflineStoreOffer(string productId, StoreCurrencyKind currencyKind,
            long rewardAmount, int sortOrder, int artVariant)
        {
            return new StoreOffer
            {
                ProductId = productId,
                CurrencyKind = currencyKind,
                RewardAmount = rewardAmount,
                SortOrder = sortOrder,
                ArtVariant = artVariant,
                Enabled = true
            };
        }

        private List<StoreOffer> VisibleStoreOffers(StoreCurrencyKind currencyKind)
        {
            return storeOffers.Where(offer => offer.Enabled && offer.CurrencyKind == currencyKind)
                .OrderBy(offer => offer.SortOrder)
                .ThenBy(offer => offer.RewardAmount)
                .ToList();
        }

        private void EnsureStoreCatalogSync()
        {
            EnsureFallbackStoreOffers();
            ConfigureStoreIap();
            if (storeCatalogListener != null || firestore == null || auth == null
                || auth.CurrentUser == null)
                return;

            storeCatalogListener = firestore.Collection("storeCatalog").Listen(snapshot =>
            {
                if (snapshot == null)
                    return;

                var remoteOffers = new List<StoreOffer>();
                foreach (var document in snapshot.Documents)
                {
                    if (TryParseStoreOffer(document, out var offer) && offer.Enabled)
                        remoteOffers.Add(offer);
                }

                if (remoteOffers.Count > 0)
                {
                    storeOffers.Clear();
                    storeOffers.AddRange(remoteOffers);
                    storeCatalogIsRemote = true;
                    ConfigureStoreIap();
                }
                else
                {
                    storeCatalogIsRemote = false;
                    if (storeOffers.Count == 0)
                        EnsureFallbackStoreOffers();
                }
                RefreshStoreCatalogUI();
            });

            storePopularityListener = firestore.Collection("storePopularity")
                .Document("current").Listen(snapshot =>
                {
                    storePopularCoinProductId = string.Empty;
                    storePopularCoreProductId = string.Empty;
                    if (snapshot != null && snapshot.Exists)
                    {
                        var values = snapshot.ToDictionary();
                        storePopularCoinProductId = StoreString(values, "coin_product_id");
                        storePopularCoreProductId = StoreString(values, "gravity_core_product_id");
                    }
                    RefreshStoreCatalogUI();
                });
        }

        private static bool TryParseStoreOffer(DocumentSnapshot document, out StoreOffer offer)
        {
            offer = null;
            if (document == null || !document.Exists)
                return false;

            var values = document.ToDictionary();
            var productId = StoreString(values, "product_id");
            if (string.IsNullOrWhiteSpace(productId))
                productId = document.Id;
            var rewardType = StoreString(values, "reward_type").ToLowerInvariant();
            if (rewardType != "coins" && rewardType != "gravity_cores")
                return false;

            values.TryGetValue("reward_amount", out var rewardAmountValue);
            values.TryGetValue("sort_order", out var sortOrderValue);
            values.TryGetValue("art_variant", out var artVariantValue);
            values.TryGetValue("enabled", out var enabledValue);
            var rewardAmount = DatabaseLong(rewardAmountValue, 0L);
            if (rewardAmount <= 0L)
                return false;

            var currencyKind = rewardType == "coins"
                ? StoreCurrencyKind.Coins
                : StoreCurrencyKind.GravityCore;
            var maximumArtVariant = currencyKind == StoreCurrencyKind.Coins ? 5 : 4;

            offer = new StoreOffer
            {
                ProductId = productId.Trim(),
                CurrencyKind = currencyKind,
                RewardAmount = rewardAmount,
                SortOrder = Mathf.Clamp((int)DatabaseLong(sortOrderValue, 0L), 0, 100000),
                ArtVariant = Mathf.Clamp((int)DatabaseLong(artVariantValue, 0L),
                    0, maximumArtVariant),
                Enabled = enabledValue == null || DatabaseBool(enabledValue)
            };
            return true;
        }

        private static string StoreString(IDictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out var value) && value != null
                ? value.ToString().Trim()
                : string.Empty;
        }

        private bool IsMostPopularStoreOffer(StoreOffer offer)
        {
            return offer.CurrencyKind == StoreCurrencyKind.Coins
                ? offer.ProductId == storePopularCoinProductId
                : offer.ProductId == storePopularCoreProductId;
        }

        private void ConfigureStoreIap()
        {
            if (storeOffers.Count == 0)
                return;
            if (storeIapProvider == null)
                storeIapProvider = gameObject.GetComponent<GravityIapStoreProvider>()
                                   ?? gameObject.AddComponent<GravityIapStoreProvider>();

            storeIapProvider.Configure(storeOffers.Where(offer => offer.Enabled)
                    .Select(offer => offer.ProductId),
                OnStoreLocalizedPrice,
                FulfillStorePurchaseAsync,
                OnStoreIapStatus);
        }

        private void OnStoreLocalizedPrice(string productId, string localizedPrice)
        {
            if (!string.IsNullOrWhiteSpace(productId) && !string.IsNullOrWhiteSpace(localizedPrice))
                storeIapPrices[productId] = localizedPrice;
            RefreshStoreCatalogUI();
        }

        private void OnStoreIapStatus(string productId, string message, bool failed)
        {
            if (failed && (string.IsNullOrEmpty(productId)
                           || storePurchaseInFlightProductId == productId))
                storePurchaseInFlightProductId = string.Empty;
            RefreshStoreCatalogUI();
            if (storeCatalogStatusText != null && !string.IsNullOrWhiteSpace(message))
            {
                storeCatalogStatusText.text = message.ToUpperInvariant();
                storeCatalogStatusText.color = failed ? Hex("FF87B7") : Hex("819AB9");
            }
        }

        private void BeginStorePurchase(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId) || storeIapProvider == null
                || !storeIapProvider.CanPurchase(productId)
                || !string.IsNullOrEmpty(storePurchaseInFlightProductId))
                return;

            storePurchaseInFlightProductId = productId;
            RefreshStoreCatalogUI();
            storeIapProvider.Purchase(productId);
        }

        private async Task<bool> FulfillStorePurchaseAsync(string productId, string transactionId,
            string receipt, string localizedPrice)
        {
            var offer = storeOffers.FirstOrDefault(candidate => candidate.Enabled
                                                                && candidate.ProductId == productId);
            if (offer == null || auth == null || auth.CurrentUser == null
                || realtimeDatabase == null || string.IsNullOrWhiteSpace(transactionId))
                return false;

            var expectedUserId = auth.CurrentUser.UserId;
            var transactionHash = StoreTransactionHash(transactionId);
            var playerReference = realtimeDatabase.RootReference.Child("players").Child(expectedUserId);
            var alreadyProcessed = false;
            var grantApplied = false;

            try
            {
                var result = await playerReference.RunTransaction(mutablePlayer =>
                {
                    alreadyProcessed = false;
                    grantApplied = false;
                    var purchase = mutablePlayer.Child("storePurchases/" + transactionHash);
                    if (purchase.Value != null)
                    {
                        alreadyProcessed = true;
                        return TransactionResult.Abort();
                    }

                    var walletKey = offer.CurrencyKind == StoreCurrencyKind.Coins
                        ? "coins"
                        : "gravityCores";
                    var wallet = mutablePlayer.Child(walletKey);
                    var fallbackBalance = offer.CurrencyKind == StoreCurrencyKind.Coins
                        ? bootstrapState.Coins
                        : bootstrapState.GravityCores;
                    wallet.Value = Math.Max(0L, DatabaseLong(wallet.Value, fallbackBalance))
                                   + offer.RewardAmount;
                    purchase.Child("productId").Value = offer.ProductId;
                    purchase.Child("rewardType").Value = offer.CurrencyKind == StoreCurrencyKind.Coins
                        ? "coins"
                        : "gravity_cores";
                    purchase.Child("rewardAmount").Value = offer.RewardAmount;
                    purchase.Child("localizedPrice").Value = (localizedPrice ?? string.Empty).Trim();
                    purchase.Child("purchasedAt").Value = ServerValue.Timestamp;
                    mutablePlayer.Child("updatedAt").Value = ServerValue.Timestamp;
                    grantApplied = true;
                    return TransactionResult.Success(mutablePlayer);
                }, true);

                if (!alreadyProcessed && !grantApplied)
                    return false;
                if (auth == null || auth.CurrentUser == null
                    || auth.CurrentUser.UserId != expectedUserId)
                    return false;

                if (result != null && result.Exists)
                {
                    bootstrapState.Coins = Math.Max(0L,
                        DatabaseLong(result.Child("coins").Value, bootstrapState.Coins));
                    bootstrapState.GravityCores = Math.Max(0L,
                        DatabaseLong(result.Child("gravityCores").Value, bootstrapState.GravityCores));
                }

                // The receipt is intentionally not copied to player-readable Firebase data.
                // It remains available here for the production server-side store validator.
                var purchaseDocument = firestore.Collection("users").Document(expectedUserId)
                    .Collection("purchases").Document(transactionHash);
                var purchaseSnapshot = await purchaseDocument.GetSnapshotAsync();
                if (!purchaseSnapshot.Exists)
                {
                    await purchaseDocument.SetAsync(new Dictionary<string, object>
                    {
                        { "product_id", offer.ProductId },
                        { "reward_type", offer.CurrencyKind == StoreCurrencyKind.Coins
                            ? "coins" : "gravity_cores" },
                        { "reward_amount", offer.RewardAmount },
                        { "localized_price", (localizedPrice ?? string.Empty).Trim() },
                        { "store_transaction_hash", transactionHash },
                        { "created_at", FieldValue.ServerTimestamp }
                    });
                }

                if (gameCoinAmountText != null)
                    gameCoinAmountText.text = bootstrapState.Coins.ToString("N0");
                RefreshGravityCoreHeader();
                RefreshStoreCatalogUI();
                RegisterSuccessfulPurchase();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Store purchase fulfillment is pending: " + exception.Message);
                return false;
            }
            finally
            {
                storePurchaseInFlightProductId = string.Empty;
                RefreshStoreCatalogUI();
            }
        }

        private static string StoreTransactionHash(string transactionId)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(transactionId ?? string.Empty));
            var result = new StringBuilder(64);
            foreach (var value in bytes)
                result.Append(value.ToString("x2"));
            return result.ToString();
        }

        private void StopStoreCatalogSync()
        {
            storeCatalogListener?.Stop();
            storeCatalogListener = null;
            storePopularityListener?.Stop();
            storePopularityListener = null;
        }
    }
}
