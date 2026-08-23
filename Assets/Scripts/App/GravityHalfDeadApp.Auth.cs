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
        private void BuildAuthScreen()
        {
            var screen = CreateScreen("Auth");

            var heroTexture = Resources.Load<Texture2D>("Art/Splash/nova-neon-tunnel");
            var heroObject = new GameObject("Neon verification hero");
            heroObject.transform.SetParent(screen.transform, false);
            var heroRect = heroObject.AddComponent<RectTransform>();
            Stretch(heroRect);
            var heroImage = heroObject.AddComponent<RawImage>();
            heroImage.texture = heroTexture;
            heroImage.raycastTarget = false;
            var heroAspect = heroObject.AddComponent<AspectRatioFitter>();
            heroAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            heroAspect.aspectRatio = heroTexture != null ? (float)heroTexture.width / heroTexture.height : 2f / 3f;

            CreateImage("Hero dark wash", screen.transform, new Color(0.01f, 0.015f, 0.06f, 0.24f), FullStretch());
            var topShade = CreateCard("Logo shade", screen.transform, new Vector2(0, 720),
                new Vector2(1080, 400), new Color(Ink.r, Ink.g, Ink.b, 0.48f), 0);
            MakeText(topShade.transform, "GRAVITY:", 86, FontStyle.Bold, Cream,
                new Vector2(0, 55), new Vector2(940, 110), TextAnchor.MiddleCenter, 4);
            MakeText(topShade.transform, "HALF DEAD", 48, FontStyle.Bold, Hex("FF55F5"),
                new Vector2(0, -45), new Vector2(900, 80), TextAnchor.MiddleCenter, 10);
            MakeText(topShade.transform, "CHOOSE HOW YOU ENTER THE BREACH", 20, FontStyle.Bold, Cyan,
                new Vector2(0, -125), new Vector2(800, 45), TextAnchor.MiddleCenter, 4);

            var card = CreateCard("Neon verification card", screen.transform, new Vector2(0, -560),
                new Vector2(900, 610), new Color(Card.r, Card.g, Card.b, 0.96f), 48);
            card.transform.SetAsLastSibling();
            CreateImage("Card cyan edge", card.transform, Cyan,
                Centered(new Vector2(-220, 299), new Vector2(430, 8)), RoundedSprite(4));
            CreateImage("Card magenta edge", card.transform, Hex("FF55F5"),
                Centered(new Vector2(220, 299), new Vector2(430, 8)), RoundedSprite(4));

            var guestButton = MakeButton(card.transform, "⚡   PLAY ANONYMOUSLY", new Vector2(0, 198),
                new Vector2(760, 124), Hex("087FE8"), Cream, 31, OnGuestPressed);
            guestButton.gameObject.AddComponent<ButtonGlow>();

            CreateImage("Divider left", card.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f),
                Centered(new Vector2(-235, 92), new Vector2(220, 3)), RoundedSprite(2));
            MakeText(card.transform, "OR CONTINUE WITH", 20, FontStyle.Bold, Muted,
                new Vector2(0, 92), new Vector2(300, 42), TextAnchor.MiddleCenter, 2);
            CreateImage("Divider right", card.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f),
                Centered(new Vector2(235, 92), new Vector2(220, 3)), RoundedSprite(2));

            var facebookButton = MakeButton(card.transform, "f   FACEBOOK", new Vector2(-195, -8),
                new Vector2(360, 116), Hex("1877F2"), Cream, 26, OnFacebookPressed);
            facebookButton.gameObject.AddComponent<ButtonGlow>();

#if UNITY_IOS
            var platformButton = MakeButton(card.transform, "A   APPLE", new Vector2(195, -8),
                new Vector2(360, 116), Hex("05070A"), Cream, 26, OnApplePressed);
            var platformNote = "FACEBOOK  +  APPLE  +  FIREBASE GUEST";
