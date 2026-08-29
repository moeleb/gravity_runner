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
        private Text countrySelectionStatusText;
        private Button countryConfirmButton;
        private Text countryConfirmButtonLabel;
        private Text onboardingStatusText;
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
        private Image ageStepBandBadge;
        private Text ageStepBandText;

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

            // Screen-specific atmosphere keeps the dark space identity while giving the
            // onboarding screen its own energetic focal area. These layers never intercept input.
            var cyanAtmosphere = CreateCard("Age cyan atmosphere", screen.transform,
                new Vector2(-390f, 570f), new Vector2(620f, 620f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.075f), 64);
            cyanAtmosphere.GetComponent<Image>().raycastTarget = false;
            var coralAtmosphere = CreateCard("Age coral atmosphere", screen.transform,
                new Vector2(430f, 265f), new Vector2(520f, 520f),
                new Color(Coral.r, Coral.g, Coral.b, 0.065f), 64);
            coralAtmosphere.GetComponent<Image>().raycastTarget = false;

            // ── Header ──────────────────────────────────────────────────────
            var onboardingPill = CreateCard("Onboarding status", screen.transform,
                new Vector2(0f, 800f), new Vector2(360f, 52f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.16f), 26);
            CreateImage("Onboarding live dot", onboardingPill.transform, Green,
                Centered(new Vector2(-145f, 0f), new Vector2(12f, 12f)), RoundedSprite(6)).raycastTarget = false;
            onboardingStatusText = MakeText(onboardingPill.transform, "PLAYER SETUP  •  1 OF 2", 19, FontStyle.Bold, Cyan,
                new Vector2(10f, 0f), new Vector2(310f, 34f), TextAnchor.MiddleCenter, 2);

            var heroTitle = MakeText(screen.transform, "BEFORE WE BEND GRAVITY", 53, FontStyle.Bold, Cream,
                new Vector2(0, 708), new Vector2(970, 90), TextAnchor.MiddleCenter, 1);
            var heroShadow = heroTitle.gameObject.AddComponent<Shadow>();
            heroShadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.38f);
            heroShadow.effectDistance = new Vector2(0f, -4f);
            MakeText(screen.transform, "Choose your age first. We use only an age range to shape a safer experience.", 25,
                FontStyle.Normal, Hex("D6E8F7"), new Vector2(0, 630), new Vector2(890, 76), TextAnchor.MiddleCenter);

            // ── Decorative characters ───────────────────────────────────────
            CreateAgeCharacterGlow(screen.transform, new Vector2(-315f, 305f), Cyan, 280f);
            CreateAgeCharacterGlow(screen.transform, new Vector2(0f, 360f), Coral, 320f);
            CreateAgeCharacterGlow(screen.transform, new Vector2(320f, 305f), NeonPurple, 270f);
            AddCharacter(screen.transform, "Art/Characters/atlas", new Vector2(-310, 285), new Vector2(400, 600), -4f, 0.7f);
            AddCharacter(screen.transform, "Art/Characters/nova", new Vector2(0, 350), new Vector2(430, 650), 0f, 0.95f);
            AddCharacter(screen.transform, "Art/Characters/pip", new Vector2(320, 290), new Vector2(365, 550), 4f, 1.25f);

            // ── Step indicator ──────────────────────────────────────────────
            BuildStepIndicator(screen.transform);

            // ── Age card ────────────────────────────────────────────────────
            var cardShadow = CreateCard("Age Card shadow", screen.transform,
                new Vector2(0f, -204f), new Vector2(914f, 832f), new Color(0f, 0f, 0f, 0.42f), 46);
            cardShadow.GetComponent<Image>().raycastTarget = false;
            var cardGlow = CreateCard("Age Card cyan lift", screen.transform,
                new Vector2(0f, -180f), new Vector2(914f, 830f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f), 46);
            cardGlow.GetComponent<Image>().raycastTarget = false;
            var card = CreateCard("Age Card", screen.transform, new Vector2(0, -184),
                new Vector2(890, 810), Hex("12375F"), 42);
            card.transform.SetAsLastSibling();
            CreateImage("Age Card top energy rail", card.transform, Cyan,
                Centered(new Vector2(-185f, 399f), new Vector2(420f, 8f)), RoundedSprite(4)).raycastTarget = false;
            CreateImage("Age Card warm energy rail", card.transform, Coral,
                Centered(new Vector2(285f, 399f), new Vector2(180f, 8f)), RoundedSprite(4)).raycastTarget = false;
            ageStepGroup = card.AddComponent<CanvasGroup>();
            ageStepGroup.alpha = 1f;
            ageStepGroup.blocksRaycasts = true;
            ageStepGroup.interactable = true;
            BuildAgeStep(card.transform);

            // ── Country card (hidden) ───────────────────────────────────────
            var countryCard = CreateCard("Country Card", screen.transform, new Vector2(0, -184), new Vector2(890, 810), Hex("12375F"), 42);
            countryCard.transform.SetAsLastSibling();
            CreateImage("Country Card top energy rail", countryCard.transform, Green,
                Centered(new Vector2(-185f, 399f), new Vector2(420f, 8f)), RoundedSprite(4)).raycastTarget = false;
            CreateImage("Country Card cyan energy rail", countryCard.transform, Cyan,
                Centered(new Vector2(285f, 399f), new Vector2(180f, 8f)), RoundedSprite(4)).raycastTarget = false;
            countryStepGroup = countryCard.AddComponent<CanvasGroup>();
            countryStepGroup.alpha = 0f;
            countryStepGroup.blocksRaycasts = false;
            countryStepGroup.interactable = false;
            countryCard.SetActive(false);
            BuildCountryStep(countryCard.transform);

            // ── Footer ──────────────────────────────────────────────────────
            MakeText(screen.transform, "PRIVACY  •  Your choices remain available in Settings.", 20,
                FontStyle.Normal, Hex("AFC9E2"), new Vector2(0, -842), new Vector2(850, 45), TextAnchor.MiddleCenter);
        }

        private void CreateAgeCharacterGlow(Transform parent, Vector2 position, Color accent, float size)
        {
            var glowColor = accent;
            glowColor.a = 0.12f;
            var glow = CreateImage("Character spotlight", parent, glowColor,
                Centered(position, new Vector2(size, size)), RoundedSprite(64));
            glow.raycastTarget = false;
            glow.gameObject.AddComponent<SoftPulse>();
        }

        // ────────────────────────────────────────────────────────────────────
        //  STEP INDICATOR
        // ────────────────────────────────────────────────────────────────────

        private void BuildStepIndicator(Transform parent)
        {
            var stepPill = CreateCard("Age flow progress", parent, new Vector2(0f, 500f),
                new Vector2(330f, 74f), new Color(0.025f, 0.09f, 0.16f, 0.90f), 37);
            stepPill.GetComponent<Image>().raycastTarget = false;

            var dot1Obj = CreateImage("Step Dot 1", stepPill.transform, Cyan,
                Centered(new Vector2(-115, 0), new Vector2(34, 34)), RoundedSprite(17));
            stepDot1 = dot1Obj;
            stepIndicator1 = MakeText(stepPill.transform, "1", 17, FontStyle.Bold, Ink,
                new Vector2(-115, 0), new Vector2(34, 34), TextAnchor.MiddleCenter);

            stepConnector = CreateImage("Step Connector", stepPill.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.38f),
                Centered(new Vector2(0, 0), new Vector2(84, 5)), RoundedSprite(3));

            var dot2Obj = CreateImage("Step Dot 2", stepPill.transform, Hex("315375"),
                Centered(new Vector2(115, 0), new Vector2(34, 34)), RoundedSprite(17));
            stepDot2 = dot2Obj;
            stepIndicator2 = MakeText(stepPill.transform, "2", 17, FontStyle.Bold, Muted,
                new Vector2(115, 0), new Vector2(34, 34), TextAnchor.MiddleCenter);

            MakeText(stepPill.transform, "AGE", 18, FontStyle.Bold, Cyan,
                new Vector2(-62, 0), new Vector2(76, 30), TextAnchor.MiddleCenter, 1);
            MakeText(stepPill.transform, "REGION", 18, FontStyle.Bold, Muted,
                new Vector2(62, 0), new Vector2(94, 30), TextAnchor.MiddleCenter, 1);
        }

        private void UpdateStepIndicator(int step)
        {
            if (step == 1)
            {
                if (onboardingStatusText != null)
                    onboardingStatusText.text = "PLAYER SETUP  •  1 OF 2";
                stepDot1.color = Cyan;
                stepDot2.color = CardSoft;
                stepIndicator1.color = Ink;
                stepIndicator2.color = Muted;
                stepConnector.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
            }
            else
            {
                if (onboardingStatusText != null)
                    onboardingStatusText.text = "PLAYER SETUP  •  2 OF 2";
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
            MakeText(card.transform, "CHOOSE YOUR AGE", 33, FontStyle.Bold, Cream,
                new Vector2(0, 337), new Vector2(760, 55), TextAnchor.MiddleCenter, 2);
            MakeText(card.transform, "Slide or tap the track", 20, FontStyle.Normal, Hex("AFC9E2"),
                new Vector2(0, 298), new Vector2(540, 34), TextAnchor.MiddleCenter);

            // ── Big age number (the centrepiece) ────────────────────────────
            var valueGlow = CreateCard("Selected age glow", card.transform, new Vector2(0f, 205f),
                new Vector2(282f, 164f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f), 50);
            valueGlow.GetComponent<Image>().raycastTarget = false;
            var valueSurface = CreateCard("Selected age surface", card.transform, new Vector2(0f, 209f),
                new Vector2(258f, 148f), Hex("0A2747"), 46);
            valueSurface.GetComponent<Image>().raycastTarget = false;
            MakeText(valueSurface.transform, "AGE", 17, FontStyle.Bold, Cyan,
                new Vector2(0f, 50f), new Vector2(150f, 28f), TextAnchor.MiddleCenter, 3);
            ageStepValueText = MakeText(valueSurface.transform, selectedAge.ToString(), 96, FontStyle.Bold, Cream,
                new Vector2(0, -7), new Vector2(220, 104), TextAnchor.MiddleCenter);
            ageStepValueText.gameObject.AddComponent<SoftPulse>();

            // ── Age-band state and description ──────────────────────────────
            var bandObject = CreateCard("Age experience badge", card.transform, new Vector2(0f, 105f),
                new Vector2(420f, 50f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f), 25);
            ageStepBandBadge = bandObject.GetComponent<Image>();
            ageStepBandText = MakeText(bandObject.transform, "ADULT EXPERIENCE", 19, FontStyle.Bold, Cyan,
                Vector2.zero, new Vector2(380f, 32f), TextAnchor.MiddleCenter, 2);
            ageStepHintText = MakeText(card.transform,
                "Full personalization controls",
                22, FontStyle.Normal, Hex("D6E8F7"), new Vector2(0, 60), new Vector2(700, 44), TextAnchor.MiddleCenter);

            // ── Slider (cyan, always) ───────────────────────────────────────
            ageStepSlider = BuildCyanSlider(card.transform, new Vector2(0, -15), new Vector2(710, 104));
            ageStepSlider.minValue = 3;
            ageStepSlider.maxValue = 65;
            ageStepSlider.wholeNumbers = true;
            ageStepSlider.value = selectedAge;
            ageStepSlider.onValueChanged.AddListener(OnAgeChanged);

            MakeText(card.transform, "3", 17, FontStyle.Bold, Hex("8FAFCB"),
                new Vector2(-355f, -70f), new Vector2(48f, 28f), TextAnchor.MiddleCenter);
            MakeText(card.transform, "18", 17, FontStyle.Bold, Hex("8FAFCB"),
                new Vector2(-183f, -70f), new Vector2(48f, 28f), TextAnchor.MiddleCenter);
            MakeText(card.transform, "35", 17, FontStyle.Bold, Hex("8FAFCB"),
                new Vector2(11f, -70f), new Vector2(48f, 28f), TextAnchor.MiddleCenter);
            MakeText(card.transform, "50", 17, FontStyle.Bold, Hex("8FAFCB"),
                new Vector2(183f, -70f), new Vector2(48f, 28f), TextAnchor.MiddleCenter);
            MakeText(card.transform, "65+", 17, FontStyle.Bold, Hex("8FAFCB"),
                new Vector2(355f, -70f), new Vector2(56f, 28f), TextAnchor.MiddleCenter);

            // ── Privacy pill ────────────────────────────────────────────────
            var privacyPill = CreateCard("Privacy Note", card.transform, new Vector2(0, -132),
                new Vector2(710, 72), Hex("1B4A70"), 24);
            CreateImage("Privacy shield", privacyPill.transform, Green,
                Centered(new Vector2(-310f, 0f), new Vector2(15f, 32f)), RoundedSprite(8)).raycastTarget = false;
            MakeText(privacyPill.transform, "We save an age range — never your birthday", 21,
                FontStyle.Normal, Cream, new Vector2(15f, 0f), new Vector2(625, 48), TextAnchor.MiddleCenter);

            // ── Continue ────────────────────────────────────────────────────
            var continueButton = MakeButton(card.transform, "CONTINUE  ›", new Vector2(0, -258),
                new Vector2(710, 108), Hex("FFB84D"), Ink, 30, OnAgeStepContinue);
            var buttonImage = continueButton.GetComponent<Image>();
            var buttonShadow = buttonImage.gameObject.AddComponent<Shadow>();
            buttonShadow.effectColor = new Color(1f, 0.45f, 0.10f, 0.42f);
            buttonShadow.effectDistance = new Vector2(0f, -7f);
            buttonShadow.useGraphicAlpha = false;
            CreateImage("Continue highlight", continueButton.transform, new Color(1f, 1f, 1f, 0.24f),
                Centered(new Vector2(0f, 40f), new Vector2(630f, 7f)), RoundedSprite(4)).raycastTarget = false;
            continueButton.gameObject.AddComponent<ButtonGlow>();

            OnAgeChanged(selectedAge);
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
            string bandLabel;
            Color bandColor;
            ageStepHintText.text = selectedAge switch
            {
                < 13 => "Limited data and age-appropriate advertising",
                < 16 => "Privacy-first personalization",
                < 18 => "Standard safety controls",
                _ => "Full personalization controls"
            };

            if (selectedAge < 13)
            {
                bandLabel = "CHILD EXPERIENCE";
                bandColor = Hex("FFD166");
            }
            else if (selectedAge < 16)
            {
                bandLabel = "TEEN EXPERIENCE";
                bandColor = Hex("7BE0FF");
            }
            else if (selectedAge < 18)
            {
                bandLabel = "OLDER TEEN EXPERIENCE";
                bandColor = Hex("B9A1FF");
            }
            else
            {
                bandLabel = "ADULT EXPERIENCE";
                bandColor = Green;
            }

            if (ageStepBandText != null)
            {
                ageStepBandText.text = bandLabel;
                ageStepBandText.color = bandColor;
            }
            if (ageStepBandBadge != null)
                ageStepBandBadge.color = new Color(bandColor.r, bandColor.g, bandColor.b, 0.16f);
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
            MakeText(card.transform, "CHOOSE YOUR REGION", 33, FontStyle.Bold, Cream,
                new Vector2(0, 337), new Vector2(760, 55), TextAnchor.MiddleCenter, 2);
            MakeText(card.transform, "Used for regional availability and relevant game services.",
                21, FontStyle.Normal, Hex("D6E8F7"), new Vector2(0, 292), new Vector2(740, 44), TextAnchor.MiddleCenter);

            // Restore a previously selected country when this screen is rebuilt.
            if (string.IsNullOrEmpty(selectedCountryCode))
            {
                selectedCountryCode = PlayerPrefs.GetString("ghd.country_code", "");
                selectedCountryName = PlayerPrefs.GetString("ghd.country_name", "");
            }

            // ── Closed dropdown field ───────────────────────────────────────
            MakeText(card.transform, "COUNTRY OR REGION", 17, FontStyle.Bold, Cyan,
                new Vector2(-300f, 244f), new Vector2(260f, 28f), TextAnchor.MiddleLeft, 2);
            var selector = CreateCard("Country Dropdown", card.transform,
                new Vector2(0, 186), new Vector2(760, 98), Hex("0B2947"), 28);
            var selectorShadow = selector.AddComponent<Shadow>();
            selectorShadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
            selectorShadow.effectDistance = new Vector2(0f, -5f);
            selectorShadow.useGraphicAlpha = false;
            CreateImage("Country selector active rail", selector.transform, Cyan,
                Centered(new Vector2(-375f, 0f), new Vector2(6f, 62f)), RoundedSprite(3)).raycastTarget = false;

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
                new Vector2(-316, 0), new Vector2(70, 52), Hex("061A2E"), 12);
            flagHolder.GetComponent<Image>().raycastTarget = false;

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

            countrySelectedLabel = MakeText(selector.transform, "Tap to choose", 26, FontStyle.Bold, Hex("AFC9E2"),
                new Vector2(22, 0), new Vector2(560, 58), TextAnchor.MiddleLeft);
            countrySelectedLabel.raycastTarget = false;

            countryDropdownArrow = MakeText(selector.transform, "+", 30, FontStyle.Bold, Cyan,
                new Vector2(326, 0), new Vector2(62, 58), TextAnchor.MiddleCenter);
            countryDropdownArrow.raycastTarget = false;

            // ── Floating dropdown panel (hidden until selector is tapped) ───
            countryDropdownPanel = CreateCard("Country Dropdown Panel", card.transform,
                new Vector2(0, -60), new Vector2(760, 420), Hex("102F50"), 28);
            var panelShadow = countryDropdownPanel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
            panelShadow.effectDistance = new Vector2(0f, -8f);
            panelShadow.useGraphicAlpha = false;
            CreateImage("Country dropdown top rail", countryDropdownPanel.transform, Cyan,
                Centered(new Vector2(0f, 205f), new Vector2(660f, 6f)), RoundedSprite(3)).raycastTarget = false;
            countryDropdownPanel.transform.SetAsLastSibling();

            countrySearchInput = BuildCountrySearch(countryDropdownPanel.transform,
                new Vector2(0, 158), new Vector2(700, 72));
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
                new Vector2(0, -10), new Vector2(700, 266));

            countryResultCountText = MakeText(countryDropdownPanel.transform, "", 17, FontStyle.Normal,
                Hex("9CB7D0"), new Vector2(0, -180), new Vector2(680, 28), TextAnchor.MiddleCenter);
            countryResultCountText.raycastTarget = false;

            countryDropdownPanel.SetActive(false);
            countryDropdownOpen = false;
            RefreshCountrySelectorVisual();

            countrySelectionStatusText = MakeText(card.transform, "SELECT A REGION TO CONTINUE", 17,
                FontStyle.Bold, Hex("89A7C3"), new Vector2(0f, 115f), new Vector2(700f, 30f),
                TextAnchor.MiddleCenter, 2);

            // ── Buttons ─────────────────────────────────────────────────────
            var backButton = MakeButton(card.transform, "‹  BACK", new Vector2(-210, -342),
                new Vector2(300, 96), Hex("1B4A70"), Cream, 24, OnCountryStepBack);

            countryConfirmButton = MakeButton(card.transform, "CONFIRM  ›", new Vector2(140, -342),
                new Vector2(440, 96), Hex("435468"), Hex("A4B0BE"), 28, OnCountryStepConfirm);
            countryConfirmButtonLabel = countryConfirmButton.GetComponentInChildren<Text>();
            countryConfirmButton.gameObject.AddComponent<ButtonGlow>();
            RefreshCountryConfirmState();
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
                countryDropdownArrow.text = open ? "−" : "+";

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
                    : "Tap to choose";
                countrySelectedLabel.color = hasSelection ? Cream : Hex("AFC9E2");
            }

            ApplyCountryFlag(countrySelectedFlagImage, countrySelectedFlagFallback,
                hasSelection ? selectedCountryCode : null);

            RefreshCountryConfirmState();
        }

        private void RefreshCountryConfirmState()
        {
            bool ready = !string.IsNullOrEmpty(selectedCountryCode);
            if (countryConfirmButton != null)
            {
                countryConfirmButton.interactable = ready;
                var image = countryConfirmButton.GetComponent<Image>();
                if (image != null)
                    image.color = ready ? Hex("FFB84D") : Hex("435468");
            }

            if (countryConfirmButtonLabel != null)
                countryConfirmButtonLabel.color = ready ? Ink : Hex("A4B0BE");

            if (countrySelectionStatusText != null)
            {
                countrySelectionStatusText.text = ready
                    ? "READY  •  " + selectedCountryCode
                    : "SELECT A REGION TO CONTINUE";
                countrySelectionStatusText.color = ready ? Green : Hex("89A7C3");
            }
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
            bg.color = Hex("183E60");
            bg.raycastTarget = true;

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
                new Vector2(-size.x / 2f + 40, 0), new Vector2(48, 48), Hex("0A2946"), 16);
            searchBadge.GetComponent<Image>().raycastTarget = false;
            var searchGlyph = MakeText(searchBadge.transform, "⌕", 30, FontStyle.Bold, Cyan,
                Vector2.zero, new Vector2(42, 42), TextAnchor.MiddleCenter);
            searchGlyph.raycastTarget = false;

            var text = MakeText(fieldObject.transform, "", 24, FontStyle.Normal, Cream,
                new Vector2(48, 0), new Vector2(size.x - 126, 52), TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.raycastTarget = false;
            input.textComponent = text;

            var placeholder = MakeText(fieldObject.transform, "Search by country or 2-letter code", 22, FontStyle.Italic,
                Hex("9CB7D0"), new Vector2(48, 0), new Vector2(size.x - 126, 52), TextAnchor.MiddleLeft);
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
            layout.spacing = 8;
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
                    isSelected ? Hex("18566A") : Hex("173A5C"), 18);

                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = rowHeight;
                le.minHeight = rowHeight;

                if (isSelected)
                {
                    var accent = CreateCard("Selected Accent", row.transform,
                        new Vector2(-342, 0), new Vector2(6, rowHeight - 14), Green, 3);
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
                    isSelected ? Color.white : Cream,
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
                    Vector2.zero, new Vector2(0, 94), Hex("173A5C"), 18);
                var emptyLayout = emptyRow.AddComponent<LayoutElement>();
                emptyLayout.preferredHeight = 84;
                emptyLayout.minHeight = 84;
                var emptyText = MakeText(emptyRow.transform, "NO MATCHES YET\nTry a country name or 2-letter code",
                    19, FontStyle.Normal, Hex("C7D9EA"), Vector2.zero, new Vector2(620, 72), TextAnchor.MiddleCenter);
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
            RefreshCountryConfirmState();

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
                if (countrySelectionStatusText != null)
                {
                    countrySelectionStatusText.text = "CHOOSE A COUNTRY OR REGION FIRST";
                    countrySelectionStatusText.color = Coral;
                }
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
