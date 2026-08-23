using System;
using System.Collections.Generic;
using Facebook.Unity;
using UnityEngine;

namespace GravityHalfDead
{
    /// <summary>
    /// Owns the native Meta login lifecycle and returns only the short-lived Facebook access token
    /// to the app flow. Firebase exchanges that token for the real player session.
    /// </summary>
    public sealed class FacebookLoginAdapter : MonoBehaviour
    {
        private const string FacebookAppId = "1029206673440587";
        private const string FacebookClientToken = "dcf690e7e2f9a39318a7e2b822aac529";

        private string receiverName;
        private string successMethod;
        private string errorMethod;

        public void SignIn(string receiver, string onSuccess, string onError)
        {
            receiverName = receiver;
            successMethod = onSuccess;
            errorMethod = onError;
            try
            {
                if (!FB.IsInitialized)
                {
                    FB.Init(appId: FacebookAppId, clientToken: FacebookClientToken,
                        onHideUnity: OnHideUnity, onInitComplete: OnInitialized);
                    return;
                }
                BeginLogin();
            }
            catch (Exception exception)
            {
                Fail("Facebook could not start: " + exception.GetBaseException().Message);
            }
        }

        private void OnInitialized()
        {
            if (!FB.IsInitialized)
            {
                Fail("Facebook SDK initialization failed. Check the Meta client token in Unity settings.");
                return;
            }
            FB.ActivateApp();
            BeginLogin();
        }

        private void BeginLogin()
        {
            FB.LogInWithReadPermissions(new List<string> { "public_profile", "email" }, result =>
            {
                if (result == null) { Fail("Facebook returned no login result."); return; }
                if (result.Cancelled) { Fail("Facebook sign-in was cancelled."); return; }
                if (!string.IsNullOrWhiteSpace(result.Error)) { Fail(result.Error); return; }
                var token = result.AccessToken?.TokenString ?? AccessToken.CurrentAccessToken?.TokenString;
                if (string.IsNullOrWhiteSpace(token)) { Fail("Facebook returned an empty access token."); return; }
                Send(successMethod, token);
            });
        }

        private static void OnHideUnity(bool hidden) => Time.timeScale = hidden ? 0f : 1f;
        private void Fail(string message) => Send(errorMethod, message);

        private void Send(string method, string value)
        {
            var receiver = GameObject.Find(receiverName);
            if (receiver == null)
            {
                Debug.LogWarning("Facebook login receiver was not found: " + receiverName);
                return;
            }
            receiver.SendMessage(method, value, SendMessageOptions.DontRequireReceiver);
        }
    }
}