#else
            var platformButton = MakeButton(card.transform, "G   GOOGLE", new Vector2(195, -8),
                new Vector2(360, 116), Cream, Ink, 26, OnGooglePressed);
            var platformNote = "FACEBOOK  +  GOOGLE  +  FIREBASE GUEST";
#endif
            platformButton.gameObject.AddComponent<ButtonGlow>();

            authMessageText = MakeText(card.transform, "Your progress is protected whichever path you choose.",
                21, FontStyle.Bold, Cyan, new Vector2(0, -125), new Vector2(760, 70), TextAnchor.MiddleCenter);
            MakeText(card.transform, platformNote, 17, FontStyle.Bold, Green,
                new Vector2(0, -202), new Vector2(760, 40), TextAnchor.MiddleCenter, 2);
            MakeText(card.transform, "Guest accounts can be linked later without losing the player UID.",
                18, FontStyle.Normal, Muted, new Vector2(0, -252), new Vector2(760, 48), TextAnchor.MiddleCenter);

            MakeText(screen.transform, "By continuing, you agree to the Terms and acknowledge the Privacy Policy.",
                18, FontStyle.Normal, Muted, new Vector2(0, -905), new Vector2(880, 45), TextAnchor.MiddleCenter);
        }

        private async void OnGuestPressed()
        {
            if (isTransitioning)
                return;

            SetAuthMessage("Creating a protected guest pilot…", Cyan);
            try
            {
                await firebaseReadyTask;
                if (auth.CurrentUser == null)
                    await auth.SignInAnonymouslyAsync();

                await ShowScreenAsync("Splash");
                await BootstrapAndEnterGameAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetAuthMessage(FriendlyError(exception), Coral);
            }
        }

        private void OnGooglePressed()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (isTransitioning)
                return;

            SetAuthMessage(settingsProviderLinkInProgress ? "Connecting Google to this player…" : "Opening your Google accounts…", Cyan);
            try
            {
                using var bridge = new AndroidJavaClass("com.mkanfani.gravityhalfdead.GoogleIdentityBridge");
                bridge.CallStatic("signIn", GoogleWebClientId, gameObject.name,
                    nameof(OnGoogleIdToken), nameof(OnGoogleSignInError));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                var message = "Google sign-in could not start. Please try again.";
                SetAuthMessage(message, Coral);
                if (settingsProviderLinkInProgress)
                    EndSettingsProviderLink(false, message);
            }
#else
            var message = "Google sign-in is available in the Android build.";
            SetAuthMessage(message, Cyan);
            if (settingsProviderLinkInProgress)
                EndSettingsProviderLink(false, message);
