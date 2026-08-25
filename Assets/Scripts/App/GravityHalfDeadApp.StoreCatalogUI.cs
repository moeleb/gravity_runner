using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private Transform storeCatalogGrid;
        private Text storeCatalogStatusText;

        private void BuildStoreCatalogPage(Transform parent)
        {
            var header = CreateCard("Store catalog header", parent, new Vector2(0f, 665f),
                new Vector2(1080f, 250f), new Color(0.004f, 0.014f, 0.045f, 0.99f), 0);
            header.GetComponent<Image>().raycastTarget = false;

            BuildShopTitleWing(header.transform, -330f, false);
            BuildShopTitleWing(header.transform, 330f, true);
            var title = MakeText(header.transform, "STORE", 84, FontStyle.BoldAndItalic,
                Color.white, new Vector2(0f, 50f), new Vector2(620f, 102f),
                TextAnchor.MiddleCenter, 4);
            AddGraphicOutline(title, Hex("183E85"), 4f);
            var titleGlow = title.gameObject.AddComponent<Shadow>();
            titleGlow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.70f);
            titleGlow.effectDistance = new Vector2(0f, -7f);

            MakeText(header.transform, "POWER THE BREACH. KEEP THE RUN ALIVE.", 25,
                FontStyle.Bold, Hex("A9EFFF"), new Vector2(0f, -16f),
                new Vector2(760f, 42f), TextAnchor.MiddleCenter, 2);

            storeCatalogStatusText = MakeText(header.transform,
                "SYNCING CATALOG · PRICES FROM YOUR APP STORE", 16, FontStyle.Bold,
                Hex("819AB9"), new Vector2(0f, -72f), new Vector2(880f, 30f),
                TextAnchor.MiddleCenter, 1);

            BuildStoreCatalogScrollArea(parent);
            EnsureFallbackStoreOffers();
            RefreshStoreCatalogUI();
        }

        private void BuildStoreCatalogScrollArea(Transform parent)
        {
            var scrollObject = new GameObject("Responsive Store catalog");
            scrollObject.transform.SetParent(parent, false);
            var scrollRectTransform = scrollObject.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(12f, 220f);
            // With the wallet capsule and currency filters removed, the unified catalog can
            // begin directly beneath the header and use the recovered vertical space.
            scrollRectTransform.offsetMax = new Vector2(-12f, -410f);
            var scrollHit = scrollObject.AddComponent<Image>();
            scrollHit.color = new Color(1f, 1f, 1f, 0.001f);

            var viewportObject = new GameObject("Store catalog viewport");
            viewportObject.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewportObject.AddComponent<RectTransform>();
            Stretch(viewportRect);
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Three column Store catalog grid");
            contentObject.transform.SetParent(viewportObject.transform, false);
            var contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 860f);

            var grid = contentObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(20, 20, 18, 18);
            grid.cellSize = new Vector2(324f, 398f);
            grid.spacing = new Vector2(14f, 18f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            storeCatalogGrid = contentObject.transform;
            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.10f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.14f;
            scroll.scrollSensitivity = 52f;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void RefreshStoreCatalogUI()
        {
            if (storeCatalogGrid == null)
                return;

            if (storeCatalogStatusText != null)
            {
                storeCatalogStatusText.text = storeCatalogIsRemote
                    ? "LIVE FIREBASE CATALOG · LOCALIZED APP-STORE PRICES"
                    : "OFFLINE CATALOG PREVIEW · WAITING FOR FIREBASE";
                storeCatalogStatusText.color = Hex("819AB9");
            }

            RebuildStoreCatalogGrid();
        }

        private void RebuildStoreCatalogGrid()
        {
            if (storeCatalogGrid == null)
                return;

            for (var i = storeCatalogGrid.childCount - 1; i >= 0; i--)
            {
                var child = storeCatalogGrid.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            var visibleOffers = VisibleStoreOffers();
            var rows = Mathf.Max(1, Mathf.CeilToInt(visibleOffers.Count / 3f));
            var contentRect = storeCatalogGrid.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0f, 36f + rows * 398f + (rows - 1) * 18f);

            if (visibleOffers.Count == 0)
            {
                BuildEmptyStoreCatalogCard(storeCatalogGrid);
                return;
            }

            foreach (var offer in visibleOffers)
                BuildStoreOfferCard(storeCatalogGrid, offer);
        }

        private void BuildEmptyStoreCatalogCard(Transform parent)
        {
            var empty = CreateCard("Empty Firebase Store catalog", parent, Vector2.zero,
                new Vector2(324f, 398f), Hex("071225"), 25);
            AddGraphicOutline(empty.GetComponent<Image>(), Hex("314862"), 1.5f);
            MakeText(empty.transform, "NO ACTIVE\nBUNDLES", 29, FontStyle.Bold,
                Hex("8190A9"), Vector2.zero, new Vector2(260f, 120f),
                TextAnchor.MiddleCenter, 1);
        }

        private void BuildStoreOfferCard(Transform parent, StoreOffer offer)
        {
            var accent = offer.CurrencyKind == StoreCurrencyKind.Coins
                ? Hex("F6A90F")
                : (offer.ArtVariant % 2 == 0 ? Cyan : NeonPurple);
            var glow = CreateCard(offer.ProductId + " glow", parent, Vector2.zero,
                new Vector2(324f, 398f), new Color(accent.r, accent.g, accent.b, 0.45f), 27);
            glow.GetComponent<Image>().raycastTarget = false;
            var shell = CreateCard(offer.ProductId + " card", glow.transform, Vector2.zero,
                new Vector2(314f, 388f), Color.Lerp(Hex("061020"), accent, 0.08f), 24);
            var shellImage = shell.GetComponent<Image>();
            shellImage.raycastTarget = false;
            AddGraphicOutline(shellImage, accent, 1.7f);

            var artStage = CreateCard(offer.ProductId + " art stage", shell.transform,
                new Vector2(0f, 58f), new Vector2(284f, 234f),
                Color.Lerp(Hex("071329"), accent, 0.10f), 20);
            artStage.GetComponent<Image>().raycastTarget = false;
            var halo = CreateCard(offer.ProductId + " halo", artStage.transform,
                new Vector2(0f, 2f), new Vector2(196f + offer.ArtVariant * 8f,
                    196f + offer.ArtVariant * 8f),
                new Color(accent.r, accent.g, accent.b, 0.13f), 98);
            halo.GetComponent<Image>().raycastTarget = false;

            // Every Firebase art_variant maps to a genuinely different transparent asset.
            // Coins have variants 0..5; revive cores have variants 0..4.
            var artResourcePath = offer.CurrencyKind == StoreCurrencyKind.Coins
                ? "UI/Shop/Store/Coins/coin_bundle_" + Mathf.Clamp(offer.ArtVariant, 0, 5)
                : "UI/Shop/Store/Revive/revive_bundle_" + Mathf.Clamp(offer.ArtVariant, 0, 4);
            var artTexture = Resources.Load<Texture2D>(artResourcePath);
            var artSize = Mathf.Clamp(190f + offer.ArtVariant * 13f, 190f, 238f);
            var art = MakeTextureImage(offer.ProductId + " generated bundle art", artStage.transform,
                artTexture, new Vector2(0f, 2f), new Vector2(artSize, artSize));
            var artAspect = art.GetComponent<AspectRatioFitter>();
            if (artAspect != null)
                artAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var amountLabel = FormatStoreOfferAmount(offer);
            var amount = MakeText(shell.transform, amountLabel, 30, FontStyle.Bold,
                Color.white, new Vector2(0f, -88f), new Vector2(286f, 62f),
                TextAnchor.MiddleCenter, 1);
            amount.resizeTextForBestFit = true;
            amount.resizeTextMinSize = 21;
            amount.resizeTextMaxSize = 30;
            AddGraphicOutline(amount, Hex("10182A"), 1.4f);

            var hasPrice = storeIapPrices.TryGetValue(offer.ProductId, out var localizedPrice)
                           && !string.IsNullOrWhiteSpace(localizedPrice);
            var canPurchase = hasPrice && storeIapProvider != null
                              && storeIapProvider.CanPurchase(offer.ProductId)
                              && string.IsNullOrEmpty(storePurchaseInFlightProductId);
            var priceText = storePurchaseInFlightProductId == offer.ProductId
                ? "PROCESSING…"
                : hasPrice ? localizedPrice : "CHECKING STORE…";
            var buttonColor = canPurchase
                ? Color.Lerp(Hex("0C1D34"), accent, 0.42f)
                : Hex("273140");
            var buy = MakeButton(shell.transform, priceText, new Vector2(0f, -154f),
                new Vector2(286f, 70f), buttonColor, canPurchase ? Color.white : Hex("8B96A6"),
                27, () => BeginStorePurchase(offer.ProductId));
            buy.interactable = canPurchase;
            AddGraphicOutline(buy.GetComponent<Image>(), canPurchase ? accent : Hex("4C5766"), 1.7f);

            if (IsMostPopularStoreOffer(offer))
            {
                var ribbon = CreateCard(offer.ProductId + " dynamic popular ribbon", shell.transform,
                    new Vector2(0f, 169f), new Vector2(270f, 48f), Hex("FFB62F"), 13);
                ribbon.GetComponent<Image>().raycastTarget = false;
                MakeText(ribbon.transform, "MOST POPULAR", 20, FontStyle.Bold,
                    Hex("4D2105"), Vector2.zero, new Vector2(250f, 44f),
                    TextAnchor.MiddleCenter, 1).raycastTarget = false;
            }
        }

        private static string FormatStoreOfferAmount(StoreOffer offer)
        {
            var unit = offer.CurrencyKind == StoreCurrencyKind.Coins ? "G COINS" : "REVIVE CORES";
            return Math.Max(0L, offer.RewardAmount).ToString("N0") + "  " + unit;
        }
    }
}
