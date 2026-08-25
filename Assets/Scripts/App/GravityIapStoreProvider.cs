using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GravityHalfDead
{
    /// <summary>
    /// Thin Unity IAP 5 adapter. Firebase owns bundle contents and popularity;
    /// Google Play / App Store own availability and localized real-money prices.
    /// Pending consumables are confirmed only after the app persists the grant.
    /// </summary>
    public sealed class GravityIapStoreProvider : MonoBehaviour
    {
        private readonly HashSet<string> configuredProductIds = new();
        private readonly Dictionary<string, Product> products = new();
        private StoreController storeController;
        private Action<string, string> localizedPriceCallback;
        private Func<string, string, string, string, Task<bool>> fulfillmentCallback;
        private Action<string, string, bool> statusCallback;
        private bool initialized;
        private bool connected;
        private bool purchaseHistoryFetched;

        public void Configure(IEnumerable<string> productIds,
            Action<string, string> onLocalizedPrice,
            Func<string, string, string, string, Task<bool>> onFulfill,
            Action<string, string, bool> onStatus)
        {
            localizedPriceCallback = onLocalizedPrice;
            fulfillmentCallback = onFulfill;
            statusCallback = onStatus;

            var changed = false;
            foreach (var productId in productIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(productId))
                    changed |= configuredProductIds.Add(productId.Trim());
            }

            if (!initialized)
                InitializeStore();
            else if (connected && changed)
                FetchConfiguredProducts();
        }

        public bool CanPurchase(string productId)
        {
            return connected && !string.IsNullOrWhiteSpace(productId)
                             && products.TryGetValue(productId, out var product)
                             && product != null && product.availableToPurchase;
        }

        public void Purchase(string productId)
        {
            if (!CanPurchase(productId))
            {
                statusCallback?.Invoke(productId, "PRODUCT IS NOT AVAILABLE FROM THIS STORE", true);
                return;
            }
            storeController.PurchaseProduct(productId);
        }

        private async void InitializeStore()
        {
            initialized = true;
            storeController = UnityIAPServices.StoreController();
            storeController.OnStoreConnected += OnStoreConnected;
            storeController.OnStoreDisconnected += OnStoreDisconnected;
            storeController.OnProductsFetched += OnProductsFetched;
            storeController.OnProductsFetchFailed += OnProductsFetchFailed;
            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
            storeController.OnPurchaseFailed += OnPurchaseFailed;
            storeController.OnPurchasesFetched += OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            try
            {
                statusCallback?.Invoke(string.Empty, "CONNECTING TO YOUR APP STORE", false);
                await storeController.Connect();
            }
            catch (Exception exception)
            {
                connected = false;
                statusCallback?.Invoke(string.Empty,
                    "APP STORE CONNECTION DELAYED · " + exception.Message, true);
            }
        }

        private void OnStoreConnected()
        {
            connected = true;
            statusCallback?.Invoke(string.Empty, "APP STORE CONNECTED · LOADING LOCAL PRICES", false);
            FetchConfiguredProducts();
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            connected = false;
            statusCallback?.Invoke(string.Empty,
                "APP STORE OFFLINE · " + (description?.message ?? "TRY AGAIN LATER"), true);
        }

        private void FetchConfiguredProducts()
        {
            if (!connected || configuredProductIds.Count == 0)
                return;
            var definitions = configuredProductIds
                .Select(productId => new ProductDefinition(productId, ProductType.Consumable))
                .ToList();
            storeController.FetchProducts(definitions);
        }

        private void OnProductsFetched(List<Product> fetchedProducts)
        {
            foreach (var product in fetchedProducts ?? new List<Product>())
            {
                if (product == null || string.IsNullOrWhiteSpace(product.definition.id))
                    continue;
                products[product.definition.id] = product;
                if (!string.IsNullOrWhiteSpace(product.metadata?.localizedPriceString))
                    localizedPriceCallback?.Invoke(product.definition.id,
                        product.metadata.localizedPriceString);
            }

            statusCallback?.Invoke(string.Empty, "LOCALIZED APP-STORE PRICES READY", false);
            if (!purchaseHistoryFetched)
            {
                purchaseHistoryFetched = true;
                storeController.FetchPurchases();
            }
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            statusCallback?.Invoke(string.Empty,
                "PRODUCT CATALOG UNAVAILABLE · " + failure.FailureReason, true);
        }

        private async void OnPurchasePending(PendingOrder order)
        {
            var product = FirstProduct(order);
            if (product == null || fulfillmentCallback == null)
            {
                statusCallback?.Invoke(string.Empty, "PURCHASE DATA WAS INCOMPLETE", true);
                return;
            }

            var productId = product.definition.id;
            try
            {
                var persisted = await fulfillmentCallback(productId,
                    order.Info?.TransactionID ?? string.Empty,
                    order.Info?.Receipt ?? string.Empty,
                    product.metadata?.localizedPriceString ?? string.Empty);
                if (!persisted)
                {
                    statusCallback?.Invoke(productId,
                        "PURCHASE SAVED BY STORE · FIREBASE SYNC WILL RETRY", true);
                    return;
                }

                storeController.ConfirmPurchase(order);
                statusCallback?.Invoke(productId, "PURCHASE ADDED TO YOUR ACCOUNT", false);
            }
            catch (Exception exception)
            {
                // Do not confirm. Unity IAP will redeliver the pending consumable later.
                statusCallback?.Invoke(productId,
                    "PURCHASE SYNC WILL RETRY · " + exception.Message, true);
            }
        }

        private void OnPurchaseConfirmed(Order order)
        {
            var product = FirstProduct(order);
            statusCallback?.Invoke(product?.definition.id ?? string.Empty,
                "PURCHASE CONFIRMED", false);
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            var product = FirstProduct(order);
            statusCallback?.Invoke(product?.definition.id ?? string.Empty,
                "PURCHASE NOT COMPLETED · " + order.FailureReason, true);
        }

        private void OnPurchasesFetched(Orders orders)
        {
            statusCallback?.Invoke(string.Empty, "STORE PURCHASES SYNCHRONIZED", false);
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription description)
        {
            statusCallback?.Invoke(string.Empty, "PURCHASE HISTORY SYNC DELAYED", true);
        }

        private static Product FirstProduct(Order order)
        {
            return order?.CartOrdered?.Items().FirstOrDefault()?.Product;
        }

        private void OnDestroy()
        {
            if (storeController == null)
                return;
            storeController.OnStoreConnected -= OnStoreConnected;
            storeController.OnStoreDisconnected -= OnStoreDisconnected;
            storeController.OnProductsFetched -= OnProductsFetched;
            storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
            storeController.OnPurchasePending -= OnPurchasePending;
            storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            storeController.OnPurchaseFailed -= OnPurchaseFailed;
            storeController.OnPurchasesFetched -= OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        }
    }
}
