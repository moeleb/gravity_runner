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
        private readonly Dictionary<string, ProductType> configuredProductTypes = new();
        private readonly Dictionary<string, Product> products = new();
        private readonly Dictionary<string, Action<string, string>> localizedPriceCallbacks = new();
        private readonly Dictionary<string, Func<string, string, string, string, Task<bool>>> fulfillmentCallbacks = new();
        private readonly Dictionary<string, Action<string, string, bool>> statusCallbacks = new();
        private StoreController storeController;
        private bool initialized;
        private bool connected;
        private bool purchaseHistoryFetched;

        public void Configure(IEnumerable<string> productIds,
            Action<string, string> onLocalizedPrice,
            Func<string, string, string, string, Task<bool>> onFulfill,
            Action<string, string, bool> onStatus)
            => ConfigureProducts(productIds, ProductType.Consumable, onLocalizedPrice, onFulfill, onStatus);

        public void ConfigureNonConsumables(IEnumerable<string> productIds,
            Action<string, string> onLocalizedPrice,
            Func<string, string, string, string, Task<bool>> onFulfill,
            Action<string, string, bool> onStatus)
            => ConfigureProducts(productIds, ProductType.NonConsumable, onLocalizedPrice, onFulfill, onStatus);

        private void ConfigureProducts(IEnumerable<string> productIds, ProductType productType,
            Action<string, string> onLocalizedPrice,
            Func<string, string, string, string, Task<bool>> onFulfill,
            Action<string, string, bool> onStatus)
        {
            var changed = false;
            foreach (var productId in productIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(productId))
                    continue;
                var id = productId.Trim();
                changed |= configuredProductIds.Add(id);
                configuredProductTypes[id] = productType;
                localizedPriceCallbacks[id] = onLocalizedPrice;
                fulfillmentCallbacks[id] = onFulfill;
                statusCallbacks[id] = onStatus;
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
                DispatchStatus(productId, "PRODUCT IS NOT AVAILABLE FROM THIS STORE", true);
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
                DispatchStatus(string.Empty, "CONNECTING TO YOUR APP STORE", false);
                await storeController.Connect();
            }
            catch (Exception exception)
            {
                connected = false;
                DispatchStatus(string.Empty,
                    "APP STORE CONNECTION DELAYED · " + exception.Message, true);
            }
        }

        private void OnStoreConnected()
        {
            connected = true;
            DispatchStatus(string.Empty, "APP STORE CONNECTED · LOADING LOCAL PRICES", false);
            FetchConfiguredProducts();
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            connected = false;
            DispatchStatus(string.Empty,
                "APP STORE OFFLINE · " + (description?.message ?? "TRY AGAIN LATER"), true);
        }

        private void FetchConfiguredProducts()
        {
            if (!connected || configuredProductIds.Count == 0)
                return;
            var definitions = configuredProductIds
                .Select(productId => new ProductDefinition(productId,
                    configuredProductTypes.TryGetValue(productId, out var type) ? type : ProductType.Consumable))
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
                {
                    if (localizedPriceCallbacks.TryGetValue(product.definition.id, out var callback))
                        callback?.Invoke(product.definition.id, product.metadata.localizedPriceString);
                }
            }

            DispatchStatus(string.Empty, "LOCALIZED APP-STORE PRICES READY", false);
            if (!purchaseHistoryFetched)
            {
                purchaseHistoryFetched = true;
                storeController.FetchPurchases();
            }
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            DispatchStatus(string.Empty,
                "PRODUCT CATALOG UNAVAILABLE · " + failure.FailureReason, true);
        }

        private async void OnPurchasePending(PendingOrder order)
        {
            var product = FirstProduct(order);
            if (product == null || !fulfillmentCallbacks.TryGetValue(product.definition.id, out var fulfillmentCallback)
                                || fulfillmentCallback == null)
            {
                DispatchStatus(string.Empty, "PURCHASE DATA WAS INCOMPLETE", true);
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
                    DispatchStatus(productId,
                        "PURCHASE SAVED BY STORE · FIREBASE SYNC WILL RETRY", true);
                    return;
                }

                storeController.ConfirmPurchase(order);
                DispatchStatus(productId, "PURCHASE ADDED TO YOUR ACCOUNT", false);
            }
            catch (Exception exception)
            {
                // Do not confirm. Unity IAP will redeliver the pending consumable later.
                DispatchStatus(productId,
                    "PURCHASE SYNC WILL RETRY · " + exception.Message, true);
            }
        }

        private void OnPurchaseConfirmed(Order order)
        {
            var product = FirstProduct(order);
            DispatchStatus(product?.definition.id ?? string.Empty,
                "PURCHASE CONFIRMED", false);
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            var product = FirstProduct(order);
            DispatchStatus(product?.definition.id ?? string.Empty,
                "PURCHASE NOT COMPLETED · " + order.FailureReason, true);
        }

        private void OnPurchasesFetched(Orders orders)
        {
            DispatchStatus(string.Empty, "STORE PURCHASES SYNCHRONIZED", false);
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription description)
        {
            DispatchStatus(string.Empty, "PURCHASE HISTORY SYNC DELAYED", true);
        }

        private void DispatchStatus(string productId, string message, bool failed)
        {
            if (!string.IsNullOrWhiteSpace(productId)
                && statusCallbacks.TryGetValue(productId, out var productCallback))
            {
                productCallback?.Invoke(productId, message, failed);
                return;
            }

            foreach (var callback in statusCallbacks.Values.Distinct())
                callback?.Invoke(productId, message, failed);
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
