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
        // ── Country data ────────────────────────────────────────────────────
        private static readonly (string code, string name)[] Countries =
        {
            ("AD","Andorra"),("AE","United Arab Emirates"),("AF","Afghanistan"),
            ("AL","Albania"),("AM","Armenia"),("AO","Angola"),
            ("AR","Argentina"),("AT","Austria"),("AU","Australia"),
            ("AZ","Azerbaijan"),("BA","Bosnia and Herzegovina"),("BD","Bangladesh"),
            ("BE","Belgium"),("BG","Bulgaria"),("BH","Bahrain"),
            ("BN","Brunei"),("BO","Bolivia"),("BR","Brazil"),
            ("BY","Belarus"),("CA","Canada"),("CD","DR Congo"),
            ("CH","Switzerland"),("CL","Chile"),("CM","Cameroon"),
            ("CN","China"),("CO","Colombia"),("CR","Costa Rica"),
            ("CU","Cuba"),("CY","Cyprus"),("CZ","Czechia"),
            ("DE","Germany"),("DK","Denmark"),("DO","Dominican Republic"),
            ("DZ","Algeria"),("EC","Ecuador"),("EE","Estonia"),
            ("EG","Egypt"),("ES","Spain"),("ET","Ethiopia"),
            ("FI","Finland"),("FR","France"),("GB","United Kingdom"),
            ("GE","Georgia"),("GH","Ghana"),("GR","Greece"),
            ("GT","Guatemala"),("HK","Hong Kong"),("HN","Honduras"),
            ("HR","Croatia"),("HU","Hungary"),("ID","Indonesia"),
            ("IE","Ireland"),("IL","Israel"),("IN","India"),
            ("IQ","Iraq"),("IR","Iran"),("IS","Iceland"),
            ("IT","Italy"),("JM","Jamaica"),("JO","Jordan"),
            ("JP","Japan"),("KE","Kenya"),("KG","Kyrgyzstan"),
            ("KH","Cambodia"),("KR","South Korea"),("KW","Kuwait"),
            ("KZ","Kazakhstan"),("LA","Laos"),("LB","Lebanon"),
            ("LK","Sri Lanka"),("LT","Lithuania"),("LU","Luxembourg"),
            ("LV","Latvia"),("LY","Libya"),("MA","Morocco"),
            ("MD","Moldova"),("ME","Montenegro"),("MK","North Macedonia"),
            ("MM","Myanmar"),("MN","Mongolia"),("MO","Macao"),
            ("MT","Malta"),("MU","Mauritius"),("MV","Maldives"),
            ("MX","Mexico"),("MY","Malaysia"),("MZ","Mozambique"),
            ("NA","Namibia"),("NG","Nigeria"),("NI","Nicaragua"),
            ("NL","Netherlands"),("NO","Norway"),("NP","Nepal"),
            ("NZ","New Zealand"),("OM","Oman"),("PA","Panama"),
            ("PE","Peru"),("PH","Philippines"),("PK","Pakistan"),
            ("PL","Poland"),("PR","Puerto Rico"),("PS","Palestine"),
            ("PT","Portugal"),("PY","Paraguay"),("QA","Qatar"),
            ("RO","Romania"),("RS","Serbia"),("RU","Russia"),
            ("RW","Rwanda"),("SA","Saudi Arabia"),("SD","Sudan"),
            ("SE","Sweden"),("SG","Singapore"),("SI","Slovenia"),
            ("SK","Slovakia"),("SN","Senegal"),("SO","Somalia"),
            ("SV","El Salvador"),("SY","Syria"),("TH","Thailand"),
            ("TN","Tunisia"),("TR","Turkey"),("TT","Trinidad and Tobago"),
            ("TW","Taiwan"),("TZ","Tanzania"),("UA","Ukraine"),
            ("UG","Uganda"),("US","United States"),("UY","Uruguay"),
            ("UZ","Uzbekistan"),("VE","Venezuela"),("VN","Vietnam"),
            ("YE","Yemen"),("ZA","South Africa"),("ZM","Zambia"),
            ("ZW","Zimbabwe"),
        };

        // ── Step state ──────────────────────────────────────────────────────
        private string selectedCountryCode = "";
        private string selectedCountryName = "";

        // ── Step UI references ──────────────────────────────────────────────
        private CanvasGroup ageStepGroup;
        private CanvasGroup countryStepGroup;
        private InputField countrySearchInput;
        private Text countrySelectedLabel;
        private RawImage countrySelectedFlagImage;
        private Text countrySelectedFlagFallback;
        private GameObject countryDropdownPanel;
        private Text countryDropdownArrow;
        private RectTransform countryListContent;
        private ScrollRect countryScrollRect;
        private readonly List<GameObject> countryRowPool = new();
        private Text countryResultCountText;
        private bool countryDropdownOpen;

        // Static local textures, one exact file per ISO country code. Keeping each flag separate prevents
        // atlas UV rounding, neighbouring-cell bleed and partial/doubled flags on different GPU formats.
        private readonly Dictionary<string, Texture2D> countryFlagTextures = new();
        private const int CountryFlagPixelWidth = 96;
        private const int CountryFlagPixelHeight = 64;

        // ── Age step UI references ──────────────────────────────────────────
        private Slider ageStepSlider;
        private Text ageStepValueText;
        private Text ageStepHintText;

        // ── Step indicator ──────────────────────────────────────────────────
        private Text stepIndicator1;
        private Text stepIndicator2;
        private Image stepDot1;
        private Image stepDot2;
        private Image stepConnector;

        // ════════════════════════════════════════════════════════════════════
        //  BUILD
        // ════════════════════════════════════════════════════════════════════

        private void BuildAgeScreen()
        {
            var screen = CreateScreen("Age");

            // ── Header ──────────────────────────────────────────────────────
            MakeText(screen.transform, "AGE CHECK", 28, FontStyle.Bold, Cyan,
                new Vector2(0, 780), new Vector2(760, 55), TextAnchor.MiddleCenter, 4);
            MakeText(screen.transform, "Before we bend gravity…", 55, FontStyle.Bold, Cream,
                new Vector2(0, 690), new Vector2(940, 100), TextAnchor.MiddleCenter);
            MakeText(screen.transform, "Tell us your age and region so we can shape a safer experience.", 27,
                FontStyle.Normal, Muted, new Vector2(0, 610), new Vector2(850, 90), TextAnchor.MiddleCenter);

            // ── Decorative characters ───────────────────────────────────────
            AddCharacter(screen.transform, "Art/Characters/atlas", new Vector2(-310, 260), new Vector2(400, 600), -4f, 0.7f);
            AddCharacter(screen.transform, "Art/Characters/nova", new Vector2(0, 330), new Vector2(430, 650), 0f, 0.95f);
            AddCharacter(screen.transform, "Art/Characters/pip", new Vector2(320, 270), new Vector2(365, 550), 4f, 1.25f);

            // ── Step indicator ──────────────────────────────────────────────
            BuildStepIndicator(screen.transform);

            // ── Age card ────────────────────────────────────────────────────
            var card = CreateCard("Age Card", screen.transform, new Vector2(0, -175), new Vector2(890, 860), Card, 42);
            card.transform.SetAsLastSibling();
            ageStepGroup = card.AddComponent<CanvasGroup>();
            ageStepGroup.alpha = 1f;
            ageStepGroup.blocksRaycasts = true;
            ageStepGroup.interactable = true;
            BuildAgeStep(card.transform);

            // ── Country card (hidden) ───────────────────────────────────────
            var countryCard = CreateCard("Country Card", screen.transform, new Vector2(0, -175), new Vector2(890, 860), Card, 42);
            countryCard.transform.SetAsLastSibling();
            countryStepGroup = countryCard.AddComponent<CanvasGroup>();
            countryStepGroup.alpha = 0f;
            countryStepGroup.blocksRaycasts = false;
            countryStepGroup.interactable = false;
            countryCard.SetActive(false);
            BuildCountryStep(countryCard.transform);

            // ── Footer ──────────────────────────────────────────────────────
            MakeText(screen.transform, "You can review privacy choices later in Settings.", 21,
                FontStyle.Normal, Muted, new Vector2(0, -845), new Vector2(850, 45), TextAnchor.MiddleCenter);
        }

        // ────────────────────────────────────────────────────────────────────
        //  STEP INDICATOR
        // ────────────────────────────────────────────────────────────────────

        private void BuildStepIndicator(Transform parent)
        {
            var dot1Obj = CreateImage("Step Dot 1", parent, Cyan,
                Centered(new Vector2(-60, 490), new Vector2(22, 22)), RoundedSprite(11));
            stepDot1 = dot1Obj;
            stepIndicator1 = MakeText(parent, "1", 16, FontStyle.Bold, Ink,
                new Vector2(-60, 490), new Vector2(22, 22), TextAnchor.MiddleCenter);

            stepConnector = CreateImage("Step Connector", parent, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f),
                Centered(new Vector2(0, 490), new Vector2(56, 4)), RoundedSprite(2));

            var dot2Obj = CreateImage("Step Dot 2", parent, CardSoft,
                Centered(new Vector2(60, 490), new Vector2(22, 22)), RoundedSprite(11));
            stepDot2 = dot2Obj;
            stepIndicator2 = MakeText(parent, "2", 16, FontStyle.Bold, Muted,
                new Vector2(60, 490), new Vector2(22, 22), TextAnchor.MiddleCenter);

            MakeText(parent, "AGE", 17, FontStyle.Bold, Cyan,
                new Vector2(-60, 465), new Vector2(80, 30), TextAnchor.MiddleCenter);
            MakeText(parent, "COUNTRY", 17, FontStyle.Bold, Muted,
                new Vector2(60, 465), new Vector2(100, 30), TextAnchor.MiddleCenter);
        }

        private void UpdateStepIndicator(int step)
        {
            if (step == 1)
            {
                stepDot1.color = Cyan;
                stepDot2.color = CardSoft;
                stepIndicator1.color = Ink;
                stepIndicator2.color = Muted;
                stepConnector.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
            }
            else
            {
                stepDot1.color = Green;
                stepDot2.color = Cyan;
                stepIndicator1.color = Ink;
                stepIndicator2.color = Ink;
                stepConnector.color = Green;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  STEP 1 — AGE
        // ════════════════════════════════════════════════════════════════════

        private void BuildAgeStep(Transform card)
        {
            // ── Title ───────────────────────────────────────────────────────
            MakeText(card.transform, "HOW OLD ARE YOU?", 31, FontStyle.Bold, Muted,
                new Vector2(0, 330), new Vector2(760, 55), TextAnchor.MiddleCenter, 2);

            // ── Big age number (the centrepiece) ────────────────────────────
            ageStepValueText = MakeText(card.transform, selectedAge.ToString(), 140, FontStyle.Bold, Cream,
                new Vector2(0, 210), new Vector2(500, 190), TextAnchor.MiddleCenter);
            ageStepValueText.gameObject.AddComponent<SoftPulse>();

            // ── Description ─────────────────────────────────────────────────
            ageStepHintText = MakeText(card.transform,
                "You're in control — this is saved as an age group only.",
                24, FontStyle.Normal, Muted, new Vector2(0, 120), new Vector2(700, 60), TextAnchor.MiddleCenter);

            // ── Slider (cyan, always) ───────────────────────────────────────
            ageStepSlider = BuildCyanSlider(card.transform, new Vector2(0, 10), new Vector2(760, 110));
            ageStepSlider.minValue = 3;
            ageStepSlider.maxValue = 65;
            ageStepSlider.wholeNumbers = true;
            ageStepSlider.value = selectedAge;
            ageStepSlider.onValueChanged.AddListener(OnAgeChanged);

            // ── Privacy pill ────────────────────────────────────────────────
            var privacyPill = CreateCard("Privacy Note", card.transform, new Vector2(0, -110), new Vector2(700, 82), CardSoft, 24);
            MakeText(privacyPill.transform, "●  We store an age range — never your birthday", 23,
                FontStyle.Normal, Green, Vector2.zero, new Vector2(640, 58), TextAnchor.MiddleCenter);

            // ── Continue ────────────────────────────────────────────────────
            var continueButton = MakeButton(card.transform, "CONTINUE", new Vector2(0, -240),
                new Vector2(700, 116), Cyan, Ink, 30, OnAgeStepContinue);
            continueButton.gameObject.AddComponent<ButtonGlow>();
        }

        /// <summary>Cyan slider — always cyan, tap-to-jump enabled.</summary>
        private Slider BuildCyanSlider(Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject("Age Slider");
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            var slider = root.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            // Track
            CreateImage("Track BG", root.transform, Hex("1A3050"),
                Centered(Vector2.zero, new Vector2(size.x, 38)), RoundedSprite(19));

            // Track glow
            CreateImage("Track Glow", root.transform,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.12f),
                Centered(Vector2.zero, new Vector2(size.x + 8, 46)), RoundedSprite(23));

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(root.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1, 0.5f);
            fillAreaRect.offsetMin = new Vector2(4, -18);
            fillAreaRect.offsetMax = new Vector2(-4, 18);

            var fill = CreateImage("Fill", fillArea.transform, Cyan,
                FullStretch(), RoundedSprite(19));
            slider.fillRect = fill.rectTransform;

            // Handle slide area (wide for tap-to-jump)
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(root.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(0, -40);
            handleAreaRect.offsetMax = new Vector2(0, 40);

            // Glow ring
            var handleGlow = CreateImage("Handle Glow", handleArea.transform,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f),
                Centered(Vector2.zero, new Vector2(92, 92)), RoundedSprite(46));
            handleGlow.rectTransform.anchorMin = new Vector2(0, 0.5f);
            handleGlow.rectTransform.anchorMax = new Vector2(0, 0.5f);

            // Handle
            var handle = CreateImage("Handle", handleArea.transform, Cream,
                Centered(Vector2.zero, new Vector2(72, 72)), RoundedSprite(36));
            handle.rectTransform.anchorMin = new Vector2(0, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0, 0.5f);

            // Inner dot
            var handleDot = CreateImage("Handle Dot", handle.transform, Cyan,
                Centered(Vector2.zero, new Vector2(22, 22)), RoundedSprite(11));
            handleDot.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            handleDot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private void OnAgeChanged(float value)
        {
            selectedAge = Mathf.RoundToInt(value);
            ageStepValueText.text = selectedAge >= 65 ? "65+" : selectedAge.ToString();
            ageStepHintText.text = selectedAge switch
            {
                < 13 => "Child experience · limited data and age-appropriate ads",
                < 16 => "Teen experience · privacy-first personalization",
                < 18 => "Older teen experience · standard safety controls",
                _ => "Adult experience · full personalization controls"
            };
        }

        private async void OnAgeStepContinue()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            PlayerPrefs.SetString(AgeGroupKey, AgeGroupFor(selectedAge));
            PlayerPrefs.SetInt(AgeConfirmedKey, 1);

            UpdateStepIndicator(2);
            const float duration = 0.32f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float progress = Smooth(t / duration);
                ageStepGroup.alpha = 1f - progress;
                countryStepGroup.alpha = progress;
                await Task.Yield();
            }

            ageStepGroup.alpha = 0f;
            ageStepGroup.blocksRaycasts = false;
            ageStepGroup.interactable = false;

            countryStepGroup.gameObject.SetActive(true);
            countryStepGroup.alpha = 1f;
            countryStepGroup.blocksRaycasts = true;
            countryStepGroup.interactable = true;
            isTransitioning = false;
        }

        // ════════════════════════════════════════════════════════════════════
        //  STEP 2 — COUNTRY
        // ════════════════════════════════════════════════════════════════════

        private void BuildCountryStep(Transform card)
        {
            // ── Title ───────────────────────────────────────────────────────
            MakeText(card.transform, "WHERE ARE YOU FROM?", 31, FontStyle.Bold, Muted,
                new Vector2(0, 330), new Vector2(760, 55), TextAnchor.MiddleCenter, 2);
            MakeText(card.transform, "Choose your country. You can search by country name or code.",
                22, FontStyle.Normal, Muted, new Vector2(0, 282), new Vector2(740, 50), TextAnchor.MiddleCenter);

            // Restore a previously selected country when this screen is rebuilt.
            if (string.IsNullOrEmpty(selectedCountryCode))
            {
                selectedCountryCode = PlayerPrefs.GetString("ghd.country_code", "");
                selectedCountryName = PlayerPrefs.GetString("ghd.country_name", "");
            }

            // ── Closed dropdown field ───────────────────────────────────────
            var selector = CreateCard("Country Dropdown", card.transform,
                new Vector2(0, 190), new Vector2(760, 94), Hex("0A1A2E"), 28);
            AddSettingsOutline(selector.GetComponent<Image>(), Hex("24486B"), 1.4f);

            var selectorButton = selector.AddComponent<Button>();
            selectorButton.targetGraphic = selector.GetComponent<Image>();
            selectorButton.transition = Selectable.Transition.ColorTint;
            selectorButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var selectorColors = selectorButton.colors;
            selectorColors.normalColor = Color.white;
            selectorColors.highlightedColor = new Color(1.05f, 1.08f, 1.10f, 1f);
            selectorColors.pressedColor = new Color(0.86f, 0.92f, 0.96f, 1f);
            selectorColors.fadeDuration = 0.06f;
            selectorButton.colors = selectorColors;
            selectorButton.onClick.AddListener(ToggleCountryDropdown);

            var flagHolder = CreateCard("Selected Country Flag Holder", selector.transform,
                new Vector2(-316, 0), new Vector2(68, 50), Hex("071421"), 10);
            flagHolder.GetComponent<Image>().raycastTarget = false;
            AddSettingsOutline(flagHolder.GetComponent<Image>(), Hex("2E5F7D"), 1f);

            var selectedFlagObject = new GameObject("Selected Country Flag");
            selectedFlagObject.transform.SetParent(flagHolder.transform, false);
            var selectedFlagRect = selectedFlagObject.AddComponent<RectTransform>();
            SetRect(selectedFlagRect, Vector2.zero, new Vector2(60, 40));
            var selectedFlagAspect = selectedFlagObject.AddComponent<AspectRatioFitter>();
            selectedFlagAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            selectedFlagAspect.aspectRatio = CountryFlagPixelWidth / (float)CountryFlagPixelHeight;
            countrySelectedFlagImage = selectedFlagObject.AddComponent<RawImage>();
            countrySelectedFlagImage.raycastTarget = false;
            countrySelectedFlagImage.color = Color.white;

            countrySelectedFlagFallback = MakeText(flagHolder.transform, "", 29, FontStyle.Normal, Color.white,
                Vector2.zero, new Vector2(62, 46), TextAnchor.MiddleCenter);
            countrySelectedFlagFallback.raycastTarget = false;

            countrySelectedLabel = MakeText(selector.transform, "Select your country", 26, FontStyle.Bold, Cream,
                new Vector2(22, 0), new Vector2(560, 58), TextAnchor.MiddleLeft);
            countrySelectedLabel.raycastTarget = false;

            countryDropdownArrow = MakeText(selector.transform, "▼", 24, FontStyle.Bold, Cyan,
                new Vector2(326, 0), new Vector2(62, 58), TextAnchor.MiddleCenter);
            countryDropdownArrow.raycastTarget = false;

            // ── Floating dropdown panel (hidden until selector is tapped) ───
            countryDropdownPanel = CreateCard("Country Dropdown Panel", card.transform,
                new Vector2(0, -66), new Vector2(760, 408), Hex("081728"), 28);
            AddSettingsOutline(countryDropdownPanel.GetComponent<Image>(), Hex("24486B"), 1.25f);
            countryDropdownPanel.transform.SetAsLastSibling();

            countrySearchInput = BuildCountrySearch(countryDropdownPanel.transform,
                new Vector2(0, 156), new Vector2(700, 68));
            countrySearchInput.onValueChanged.AddListener(OnCountrySearchChanged);

            // Keep the dropdown/search visible above the iOS/Android soft keyboard.
            // The component reads the real keyboard height and only lifts this floating
            // panel by the amount that is actually occluded; no fixed device-specific offset.
            var keyboardAvoider = countryDropdownPanel.AddComponent<CountryKeyboardAvoider>();
            keyboardAvoider.Configure(
                countryDropdownPanel.GetComponent<RectTransform>(),
                countrySearchInput,
                28f,
                24f);

            BuildCountryScrollList(countryDropdownPanel.transform,
                new Vector2(0, -14), new Vector2(700, 260));

            countryResultCountText = MakeText(countryDropdownPanel.transform, "", 17, FontStyle.Normal,
                Hex("6F88A5"), new Vector2(0, -172), new Vector2(680, 28), TextAnchor.MiddleCenter);
            countryResultCountText.raycastTarget = false;

            countryDropdownPanel.SetActive(false);
            countryDropdownOpen = false;
            RefreshCountrySelectorVisual();

            // ── Buttons ─────────────────────────────────────────────────────
            var backButton = MakeButton(card.transform, "BACK", new Vector2(-210, -370),
                new Vector2(300, 90), Hex("163A61"), Muted, 24, OnCountryStepBack);
            AddSettingsOutline(backButton.GetComponent<Image>(), Hex("2A5080"), 1f);

            var confirmButton = MakeButton(card.transform, "CONFIRM", new Vector2(140, -370),
                new Vector2(440, 90), Cyan, Ink, 28, OnCountryStepConfirm);
            confirmButton.gameObject.AddComponent<ButtonGlow>();
        }

        private void ToggleCountryDropdown()
        {
            SetCountryDropdownOpen(!countryDropdownOpen);
        }

        private void SetCountryDropdownOpen(bool open)
        {
            countryDropdownOpen = open;

            // Close the soft keyboard before hiding the dropdown so the panel can
            // settle back to its normal position immediately.
            if (!open && countrySearchInput != null)
            {
                countrySearchInput.DeactivateInputField();
                if (EventSystem.current != null &&
                    EventSystem.current.currentSelectedGameObject == countrySearchInput.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            if (countryDropdownPanel != null)
            {
                countryDropdownPanel.SetActive(open);
                if (open)
                    countryDropdownPanel.transform.SetAsLastSibling();
            }

            if (countryDropdownArrow != null)
                countryDropdownArrow.text = open ? "▲" : "▼";

            if (open)
            {
                if (countrySearchInput != null)
                {
                    countrySearchInput.SetTextWithoutNotify("");
                    countrySearchInput.interactable = true;
                }
                PopulateCountryList("");
            }
        }

        private void RefreshCountrySelectorVisual()
        {
            bool hasSelection = !string.IsNullOrEmpty(selectedCountryCode);
            if (countrySelectedLabel != null)
            {
                countrySelectedLabel.text = hasSelection
                    ? selectedCountryName
                    : "Select your country";
                countrySelectedLabel.color = hasSelection ? Cream : Muted;
            }

            ApplyCountryFlag(countrySelectedFlagImage, countrySelectedFlagFallback,
                hasSelection ? selectedCountryCode : null);
        }

        // ── Search bar ──────────────────────────────────────────────────────

        private InputField BuildCountrySearch(Transform parent, Vector2 position, Vector2 size)
        {
            var fieldObject = new GameObject("Country Search");
            fieldObject.transform.SetParent(parent, false);
            var rect = fieldObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);

            var bg = fieldObject.AddComponent<Image>();
            bg.sprite = RoundedSprite(26);
            bg.type = Image.Type.Sliced;
            bg.color = Hex("0C2035");
            bg.raycastTarget = true;
            AddSettingsOutline(bg, Hex("2B5878"), 1f);

            var input = fieldObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 40;
            input.interactable = true;
            input.shouldHideMobileInput = true;
            input.keyboardType = TouchScreenKeyboardType.Default;
            input.caretWidth = 3;
            input.caretBlinkRate = 0.75f;
            input.selectionColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.32f);
            input.navigation = new Navigation { mode = Navigation.Mode.None };

            var searchBadge = CreateCard("Search Icon Badge", fieldObject.transform,
                new Vector2(-size.x / 2f + 38, 0), new Vector2(46, 46), Hex("123653"), 16);
            searchBadge.GetComponent<Image>().raycastTarget = false;
            var searchGlyph = MakeText(searchBadge.transform, "⌕", 30, FontStyle.Bold, Cyan,
                Vector2.zero, new Vector2(42, 42), TextAnchor.MiddleCenter);
            searchGlyph.raycastTarget = false;

            var text = MakeText(fieldObject.transform, "", 24, FontStyle.Normal, Cream,
                new Vector2(48, 0), new Vector2(size.x - 126, 52), TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.raycastTarget = false;
            input.textComponent = text;

            var placeholder = MakeText(fieldObject.transform, "Search country or code…", 23, FontStyle.Italic,
                Hex("6A819B"), new Vector2(48, 0), new Vector2(size.x - 126, 52), TextAnchor.MiddleLeft);
            placeholder.supportRichText = false;
            placeholder.raycastTarget = false;
            input.placeholder = placeholder;

            return input;
        }

        // ── Scrollable dropdown list ─────────────────────────────────────────

        private void BuildCountryScrollList(Transform parent, Vector2 position, Vector2 size)
        {
            var scrollRoot = new GameObject("Country Results");
            scrollRoot.transform.SetParent(parent, false);
            var scrollRect = scrollRoot.AddComponent<RectTransform>();
            SetRect(scrollRect, position, size);

            countryScrollRect = scrollRoot.AddComponent<ScrollRect>();
            countryScrollRect.horizontal = false;
            countryScrollRect.vertical = true;
            countryScrollRect.movementType = ScrollRect.MovementType.Clamped;
            countryScrollRect.scrollSensitivity = 34f;
            countryScrollRect.inertia = true;
            countryScrollRect.decelerationRate = 0.12f;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollRoot.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            Stretch(vpRect);
            var vpImage = viewport.AddComponent<Image>();
            vpImage.color = new Color(1f, 1f, 1f, 0.001f);
            vpImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            countryListContent = content.AddComponent<RectTransform>();
            countryListContent.anchorMin = new Vector2(0, 1);
            countryListContent.anchorMax = new Vector2(1, 1);
            countryListContent.pivot = new Vector2(0.5f, 1);
            countryListContent.anchoredPosition = Vector2.zero;
            countryListContent.sizeDelta = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(2, 2, 2, 2);

            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            countryScrollRect.content = countryListContent;
            countryScrollRect.viewport = vpRect;

            PopulateCountryList("");
        }

        private void PopulateCountryList(string filter)
        {
            if (countryListContent == null)
                return;

            foreach (var row in countryRowPool)
                if (row != null)
                    Destroy(row);
            countryRowPool.Clear();

            string query = (filter ?? string.Empty).Trim().ToLowerInvariant();
            const float rowHeight = 66f;
            int matchCount = 0;

            foreach (var (code, name) in Countries)
            {
                if (!CountryMatchesSearch(code, name, query))
                    continue;

                matchCount++;
                bool isSelected = code == selectedCountryCode;

                var row = CreateCard("Country " + code, countryListContent,
                    Vector2.zero, new Vector2(0, rowHeight),
                    isSelected ? Hex("123650") : Hex("0C2035"), 18);

                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = rowHeight;
                le.minHeight = rowHeight;

                if (isSelected)
                {
                    var accent = CreateCard("Selected Accent", row.transform,
                        new Vector2(-342, 0), new Vector2(5, rowHeight - 14), Cyan, 3);
                    accent.GetComponent<Image>().raycastTarget = false;
                }

                var flagHolder = CreateCard("Flag Holder " + code, row.transform,
                    new Vector2(-300, 0), new Vector2(58, 42), Hex("071421"), 9);
                flagHolder.GetComponent<Image>().raycastTarget = false;

                var flagObject = new GameObject("Flag " + code);
                flagObject.transform.SetParent(flagHolder.transform, false);
                var flagRect = flagObject.AddComponent<RectTransform>();
                SetRect(flagRect, Vector2.zero, new Vector2(54, 36));
                var rowFlagAspect = flagObject.AddComponent<AspectRatioFitter>();
                rowFlagAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                rowFlagAspect.aspectRatio = CountryFlagPixelWidth / (float)CountryFlagPixelHeight;
                var flagImage = flagObject.AddComponent<RawImage>();
                flagImage.color = Color.white;
                flagImage.raycastTarget = false;

                var flagFallback = MakeText(flagHolder.transform, "", 27, FontStyle.Normal, Color.white,
                    Vector2.zero, new Vector2(54, 40), TextAnchor.MiddleCenter);
                flagFallback.raycastTarget = false;
                ApplyCountryFlag(flagImage, flagFallback, code);

                var countryName = MakeText(row.transform, name, 23, FontStyle.Bold,
                    isSelected ? Cyan : Cream,
                    new Vector2(10, 7), new Vector2(540, 34), TextAnchor.MiddleLeft);
                countryName.raycastTarget = false;

                var countryCode = MakeText(row.transform, code, 15, FontStyle.Bold, Hex("6F88A5"),
                    new Vector2(10, -18), new Vector2(540, 24), TextAnchor.MiddleLeft, 1);
                countryCode.raycastTarget = false;

                if (isSelected)
                {
                    var check = MakeText(row.transform, "✓", 27, FontStyle.Bold, Cyan,
                        new Vector2(316, 0), new Vector2(44, 48), TextAnchor.MiddleCenter);
                    check.raycastTarget = false;
                }

                var btn = row.AddComponent<Button>();
                btn.targetGraphic = row.GetComponent<Image>();
                btn.transition = Selectable.Transition.ColorTint;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.06f, 1.08f, 1.10f, 1f);
                colors.pressedColor = new Color(0.82f, 0.90f, 0.95f, 1f);
                colors.fadeDuration = 0.05f;
                btn.colors = colors;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                string capturedCode = code;
                string capturedName = name;
                btn.onClick.AddListener(() => OnCountrySelected(capturedCode, capturedName));

                countryRowPool.Add(row);
            }

            if (matchCount == 0)
            {
                var emptyRow = CreateCard("No Country Matches", countryListContent,
                    Vector2.zero, new Vector2(0, 84), Hex("0C2035"), 18);
                var emptyLayout = emptyRow.AddComponent<LayoutElement>();
                emptyLayout.preferredHeight = 84;
                emptyLayout.minHeight = 84;
                var emptyText = MakeText(emptyRow.transform, "No countries found\nTry another name or 2-letter code",
                    19, FontStyle.Normal, Muted, Vector2.zero, new Vector2(620, 66), TextAnchor.MiddleCenter);
                emptyText.raycastTarget = false;
                countryRowPool.Add(emptyRow);
            }

            if (countryResultCountText != null)
                countryResultCountText.text = matchCount == 1 ? "1 country" : matchCount + " countries";

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(countryListContent);
            if (countryScrollRect != null)
                countryScrollRect.verticalNormalizedPosition = 1f;
        }

        private static bool CountryMatchesSearch(string code, string name, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            if (name.ToLowerInvariant().Contains(query) || code.ToLowerInvariant().Contains(query))
                return true;

            // Friendly aliases people commonly type.
            return (code == "US" && (query == "usa" || query.Contains("america")))
                || (code == "GB" && (query == "uk" || query.Contains("britain")))
                || (code == "AE" && query == "uae")
                || (code == "KR" && query == "korea")
                || (code == "CD" && query.Contains("congo"));
        }

        private Texture2D LoadCountryFlag(string countryCode)
        {
            var normalizedCode = (countryCode ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedCode.Length != 2)
                return null;
            if (countryFlagTextures.TryGetValue(normalizedCode, out var cached))
                return cached;

            var texture = Resources.Load<Texture2D>("UI/CountryFlags/" + normalizedCode);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 0;
                countryFlagTextures[normalizedCode] = texture;
            }
            return texture;
        }

        private void ApplyCountryFlag(RawImage image, Text fallback, string countryCode)
        {
            if (image == null || fallback == null)
                return;

            if (string.IsNullOrEmpty(countryCode))
            {
                image.texture = null;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                image.gameObject.SetActive(false);
                fallback.gameObject.SetActive(true);
                fallback.text = "?";
                fallback.color = Muted;
                return;
            }

            var flag = LoadCountryFlag(countryCode);
            if (flag != null)
            {
                image.texture = flag;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                image.gameObject.SetActive(true);
                fallback.gameObject.SetActive(false);
            }
            else
            {
                image.texture = null;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                image.gameObject.SetActive(false);
                fallback.gameObject.SetActive(true);
                fallback.text = CountryFlag(countryCode);
                fallback.color = Color.white;
            }
        }

        private string CountryFlag(string countryCode)
        {
            // Emoji is only an emergency fallback if the atlas asset is missing.
            if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
                return "?";
            countryCode = countryCode.ToUpperInvariant();
            return char.ConvertFromUtf32(0x1F1E6 + (countryCode[0] - 'A'))
                 + char.ConvertFromUtf32(0x1F1E6 + (countryCode[1] - 'A'));
        }

        private void OnCountrySelected(string code, string name)
        {
            selectedCountryCode = code;
            selectedCountryName = name;
            RefreshCountrySelectorVisual();

            if (countrySearchInput != null)
                countrySearchInput.SetTextWithoutNotify("");

            PopulateCountryList("");
            SetCountryDropdownOpen(false);
        }

        private void OnCountrySearchChanged(string value)
        {
            PopulateCountryList(value);
        }

        private async void OnCountryStepBack()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            UpdateStepIndicator(1);
            const float duration = 0.28f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float progress = Smooth(t / duration);
                countryStepGroup.alpha = 1f - progress;
                ageStepGroup.alpha = progress;
                await Task.Yield();
            }

            countryStepGroup.alpha = 0f;
            countryStepGroup.blocksRaycasts = false;
            countryStepGroup.interactable = false;
            countryStepGroup.gameObject.SetActive(false);

            ageStepGroup.alpha = 1f;
            ageStepGroup.blocksRaycasts = true;
            ageStepGroup.interactable = true;
            isTransitioning = false;
        }

        private async void OnCountryStepConfirm()
        {
            if (isTransitioning) return;

            if (string.IsNullOrEmpty(selectedCountryCode))
            {
                if (countrySelectedLabel != null)
                    countrySelectedLabel.text = "<color=#FF795F>Please select a country</color>";
                return;
            }

            PlayerPrefs.SetString("ghd.country_code", selectedCountryCode);
            PlayerPrefs.SetString("ghd.country_name", selectedCountryName);
            PlayerPrefs.Save();

            isTransitioning = true;
            _ = SyncCountryToFirestoreAsync(selectedCountryCode, selectedCountryName);
            await ShowScreenAsync("Auth");
        }

        // ════════════════════════════════════════════════════════════════════
        //  FIRESTORE
        // ════════════════════════════════════════════════════════════════════

        private async Task SyncCountryToFirestoreAsync(string countryCode, string countryName)
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "country_code", countryCode },
                        { "country_name", countryName },
                        { "country_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Country sync delayed: " + exception.Message);
            }
        }

        /// <summary>
        /// Moves only the floating country dropdown when the mobile keyboard would
        /// cover it. The shift is calculated from the real keyboard/safe-area geometry,
        /// so it scales across different phones instead of using a hard-coded Y offset.
        /// </summary>
        private sealed class CountryKeyboardAvoider : MonoBehaviour
        {
            private RectTransform target;
            private InputField input;
            private Canvas canvas;
            private RectTransform parentRect;
            private Vector2 restingPosition;
            private float keyboardPaddingPixels = 28f;
            private float safeTopPaddingPixels = 24f;
            private float yVelocity;
            private bool configured;
            private float nextAndroidKeyboardSampleTime;
            private float cachedAndroidKeyboardHeight;
            private readonly Vector3[] targetCorners = new Vector3[4];

            public void Configure(RectTransform targetRect, InputField searchInput,
                float keyboardPadding, float safeTopPadding)
            {
                target = targetRect;
                input = searchInput;
                keyboardPaddingPixels = Mathf.Max(0f, keyboardPadding);
                safeTopPaddingPixels = Mathf.Max(0f, safeTopPadding);

                if (target != null)
                {
                    parentRect = target.parent as RectTransform;
                    restingPosition = target.anchoredPosition;
                    canvas = target.GetComponentInParent<Canvas>();
                }

                configured = target != null && input != null && parentRect != null;
            }

            private void OnEnable()
            {
                if (target != null)
                    restingPosition = target.anchoredPosition;
                yVelocity = 0f;
            }

            private void OnDisable()
            {
                if (target != null)
                    target.anchoredPosition = restingPosition;
                yVelocity = 0f;
            }

            private void LateUpdate()
            {
                if (!configured || target == null || input == null || parentRect == null)
                    return;

                float desiredY = restingPosition.y;
                float keyboardHeight = input.isFocused ? GetKeyboardHeightPixels() : 0f;

                if (keyboardHeight > Screen.height * 0.12f)
                {
                    Camera uiCamera = GetUiCamera();
                    target.GetWorldCorners(targetCorners);

                    // Bottom and top of the dropdown in the coordinate space of its parent.
                    float panelBottom = parentRect.InverseTransformPoint(targetCorners[0]).y;
                    float panelTop = parentRect.InverseTransformPoint(targetCorners[1]).y;

                    // Convert the keyboard's top edge from screen pixels to the same local space.
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect,
                        new Vector2(Screen.width * 0.5f, keyboardHeight),
                        uiCamera,
                        out var keyboardTopLocal))
                    {
                        float paddingLocal = PixelsToParentUnits(keyboardPaddingPixels);
                        float overlap = keyboardTopLocal.y + paddingLocal - panelBottom;

                        if (overlap > 0f)
                        {
                            // Do not push the dropdown through the device's safe-area top.
                            float safeTopPixels = Mathf.Max(0f, Screen.safeArea.yMax - safeTopPaddingPixels);
                            float allowedUp = overlap;
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                parentRect,
                                new Vector2(Screen.width * 0.5f, safeTopPixels),
                                uiCamera,
                                out var safeTopLocal))
                            {
                                allowedUp = Mathf.Min(overlap, Mathf.Max(0f, safeTopLocal.y - panelTop));
                            }

                            desiredY = target.anchoredPosition.y + allowedUp;
                        }
                        else
                        {
                            // If the keyboard height changed while open, settle toward the
                            // lowest position that still keeps the panel clear.
                            desiredY = Mathf.Max(restingPosition.y, target.anchoredPosition.y + overlap);
                        }
                    }
                }

                var position = target.anchoredPosition;
                position.y = Mathf.SmoothDamp(
                    position.y,
                    desiredY,
                    ref yVelocity,
                    0.10f,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);

                if (Mathf.Abs(position.y - desiredY) < 0.15f)
                    position.y = desiredY;

                target.anchoredPosition = position;
            }

            private Camera GetUiCamera()
            {
                if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return null;
                return canvas.worldCamera;
            }

            private float PixelsToParentUnits(float pixels)
            {
                Camera uiCamera = GetUiCamera();
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, Vector2.zero, uiCamera, out var a) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, new Vector2(0f, pixels), uiCamera, out var b))
                    return Mathf.Abs(b.y - a.y);

                float scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
                return pixels / scaleFactor;
            }

            private float GetKeyboardHeightPixels()
            {
                float height = TouchScreenKeyboard.area.height;

#if UNITY_ANDROID && !UNITY_EDITOR
                // Some Android/Unity combinations report TouchScreenKeyboard.area as zero.
                // Fall back to Android's visible display frame, sampled at 20 Hz.
                if (height <= 1f)
                {
                    if (Time.unscaledTime >= nextAndroidKeyboardSampleTime)
                    {
                        nextAndroidKeyboardSampleTime = Time.unscaledTime + 0.05f;
                        cachedAndroidKeyboardHeight = ReadAndroidKeyboardHeight();
                    }
                    height = cachedAndroidKeyboardHeight;
                }
#endif

                // Ignore status/navigation-bar-sized values when no real keyboard is open.
                return height > Screen.height * 0.12f ? height : 0f;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            private static float ReadAndroidKeyboardHeight()
            {
                try
                {
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    using var window = activity.Call<AndroidJavaObject>("getWindow");
                    using var decorView = window.Call<AndroidJavaObject>("getDecorView");
                    using var visibleFrame = new AndroidJavaObject("android.graphics.Rect");
                    decorView.Call("getWindowVisibleDisplayFrame", visibleFrame);
                    int visibleBottom = visibleFrame.Get<int>("bottom");
                    return Mathf.Max(0f, Screen.height - visibleBottom);
                }
                catch
                {
                    return 0f;
                }
            }
#endif
        }

        private static string AgeGroupFor(int age)
        {
            if (age < 13) return "under_13";
            if (age < 16) return "13_15";
            if (age < 18) return "16_17";
            return "18_plus";
        }
    }
}