#endif
        }

        private void OnApplePressed()
        {
            // Firebase can link Apple once an Apple identity token + raw nonce are supplied to
            // CompleteAppleSignInAsync. This project currently has no native Apple token bridge.
            var message = "Apple Firebase linking is ready for an Apple identity token, but the native Apple sign-in bridge still needs to provide it.";
            SetAuthMessage(message, Cyan);
            if (settingsProviderLinkInProgress)
                EndSettingsProviderLink(false, "Apple native sign-in bridge is not connected yet.");
        }

        private void OnFacebookPressed()
        {
            if (isTransitioning)
                return;

            SetAuthMessage(settingsProviderLinkInProgress ? "Connecting Facebook to this player…" : "Opening Facebook securely…", Cyan);
            var adapter = GetComponent<FacebookLoginAdapter>();
            if (adapter == null)
            {
                var message = "Facebook native login adapter is unavailable.";
                SetAuthMessage(message, Coral);
                if (settingsProviderLinkInProgress)
                    EndSettingsProviderLink(false, message);
                return;
            }

            adapter.SignIn(gameObject.name, nameof(OnFacebookAccessToken), nameof(OnFacebookSignInError));
        }

        public async Task<FirebaseUser> CompleteFacebookSignInAsync(string accessToken)
        {
            await firebaseReadyTask;
            var credential = FacebookAuthProvider.GetCredential(accessToken);
            return await SignInOrLinkAsync(credential, FacebookAuthProvider.ProviderId);
        }

        public async void OnFacebookAccessToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                OnFacebookSignInError("Facebook returned an empty access token.");
                return;
            }

            SetAuthMessage("Securing your Facebook pilot ID…", Green);
            try
            {
                var user = await CompleteFacebookSignInAsync(accessToken);
                if (settingsProviderLinkInProgress)
                {
                    await HandleSettingsProviderLinkSuccessAsync(user, "Facebook");
                    return;
                }

                await ShowScreenAsync("Splash");
                await BootstrapAndEnterGameAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                var message = FriendlyError(exception);
                SetAuthMessage(message, Coral);
                if (settingsProviderLinkInProgress)
                    EndSettingsProviderLink(false, FriendlyProviderLinkError(exception));
            }
        }

        public void OnFacebookSignInError(string message)
        {
            var cancelled = !string.IsNullOrWhiteSpace(message)
                && message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
            var display = cancelled
                ? "Facebook sign-in was cancelled. Your progress is unchanged."
                : "Facebook sign-in did not finish. Your progress is unchanged.";

            if (!cancelled)
                Debug.LogWarning("Facebook sign-in: " + message);
            SetAuthMessage(display, cancelled ? Muted : Coral);
            if (settingsProviderLinkInProgress)
                EndSettingsProviderLink(false, display);
        }

        public async Task<FirebaseUser> CompleteGoogleSignInAsync(string idToken, string accessToken)
        {
            await firebaseReadyTask;
            var credential = GoogleAuthProvider.GetCredential(idToken, accessToken);
            return await SignInOrLinkAsync(credential, GoogleAuthProvider.ProviderId);
        }

        public async void OnGoogleIdToken(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                OnGoogleSignInError("Google returned an empty identity token.");
                return;
            }

            SetAuthMessage("Securing your Google pilot ID…", Green);
            try
            {
                var user = await CompleteGoogleSignInAsync(idToken, null);
                if (settingsProviderLinkInProgress)
                {
                    await HandleSettingsProviderLinkSuccessAsync(user, "Google");
                    return;
                }

                await ShowScreenAsync("Splash");
                await BootstrapAndEnterGameAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                var message = FriendlyError(exception);
                SetAuthMessage(message, Coral);
                if (settingsProviderLinkInProgress)
                    EndSettingsProviderLink(false, FriendlyProviderLinkError(exception));
            }
        }

        public void OnGoogleSignInError(string message)
        {
            var cancelled = !string.IsNullOrWhiteSpace(message)
                && message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
            var display = cancelled
                ? "Google sign-in was cancelled. Your progress is unchanged."
                : "Google sign-in did not finish. Your progress is unchanged.";

            if (!cancelled)
                Debug.LogWarning("Google sign-in: " + message);
            SetAuthMessage(display, cancelled ? Muted : Coral);
            if (settingsProviderLinkInProgress)
                EndSettingsProviderLink(false, display);
        }

        public async Task<FirebaseUser> CompleteAppleSignInAsync(string idToken, string rawNonce)
        {
            await firebaseReadyTask;
            var credential = OAuthProvider.GetCredential("apple.com", idToken, rawNonce, null);
            var user = await SignInOrLinkAsync(credential, "apple.com");
            if (settingsProviderLinkInProgress)
                await HandleSettingsProviderLinkSuccessAsync(user, "Apple");
            return user;
        }

        private async Task<FirebaseUser> SignInOrLinkAsync(Credential credential, string providerId)
        {
            FirebaseUser user;
            var current = auth.CurrentUser;

            if (current == null)
            {
                user = await auth.SignInWithCredentialAsync(credential);
            }
            else if (current.IsAnonymous)
            {
                var linked = await current.LinkWithCredentialAsync(credential);
                user = linked.User;
            }
            else if (HasLinkedProvider(current, providerId))
            {
                user = current;
            }
            else
            {
                // This is the important multi-login behavior: attach the new provider to the
                // CURRENT Firebase UID instead of signing into a second Firebase account.
                var linked = await current.LinkWithCredentialAsync(credential);
                user = linked.User;
            }

            try
            {
                await user.ReloadAsync();
                if (auth.CurrentUser != null)
                    user = auth.CurrentUser;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Firebase profile refresh delayed: " + exception.Message);
            }

            await SyncAuthProfileToFirebaseAsync(user);
            return user;
        }

        private async Task HandleSettingsProviderLinkSuccessAsync(FirebaseUser user, string providerDisplayName)
        {
            await SyncAuthProfileToFirebaseAsync(user);
            RefreshTopPlayerAvatar(user);
            EndSettingsProviderLink(true, providerDisplayName + " is now linked to this player.");
        }

        private void SetAuthMessage(string message, Color color)
        {
            if (authMessageText == null)
                return;
            authMessageText.color = color;
            authMessageText.text = message;
        }

        private static bool HasLinkedProvider(FirebaseUser user, string providerId)
        {
            if (user == null || string.IsNullOrWhiteSpace(providerId))
                return false;
            foreach (var provider in user.ProviderData)
            {
                if (string.Equals(provider.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string BestProfilePhotoUrl(FirebaseUser user)
        {
            if (user == null)
                return string.Empty;
            if (user.PhotoUrl != null && !string.IsNullOrWhiteSpace(user.PhotoUrl.ToString()))
                return user.PhotoUrl.ToString();

            string fallback = string.Empty;
            foreach (var provider in user.ProviderData)
            {
                if (provider.PhotoUrl == null || string.IsNullOrWhiteSpace(provider.PhotoUrl.ToString()))
                    continue;
                var url = provider.PhotoUrl.ToString();
                if (provider.ProviderId == GoogleAuthProvider.ProviderId)
                    return url;
                if (provider.ProviderId == FacebookAuthProvider.ProviderId && string.IsNullOrWhiteSpace(fallback))
                    fallback = url;
                else if (string.IsNullOrWhiteSpace(fallback))
                    fallback = url;
            }
            return fallback;
        }

        private static string LinkedProviderStatus(FirebaseUser user)
        {
            if (user == null || user.IsAnonymous)
                return "Guest account";

            var names = LinkedProviderDisplayNames(user);
            if (names.Count == 0)
                return "Firebase account";
            if (names.Count == 1)
                return "Logged in with " + names[0];
            return "Linked with " + string.Join(" + ", names);
        }

        private static List<string> LinkedProviderDisplayNames(FirebaseUser user)
        {
            var names = new List<string>();
            if (user == null)
                return names;

            if (HasLinkedProvider(user, GoogleAuthProvider.ProviderId)) names.Add("Google");
            if (HasLinkedProvider(user, FacebookAuthProvider.ProviderId)) names.Add("Facebook");
            if (HasLinkedProvider(user, "apple.com")) names.Add("Apple");
            return names;
        }

        private static List<object> LinkedProviderFirestoreValues(FirebaseUser user)
        {
            var values = new List<object>();
            foreach (var name in LinkedProviderDisplayNames(user))
                values.Add(name.ToLowerInvariant());
            return values;
        }

        private static string ProviderName(FirebaseUser user)
        {
            if (user == null || user.IsAnonymous)
                return "guest";
            if (HasLinkedProvider(user, GoogleAuthProvider.ProviderId)) return "google";
            if (HasLinkedProvider(user, FacebookAuthProvider.ProviderId)) return "facebook";
            if (HasLinkedProvider(user, "apple.com")) return "apple";
            return "firebase";
        }

        private static string FriendlyProviderLinkError(Exception exception)
        {
            var message = exception.GetBaseException().Message ?? string.Empty;
            if (message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0
                   && message.IndexOf("use", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "That login is already attached to another Gravity account. No account was switched.";
            }
            return FriendlyError(exception);
        }

        private static string FriendlyError(Exception exception)
        {
            var message = exception.GetBaseException().Message;
            if (message.Length > 130)
                message = message[..130] + "…";
            return "Couldn’t connect yet. " + message;
        }
    }
}
