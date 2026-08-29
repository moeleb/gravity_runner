using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private enum DiscUnlockKind { Starter, Coins, Shards, Store }

        private const int DiscCount = 12;
        private static readonly string[] DiscIds =
        {
            "core_runner", "pulse_ring", "neon_orbit", "ion_skimmer", "void_circuit", "comet_drive",
            "prism_halo", "rift_bloom", "quantum_crown", "solar_forge", "abyss_engine", "aurora_sovereign"
        };
        private static readonly string[] DiscNames =
        {
            "CORE RUNNER", "PULSE RING", "NEON ORBIT", "ION SKIMMER", "VOID CIRCUIT", "COMET DRIVE",
            "PRISM HALO", "RIFT BLOOM", "QUANTUM CROWN", "SOLAR FORGE", "ABYSS ENGINE", "AURORA SOVEREIGN"
        };
        private static readonly string[] DiscClasses =
        {
            "STARTER BOARD", "STREET SERIES", "NEON SERIES", "ION SERIES", "VOID SERIES", "CHEST SERIES",
            "PRISM SERIES", "RIFT SERIES", "ROYAL SHARD SERIES", "PREMIUM SOLAR", "PREMIUM ABYSS", "PREMIUM AURORA"
        };
        private static readonly DiscUnlockKind[] DiscUnlockKinds =
        {
            DiscUnlockKind.Starter,
            DiscUnlockKind.Coins, DiscUnlockKind.Coins, DiscUnlockKind.Coins, DiscUnlockKind.Coins,
            DiscUnlockKind.Shards, DiscUnlockKind.Shards, DiscUnlockKind.Shards, DiscUnlockKind.Shards,
            DiscUnlockKind.Store, DiscUnlockKind.Store, DiscUnlockKind.Store
        };
        private static readonly long[] DiscUnlockCosts =
            { 0L, 5000L, 15000L, 30000L, 60000L, 10L, 25L, 50L, 100L, 0L, 0L, 0L };
        private static readonly string[] DiscStoreProductIds =
        {
            "", "", "", "", "", "", "", "", "",
            "gravityhalfdead.disc.solar_forge",
            "gravityhalfdead.disc.abyss_engine",
            "gravityhalfdead.disc.aurora_sovereign"
        };
        private static readonly Color[] DiscAccentColors =
        {
            Hex("18D9FF"), Hex("37E8FF"), Hex("8470FF"), Hex("23D4FF"), Hex("B24CFF"), Hex("FF9048"),
            Hex("56F7D4"), Hex("FF4EC8"), Hex("E9C35A"), Hex("FFB72E"), Hex("9C48FF"), Hex("42E8FF")
        };

        private RawImage meDiscFeaturedImage;
        private Text meDiscFeaturedName;
        private Text meDiscFeaturedClass;
        private Text meDiscWalletText;
        private Text meDiscActionText;
        private Text meDiscStatusText;
        private Button meDiscActionButton;
        private ScrollRect meDiscsScrollRect;
        private readonly RawImage[] meDiscCardImages = new RawImage[DiscCount];
        private readonly Image[] meDiscCardBorders = new Image[DiscCount];
        private readonly GameObject[] meDiscCardLocks = new GameObject[DiscCount];
        private readonly GameObject[] meDiscCardChecks = new GameObject[DiscCount];
        private readonly Text[] meDiscCardCosts = new Text[DiscCount];
        private string previewDiscId = "core_runner";

        private void BuildMeDiscsPanel(Transform parent)
        {
            meDiscsContent = new GameObject("ME · Rideable Disc Collection");
            meDiscsContent.transform.SetParent(parent, false);
            var rootRect = meDiscsContent.AddComponent<RectTransform>();
            Stretch(rootRect);

            CreateImage("Disc deep void", meDiscsContent.transform, Hex("010611"), FullStretch()).raycastTarget = false;
            BuildUpgradesCircuitBackdrop(meDiscsContent.transform);

            var scrollObject = new GameObject("Scrollable disc collection");
            scrollObject.transform.SetParent(meDiscsContent.transform, false);
            var scrollRectTransform = scrollObject.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            // The global resource header remains visible above ME. Keep the collection viewport
            // below it and above the fixed ME navigation on every aspect ratio.
            scrollRectTransform.offsetMin = new Vector2(0f, 250f);
            scrollRectTransform.offsetMax = new Vector2(0f, -235f);
            scrollObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);

            var viewport = new GameObject("Disc viewport");
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            Stretch(viewportRect);
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Disc content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 1f);
            contentRect.anchorMax = new Vector2(0.5f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(1080f, 1700f);
            contentRect.anchoredPosition = Vector2.zero;

            BuildDiscHeader(content.transform);
            BuildDiscFeaturedStage(content.transform);
            for (var i = 0; i < DiscCount; i++)
                BuildDiscRosterCard(content.transform, i);

            meDiscsScrollRect = scrollObject.AddComponent<ScrollRect>();
            meDiscsScrollRect.viewport = viewportRect;
            meDiscsScrollRect.content = contentRect;
            meDiscsScrollRect.horizontal = false;
            meDiscsScrollRect.vertical = true;
            meDiscsScrollRect.movementType = ScrollRect.MovementType.Clamped;
            meDiscsScrollRect.inertia = true;
            meDiscsScrollRect.decelerationRate = 0.14f;
            meDiscsScrollRect.scrollSensitivity = 54f;
            meDiscsScrollRect.verticalNormalizedPosition = 1f;
            meDiscsContent.SetActive(false);
        }

        private void BuildDiscHeader(Transform parent)
        {
            var title = MakeText(parent, "GRAVITY DISCS", 70, FontStyle.BoldAndItalic, Color.white,
                new Vector2(0f, 770f), new Vector2(760f, 82f), TextAnchor.MiddleCenter, 4);
            AddGraphicOutline(title, Hex("103C72"), 4f);
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.65f);
            MakeText(parent, "RIDE THE BREACH · MASTER EVERY LINE", 25, FontStyle.Bold, Hex("9FC8F5"),
                new Vector2(0f, 710f), new Vector2(900f, 34f), TextAnchor.MiddleCenter, 2);

            var wallet = CreateCard("Disc wallet", parent, new Vector2(0f, 650f), new Vector2(500f, 66f),
                Hex("07162A"), 20);
            AddGraphicOutline(wallet.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.85f), 2f);
            BuildDiscShardIcon(wallet.transform, new Vector2(-170f, 0f), 36f, Cyan);
            meDiscWalletText = MakeText(wallet.transform, "0 SHARDS  ·  0 G", 26, FontStyle.Bold, Color.white,
                new Vector2(40f, 0f), new Vector2(360f, 52f), TextAnchor.MiddleCenter, 1);
        }

        private void BuildDiscFeaturedStage(Transform parent)
        {
            var stage = new GameObject("Borderless rider-ready disc stage");
            stage.transform.SetParent(parent, false);
            var stageRect = stage.AddComponent<RectTransform>();
            SetRect(stageRect, new Vector2(0f, 320f), new Vector2(1080f, 540f));

            for (var i = 0; i < 5; i++)
            {
                var ring = CreateImage("Hover field " + i, stage.transform,
                    new Color(0.08f, 0.65f, 1f, 0.13f - i * 0.018f),
                    Centered(new Vector2(0f, -32f), new Vector2(520f - i * 62f, 142f - i * 16f)),
                    RoundedSprite(90));
                ring.raycastTarget = false;
            }

            meDiscFeaturedName = MakeText(stage.transform, "CORE RUNNER", 52, FontStyle.Bold, Color.white,
                new Vector2(0f, 222f), new Vector2(720f, 64f), TextAnchor.MiddleCenter, 3);
            meDiscFeaturedClass = MakeText(stage.transform, "STARTER BOARD", 21, FontStyle.Bold, Cyan,
                new Vector2(0f, 180f), new Vector2(600f, 30f), TextAnchor.MiddleCenter, 2);

            var artRoot = new GameObject("Featured disc art");
            artRoot.transform.SetParent(stage.transform, false);
            var artRect = artRoot.AddComponent<RectTransform>();
            SetRect(artRect, new Vector2(0f, 8f), new Vector2(500f, 310f));
            meDiscFeaturedImage = artRoot.AddComponent<RawImage>();
            meDiscFeaturedImage.raycastTarget = false;
            var aspect = artRoot.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1f;

            meDiscActionButton = MakeButton(stage.transform, string.Empty, new Vector2(0f, -192f),
                new Vector2(420f, 70f), Hex("0B5C82"), Color.white, 24, TryUnlockOrSelectPreviewDisc);
            AddGraphicOutline(meDiscActionButton.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f), 2f);
            meDiscActionText = MakeText(meDiscActionButton.transform, "EQUIPPED", 25, FontStyle.Bold, NeonLime,
                Vector2.zero, new Vector2(370f, 55f), TextAnchor.MiddleCenter, 2);
            meDiscStatusText = MakeText(stage.transform, "ONE SHARED 1.8 m RIDER DECK · FEET STAY CENTERED",
                16, FontStyle.Bold, Hex("718EAE"), new Vector2(0f, -240f),
                new Vector2(820f, 26f), TextAnchor.MiddleCenter, 1);
        }

        private void BuildDiscRosterCard(Transform parent, int index)
        {
            const float width = 244f;
            const float height = 220f;
            const float gap = 14f;
            var column = index % 4;
            var row = index / 4;
            var x = -((width + gap) * 1.5f) + column * (width + gap);
            var y = -105f - row * (height + gap);
            var accent = DiscAccentColors[index];

            var card = CreateCard("Disc · " + DiscNames[index], parent, new Vector2(x, y),
                new Vector2(width, height), Hex("061426"), 18);
            var border = card.GetComponent<Image>();
            AddGraphicOutline(border, new Color(accent.r, accent.g, accent.b, 0.9f), 2f);
            meDiscCardBorders[index] = border;
            var button = card.AddComponent<Button>();
            button.targetGraphic = border;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var capturedIndex = index;
            button.onClick.AddListener(() => PreviewDisc(DiscIds[capturedIndex]));

            var artObject = new GameObject("Disc art");
            artObject.transform.SetParent(card.transform, false);
            var artRect = artObject.AddComponent<RectTransform>();
            SetRect(artRect, new Vector2(0f, 25f), new Vector2(190f, 135f));
            meDiscCardImages[index] = artObject.AddComponent<RawImage>();
            meDiscCardImages[index].texture = Resources.Load<Texture2D>("UI/Discs/" + DiscIds[index]);
            meDiscCardImages[index].raycastTarget = false;

            MakeText(card.transform, DiscNames[index], 17, FontStyle.Bold, Color.white,
                new Vector2(0f, -58f), new Vector2(220f, 28f), TextAnchor.MiddleCenter, 1);
            meDiscCardCosts[index] = MakeText(card.transform, DiscCostLabel(index), 14, FontStyle.Bold, accent,
                new Vector2(0f, -86f), new Vector2(210f, 26f), TextAnchor.MiddleCenter, 0);

            meDiscCardLocks[index] = BuildSmallDiscLock(card.transform, new Vector2(-92f, 82f));
            meDiscCardChecks[index] = CreateCard("Equipped check", card.transform, new Vector2(90f, -82f),
                new Vector2(42f, 42f), NeonLime, 21);
            MakeText(meDiscCardChecks[index].transform, "✓", 28, FontStyle.Bold, Hex("05213A"),
                Vector2.zero, new Vector2(38f, 38f), TextAnchor.MiddleCenter);
        }

        private void RefreshDiscCollectionUI()
        {
            if (meDiscFeaturedImage == null || bootstrapState == null)
                return;
            var index = Array.IndexOf(DiscIds, previewDiscId);
            if (index < 0)
            {
                previewDiscId = bootstrapState.SelectedDisc;
                index = Mathf.Max(0, Array.IndexOf(DiscIds, previewDiscId));
            }

            meDiscFeaturedImage.texture = Resources.Load<Texture2D>("UI/Discs/" + DiscIds[index]);
            meDiscFeaturedName.text = DiscNames[index];
            meDiscFeaturedClass.text = DiscClasses[index];
            meDiscFeaturedClass.color = DiscAccentColors[index];
            if (meDiscWalletText != null)
                meDiscWalletText.text = bootstrapState.DiscShards.ToString("N0") + " SHARDS  ·  "
                                      + bootstrapState.Coins.ToString("N0") + " G";

            var unlocked = IsDiscUnlocked(DiscIds[index]);
            var selected = bootstrapState.SelectedDisc == DiscIds[index];
            var affordable = CanAffordDisc(index);
            meDiscActionText.text = selected ? "EQUIPPED" : unlocked ? "EQUIP" : DiscCostLabel(index);
            meDiscActionText.color = selected ? NeonLime : unlocked ? Cyan : affordable ? Hex("FFD45E") : Hex("74839A");
            meDiscActionButton.interactable = !discPurchaseInFlight && !selected && (unlocked || affordable);
            meDiscActionButton.GetComponent<Image>().color = selected ? Hex("0B5C82")
                : meDiscActionButton.interactable ? Hex("0A4974") : Hex("111A2A");

            for (var i = 0; i < DiscCount; i++)
            {
                var owned = IsDiscUnlocked(DiscIds[i]);
                var equipped = bootstrapState.SelectedDisc == DiscIds[i];
                meDiscCardLocks[i].SetActive(!owned);
                meDiscCardChecks[i].SetActive(equipped);
                meDiscCardCosts[i].text = equipped ? "EQUIPPED" : owned ? "OWNED" : DiscCostLabel(i);
                meDiscCardImages[i].color = owned ? Color.white : new Color(0.72f, 0.78f, 0.88f, 0.78f);
                meDiscCardBorders[i].color = previewDiscId == DiscIds[i] ? Hex("11385E") : Hex("061426");
            }
        }

        private void ResetDiscCollectionScroll()
        {
            if (meDiscsScrollRect == null)
                return;
            Canvas.ForceUpdateCanvases();
            meDiscsScrollRect.StopMovement();
            meDiscsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void PreviewDisc(string discId)
        {
            if (Array.IndexOf(DiscIds, discId) < 0)
                return;
            previewDiscId = discId;
            RefreshDiscCollectionUI();
        }

        private void TryUnlockOrSelectPreviewDisc()
        {
            var index = Array.IndexOf(DiscIds, previewDiscId);
            if (index < 0 || discPurchaseInFlight)
                return;
            if (IsDiscUnlocked(previewDiscId))
            {
                _ = SetSelectedDiscRealtimeAsync(previewDiscId);
                return;
            }
            if (DiscUnlockKinds[index] == DiscUnlockKind.Store)
            {
                BeginDiscPurchase(index);
                return;
            }
            _ = UnlockDiscWithCurrencyAsync(index);
        }

        private bool CanAffordDisc(int index)
        {
            if (index <= 0 || index >= DiscCount)
                return false;
            return DiscUnlockKinds[index] switch
            {
                DiscUnlockKind.Coins => bootstrapState.Coins >= DiscUnlockCosts[index],
                DiscUnlockKind.Shards => bootstrapState.DiscShards >= DiscUnlockCosts[index],
                DiscUnlockKind.Store => storeIapProvider != null
                                        && storeIapProvider.CanPurchase(DiscStoreProductIds[index]),
                _ => true
            };
        }

        private string DiscCostLabel(int index)
        {
            return DiscUnlockKinds[index] switch
            {
                DiscUnlockKind.Starter => "STARTER",
                DiscUnlockKind.Coins => DiscUnlockCosts[index].ToString("N0") + " G",
                DiscUnlockKind.Shards => DiscUnlockCosts[index].ToString("N0") + " SHARDS",
                DiscUnlockKind.Store => discIapPrices.TryGetValue(DiscStoreProductIds[index], out var price)
                    ? price : "STORE PRICE",
                _ => string.Empty
            };
        }

        private bool IsDiscUnlocked(string discId)
            => discId == "core_runner" || bootstrapState.UnlockedDiscs.Contains(discId);

        private static void BuildDiscShardIcon(Transform parent, Vector2 position, float size, Color color)
        {
            var shard = new GameObject("Disc shard");
            shard.transform.SetParent(parent, false);
            var rect = shard.AddComponent<RectTransform>();
            SetRect(rect, position, new Vector2(size * 0.58f, size));
            rect.localEulerAngles = new Vector3(0f, 0f, 45f);
            var image = shard.AddComponent<Image>();
            image.sprite = RoundedSprite(Mathf.RoundToInt(size * 0.12f));
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static GameObject BuildSmallDiscLock(Transform parent, Vector2 position)
        {
            var holder = new GameObject("Disc lock");
            holder.transform.SetParent(parent, false);
            var rect = holder.AddComponent<RectTransform>();
            SetRect(rect, position, new Vector2(34f, 42f));
            var body = holder.AddComponent<Image>();
            body.sprite = RoundedSprite(8);
            body.type = Image.Type.Sliced;
            body.color = Hex("D8E6F4");
            body.raycastTarget = false;
            return holder;
        }
    }
}
