using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private const int CharacterScreenCount = 12;
        private const long CharacterUnlockCoinCost = 30000L;

        private static readonly string[] CharacterRarities =
        {
            "CORE CREW", "SYNTH SCOUT", "CORE CREW", "NEON REBEL", "BREACH TECH", "ROYAL ELITE",
            "STREET RUNNER", "VOID WALKER", "VOLT RAIDER", "DEEP RUNNER", "GLITCH RARE", "FLAME ELITE",
            "FROST ELITE"
        };

        private readonly GameObject[] meCharacterLockOverlays = new GameObject[CharacterScreenCount];
        private readonly GameObject[] meCharacterCheckmarks = new GameObject[CharacterScreenCount];
        private readonly GameObject[] meCharacterCostGroups = new GameObject[CharacterScreenCount];
        private readonly Text[] meCharacterCostTexts = new Text[CharacterScreenCount];
        private readonly Image[] meCharacterPositionDots = new Image[CharacterScreenCount];
        private Text meCharacterCollectionText;
        private Text meCharacterRarityText;
        private Text meCharacterActionLabelText;
        private GameObject meCharacterActionLock;
        private GameObject meCharacterActionCoin;

        private static string CharacterArtworkResource(string characterId)
            => characterId == "nova" ? "UI/Characters/nova_clean" : "UI/Characters/" + characterId;

        private Material CharacterArtworkMaterial(int index)
            => index == 0 ? novaCutoutMaterial : index >= 3 ? characterCutoutMaterial : null;

        private void BuildMeCharactersPanel(Transform parent)
        {
            meCharactersContent = new GameObject("ME · Full Screen Characters");
            meCharactersContent.transform.SetParent(parent, false);
            var contentRect = meCharactersContent.AddComponent<RectTransform>();
            Stretch(contentRect);

            var background = CreateImage("Characters deep void", meCharactersContent.transform,
                Hex("010611"), FullStretch());
            background.raycastTarget = false;
            BuildUpgradesCircuitBackdrop(meCharactersContent.transform);

            var collectionPanel = new GameObject("Borderless hero breach stage");
            collectionPanel.transform.SetParent(meCharactersContent.transform, false);
            var collectionRect = collectionPanel.AddComponent<RectTransform>();
            SetRect(collectionRect, new Vector2(0f, 420f), new Vector2(1080f, 700f));
            collectionPanel.AddComponent<RectMask2D>();
            BuildCharacterFeaturedArea(collectionPanel.transform);
            BuildCharacterActionBanner(collectionPanel.transform);

            var rosterBackdrop = new GameObject("Borderless expandable character roster");
            rosterBackdrop.transform.SetParent(meCharactersContent.transform, false);
            var rosterRect = rosterBackdrop.AddComponent<RectTransform>();
            SetRect(rosterRect, new Vector2(0f, -280f), new Vector2(1080f, 700f));

            for (var i = 0; i < CharacterScreenCount; i++)
                BuildCharacterRosterCard(rosterBackdrop.transform, i);

            meCharactersContent.SetActive(true);
        }

        private void BuildCharactersHeader(Transform parent)
        {
            BuildCharacterTitleWing(parent, new Vector2(-405f, 716f), false);
            BuildCharacterTitleWing(parent, new Vector2(405f, 716f), true);

            var title = MakeText(parent, "CHARACTERS", 76, FontStyle.Bold, Color.white,
                new Vector2(0f, 716f), new Vector2(700f, 84f), TextAnchor.MiddleCenter, 4);
            AddGraphicOutline(title, Hex("102D63"), 4f);
            var titleGlow = title.gameObject.AddComponent<Shadow>();
            titleGlow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.68f);
            titleGlow.effectDistance = new Vector2(0f, -6f);

            MakeText(parent, "COLLECT HEROES AND RUN WITH STYLE!", 27, FontStyle.Bold,
                Hex("A8C6F4"), new Vector2(0f, 647f), new Vector2(860f, 38f),
                TextAnchor.MiddleCenter, 2);
        }

        private void BuildCharacterTitleWing(Transform parent, Vector2 position, bool mirror)
        {
            var rail = CreateImage("Character title wing rail", parent, Cyan,
                Centered(position, new Vector2(190f, 7f)), RoundedSprite(4));
            rail.raycastTarget = false;
            for (var i = 0; i < 3; i++)
            {
                var xOffset = (mirror ? -1f : 1f) * (60f + i * 23f);
                var slash = CreateImage("Character title wing slash " + i, parent, Hex("138EEB"),
                    Centered(position + new Vector2(xOffset, -13f - i * 7f),
                        new Vector2(62f, 8f)), RoundedSprite(4));
                slash.rectTransform.localEulerAngles = new Vector3(0f, 0f, mirror ? 42f : -42f);
                slash.raycastTarget = false;
            }
        }

        private void BuildCharacterActionBanner(Transform parent)
        {
            var actionGlow = CreateCard("Character action glow", parent, new Vector2(0f, -305f),
                new Vector2(382f, 68f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f), 18);
            actionGlow.GetComponent<Image>().raycastTarget = false;

            meCharacterActionButton = MakeButton(parent, string.Empty, new Vector2(0f, -305f),
                new Vector2(370f, 58f), Hex("0B5C82"), Color.white, 24,
                TryUnlockOrSelectPreviewCharacter);
            AddGraphicOutline(meCharacterActionButton.GetComponent<Image>(),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f), 2.2f);

            meCharacterActionLock = null;
            meCharacterActionCoin = new GameObject("Character action coin");
            meCharacterActionCoin.transform.SetParent(meCharacterActionButton.transform, false);
            var actionCoinRect = meCharacterActionCoin.AddComponent<RectTransform>();
            Stretch(actionCoinRect);
            BuildCanonicalGameCoin(meCharacterActionCoin.transform, new Vector2(-126f, 0f), 38f);

            meCharacterActionLabelText = MakeText(meCharacterActionButton.transform, "SELECTED", 24,
                FontStyle.Bold, NeonLime, new Vector2(24f, 0f), new Vector2(290f, 50f),
                TextAnchor.MiddleCenter, 2);
            meCharacterUnlockText = meCharacterActionLabelText;
        }

        private void BuildCharacterFeaturedArea(Transform parent)
        {
            meCharacterCollectionText = null;

            meCharacterName = MakeText(parent, "NOVA", 52, FontStyle.Bold, Color.white,
                new Vector2(0f, 315f), new Vector2(430f, 62f), TextAnchor.MiddleCenter, 3);
            AddGraphicOutline(meCharacterName, Hex("102952"), 2.2f);
            meCharacterRarityText = MakeText(parent, "CORE CREW", 21, FontStyle.Bold, Cyan,
                new Vector2(0f, 271f), new Vector2(330f, 34f), TextAnchor.MiddleCenter, 2);

            meCharacterShowcase = MakeTextureImage("Selected character showcase", parent,
                Resources.Load<Texture2D>(CharacterArtworkResource("nova")), new Vector2(0f, -20f),
                new Vector2(310f, 400f));
            meCharacterShowcase.material = CharacterArtworkMaterial(0);
            var featuredAspect = meCharacterShowcase.GetComponent<AspectRatioFitter>();
            if (featuredAspect != null)
                featuredAspect.enabled = false;
            SetFeaturedCharacterRect(meCharacterShowcase.texture);

            // Keep the identity text above the artwork in both layout and draw order.
            meCharacterName.transform.SetAsLastSibling();
            meCharacterRarityText.transform.SetAsLastSibling();

        }

        private void SetFeaturedCharacterRect(Texture texture)
        {
            const float showcaseHeight = 440f;
            var aspectRatio = texture != null && texture.height > 0
                ? (float)texture.width / texture.height
                : 2f / 3f;
            SetRect(meCharacterShowcase.rectTransform, new Vector2(0f, -20f),
                new Vector2(showcaseHeight * aspectRatio, showcaseHeight));
        }

        private void BuildCharacterRosterCard(Transform parent, int index)
        {
            var row = index / 5;
            var column = index % 5;
            var itemsInRow = Mathf.Min(5, CharacterScreenCount - row * 5);
            var rowStartX = -(itemsInRow - 1) * 100f;
            var position = new Vector2(rowStartX + column * 200f, 230f - row * 220f);
            var accent = index == 0 ? Cyan : Hex("1768B5");

            meAvatarBorders[index] = CreateCard(CharacterNames[index] + " selected neon glow", parent,
                position, new Vector2(198f, 204f), accent, 18).GetComponent<Image>();
            meAvatarBorders[index].raycastTarget = false;
            AddGraphicOutline(meAvatarBorders[index], new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f), 2.2f);
            var selectedGlow = meAvatarBorders[index].gameObject.AddComponent<Shadow>();
            selectedGlow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f);
            selectedGlow.effectDistance = new Vector2(0f, -5f);

            var card = CreateCard(CharacterNames[index] + " character slot", parent, position,
                new Vector2(190f, 196f), Hex("06152B"), 16);
            var cardImage = card.GetComponent<Image>();
            AddGraphicOutline(cardImage, new Color(0.12f, 0.36f, 0.67f, 0.92f), 1.5f);
            var button = card.AddComponent<Button>();
            button.targetGraphic = cardImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var characterId = CharacterIds[index];
            button.onClick.AddListener(() => PreviewCharacter(characterId));

            var portraitViewport = CreateCard(CharacterNames[index] + " portrait viewport", card.transform,
                new Vector2(0f, 21f), new Vector2(178f, 142f), Hex("020817"), 12);
            portraitViewport.GetComponent<Image>().raycastTarget = false;
            portraitViewport.AddComponent<RectMask2D>();

            meAvatarPortraits[index] = MakeTextureImage(CharacterNames[index] + " roster art",
                portraitViewport.transform, Resources.Load<Texture2D>(CharacterArtworkResource(CharacterIds[index])),
                Vector2.zero, new Vector2(168f, 136f));
            var rosterAspect = meAvatarPortraits[index].GetComponent<AspectRatioFitter>();
            if (rosterAspect != null)
                rosterAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            meAvatarPortraits[index].material = CharacterArtworkMaterial(index);

            MakeText(card.transform, CharacterNames[index], 17, FontStyle.Bold, Color.white,
                new Vector2(0f, -61f), new Vector2(174f, 25f), TextAnchor.MiddleCenter, 1);
            meAvatarStatusTexts[index] = MakeText(card.transform, CharacterRarities[index], 9,
                FontStyle.Bold, Hex("8FB7E9"), new Vector2(0f, -82f), new Vector2(178f, 18f),
                TextAnchor.MiddleCenter, 1);

            meCharacterCostGroups[index] = new GameObject(CharacterNames[index] + " unlock cost");
            meCharacterCostGroups[index].transform.SetParent(card.transform, false);
            var costGroupRect = meCharacterCostGroups[index].AddComponent<RectTransform>();
            Stretch(costGroupRect);

            meCharacterLockOverlays[index] = new GameObject(CharacterNames[index] + " lock indicator");
            meCharacterLockOverlays[index].transform.SetParent(card.transform, false);
            var lockRect = meCharacterLockOverlays[index].AddComponent<RectTransform>();
            Stretch(lockRect);
            BuildCharacterPadlock(meCharacterLockOverlays[index].transform,
                new Vector2(-76f, 75f), 25f, Hex("D5E5FA"));

            var check = CreateCard(CharacterNames[index] + " selected check", card.transform,
                new Vector2(72f, -69f), new Vector2(40f, 40f), Hex("28C765"), 20);
            check.GetComponent<Image>().raycastTarget = false;
            AddGraphicOutline(check.GetComponent<Image>(), Hex("C5FFD7"), 2f);
            MakeText(check.transform, "✓", 24, FontStyle.Bold, Color.white, Vector2.zero,
                new Vector2(34f, 34f), TextAnchor.MiddleCenter);
            meCharacterCheckmarks[index] = check;
        }

        private GameObject BuildCharacterPadlock(Transform parent, Vector2 position, float size, Color color)
        {
            var holder = new GameObject("Padlock icon");
            holder.transform.SetParent(parent, false);
            var holderRect = holder.AddComponent<RectTransform>();
            SetRect(holderRect, position, new Vector2(size, size));

            var shackle = CreateCard("Padlock shackle", holder.transform, new Vector2(0f, size * 0.18f),
                new Vector2(size * 0.62f, size * 0.64f), color, Mathf.RoundToInt(size * 0.30f));
            shackle.GetComponent<Image>().raycastTarget = false;
            var opening = CreateCard("Padlock shackle opening", shackle.transform,
                new Vector2(0f, -size * 0.07f), new Vector2(size * 0.34f, size * 0.42f), Hex("07162C"),
                Mathf.RoundToInt(size * 0.18f));
            opening.GetComponent<Image>().raycastTarget = false;
            var body = CreateCard("Padlock body", holder.transform, new Vector2(0f, -size * 0.20f),
                new Vector2(size * 0.82f, size * 0.62f), color, Mathf.RoundToInt(size * 0.12f));
            body.GetComponent<Image>().raycastTarget = false;
            body.transform.SetAsLastSibling();
            return holder;
        }
    }
}
