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
    public sealed partial class GravityHalfDeadApp : MonoBehaviour
    {
        private const string AgeGroupKey = "ghd.age_group";
        private const string AgeConfirmedKey = "ghd.age_confirmed";
        private const string MusicEnabledKey = "ghd.settings.music";
        private const string SfxEnabledKey = "ghd.settings.sfx";
        private const string VibrationEnabledKey = "ghd.settings.vibration";
        private const string NotificationsEnabledKey = "ghd.settings.notifications";
        private const string GoogleWebClientId = "33771789204-fh28a5cl9nfuhmppg9jin0b1komukibn.apps.googleusercontent.com";

        private static readonly Color Ink = Hex("091321");
        private static readonly Color Card = Hex("173A64");
        private static readonly Color CardSoft = Hex("265888");
        private static readonly Color Cyan = Hex("19D9FF");
        private static readonly Color Coral = Hex("FF795F");
        private static readonly Color Cream = Hex("FFF7E8");
        private static readonly Color Muted = Hex("B9CEE6");
        private static readonly Color Green = Hex("62F5A8");
        private static readonly Color NeonPurple = Hex("6E35FF");
        private static readonly Color NeonPink = Hex("FF3ED8");
        private static readonly Color NeonBlue = Hex("19D9FF");
        private static readonly Color NeonLime = Hex("62F5A8");
        private static readonly string[] CharacterIds =
        {
        "nova", "orbit", "jax", "raze", "echo", "regalia", "kairo",
        "luna", "volt", "mako", "glitch", "ember", "frost"
    };
        private static readonly string[] CharacterNames =
        {
        "NOVA", "ORBIT", "JAX", "RAZE", "ECHO", "REGALIA", "KAIRO",
        "LUNA", "VOLT", "MAKO", "GLITCH", "EMBER", "FROST"
    };
        private static readonly string[] PowerupIds =
            { "shield", "speed_boost", "invulnerability", "magnet", "wall_walk", "timezone" };
        private static readonly string[] PowerupNames =
            { "SHIELD", "SPEED BOOST", "INVULNERABILITY", "MAGNET", "WALL WALK", "TIMEZONE" };
        private static readonly string[] PowerupDescriptions =
        {
        "Blocks one collision before shattering.",
        "Increases running speed for a limited time.",
        "Pass safely through hazards for a short time.",
        "Pulls nearby coins into your lane.",
        "Magnetic boots let you run on tunnel walls.",
        "Extends every power-up effect by 10% per level."
    };
        private static readonly long[] PowerupUpgradeCosts = { 500L, 1000L, 3000L, 10000L, 30000L, 60000L };
        private static readonly long[] TimezoneUpgradeCosts = { 5000L, 15000L, 45000L, 60000L, 60000L, 60000L };
        private static readonly int[] PowerupDurations = { 5, 10, 14, 18, 22, 26, 30 };

        private readonly Dictionary<string, CanvasGroup> screens = new();
        private Font font;
        private Font handwritingFont;
        private RectTransform safeRoot;
        private FirebaseAuth auth;
        private FirebaseDatabase realtimeDatabase;
        private FirebaseFirestore firestore;
        private Task firebaseReadyTask;
        // ageSlider, ageValueText, ageHintText moved to GravityHalfDeadApp.Age.cs
        // (ageStepSlider, ageStepValueText, ageStepHintText) for the two-step onboarding flow.
        private Text authMessageText;
        private Text splashStatusText;
        private Text splashPercentText;
        private Image splashProgressFill;
        private Text gameModeText;
        private Text gameModeHintText;
        private Text gameTapText;
        private RectTransform gameLeverHandle;
        private Image gameModeGlow;
        private Image gameStoryBorder;
        private Image gameEndlessBorder;
        private Text gameCoinAmountText;
        private RawImage topPlayerAvatarImage;
        private RawImage topPlayerAvatarFrameImage;
        private GameObject topPlayerAvatarFallback;
        private CanvasGroup gameTabPanel;
        private GameObject gameBottomNavigation;
        private Button gameSectionCloseButton;
        private Text gameTabTitle;
        private Text gameTabSubtitle;
        private GameObject gameGenericTabContent;
        private GameObject gameMeContent;
        private GameObject meStatisticsContent;
        private GameObject meCharactersContent;
        private GameObject mePowerupsContent;
        private GameObject meDiscsContent;
        private Button meStatisticsTabButton;
        private Button meAvatarsTabButton;
        private Button mePowerupsTabButton;
        private readonly Text[] meStatisticValues = new Text[5];
        private readonly Image[] meAvatarBorders = new Image[13];
        private readonly RawImage[] meAvatarPortraits = new RawImage[13];
        private readonly Text[] meAvatarStatusTexts = new Text[13];
        private readonly Image[] meFrameBorders = new Image[3];
        private readonly Text[] meFrameStatuses = new Text[3];
        private readonly Image[,] mePowerupSegments = new Image[6, 6];
        private readonly Text[] mePowerupLevelTexts = new Text[6];
        private readonly Text[] mePowerupDurationTexts = new Text[6];
        private readonly Text[] mePowerupCostTexts = new Text[6];
        private readonly Text[] mePowerupButtonLabelTexts = new Text[6];
        private readonly Button[] mePowerupButtons = new Button[6];
        private RawImage meCharacterShowcase;
        private Material characterCutoutMaterial;
        private Material novaCutoutMaterial;
        private Text meCharacterName;
        private Text meCharacterUnlockText;
        private Text meCharacterWalletText;
        private Text mePowerupWalletText;
        private Button meCharacterActionButton;
        private GameObject meCharacterSelectedBadge;
        private string previewCharacterId = "nova";
        private readonly Text[] gameTabCardTitles = new Text[3];
        private readonly Text[] gameTabCardDetails = new Text[3];
        private readonly Text[] gameTabCardTags = new Text[3];
        private CanvasGroup gameSettingsPanel;
        private Text gameSettingsAccountText;
        private Button gameMusicButton;
        private Button gameSfxButton;
        private Button gameVibrationButton;
        private Button gameNotificationsButton;
        private GameObject gameLinkedAccountActions;
        private GameObject gameGuestAccountActions;
        private InputField supportSubjectInput;
        private InputField supportBodyInput;
        private Text supportAttachmentText;
        private Text supportStatusText;
        private Button supportAttachButton;
        private Button supportSendButton;
        private readonly List<string> supportImagePaths = new();
        private const long SupportUploadLimitBytes = 10L * 1024L * 1024L;
        private bool musicEnabled = true;
        private bool sfxEnabled = true;
        private bool vibrationEnabled = true;
        private bool notificationsEnabled;
        private bool endlessMode;
        private bool isTransitioning;
        private long pendingPlaySeconds;
        private bool playtimeSyncInFlight;
        private int selectedAge = 18;

        private BootstrapState bootstrapState = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<GravityHalfDeadApp>() != null)
                return;

            var host = new GameObject("Gravity Half Dead · App Flow");
            DontDestroyOnLoad(host);
            host.AddComponent<GravityHalfDeadApp>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            gameObject.AddComponent<FacebookLoginAdapter>();
            gameObject.AddComponent<GoogleMobileAdsTestRewardedProvider>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            handwritingFont = Resources.Load<Font>("Fonts/Kalam-Bold");
            if (handwritingFont == null)
                handwritingFont = font;
            BuildEventSystem();
            BuildInterface();
            firebaseReadyTask = InitializeFirebaseAsync();
            StartCoroutine(TrackPlaytime());
        }

        private async void Start()
        {
            if (PlayerPrefs.GetInt(AgeConfirmedKey, 0) == 0)
            {
                ShowImmediately("Age");
                return;
            }

            ShowImmediately("Splash");
            SetSplashProgress(0.08f, "Waking the gravity core");

            try
            {
                await firebaseReadyTask;
                if (auth.CurrentUser == null)
                    await ShowScreenAsync("Auth");
                else
                    await BootstrapAndEnterGameAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                authMessageText.text = FriendlyError(exception);
                await ShowScreenAsync("Auth");
            }
        }

        private void BuildEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var eventSystemObject = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            var input = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
        }

        private void BuildInterface()
        {
            var canvasObject = new GameObject("Gravity UI");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var background = CreateImage("Deep Space", canvasObject.transform, Ink, FullStretch());
            background.raycastTarget = false;
            background.gameObject.AddComponent<LivingBackdrop>();

            CreateOrb(background.transform, new Vector2(-360, 640), 520, Cyan, 0.14f, 0.16f);
            CreateOrb(background.transform, new Vector2(390, -520), 620, Coral, 0.12f, -0.11f);
            CreateOrb(background.transform, new Vector2(420, 650), 290, Cream, 0.045f, 0.2f);
            BuildStarfield(background.transform);

            var safeObject = new GameObject("Safe Area");
            safeObject.transform.SetParent(canvasObject.transform, false);
            safeRoot = safeObject.AddComponent<RectTransform>();
            Stretch(safeRoot);
            safeObject.AddComponent<SafeAreaFitter>();

            BuildAgeScreen();
            BuildAuthScreen();
            BuildSplashScreen();
            BuildGameScreen();

            foreach (var pair in screens)
            {
                pair.Value.alpha = 0f;
                pair.Value.blocksRaycasts = false;
                pair.Value.interactable = false;
                pair.Value.gameObject.SetActive(false);
            }
        }
    }
}
