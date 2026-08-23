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
        private void ApplyAndroidNotificationPreference()
        {

#if UNITY_ANDROID && !UNITY_EDITOR
try
{
using var bridge = new AndroidJavaClass("com.mkanfani.gravityhalfdead.AndroidNotificationBridge");
bridge.CallStatic(notificationsEnabled ? "enableNotifications" : "disableNotifications");
if (!notificationsEnabled)
MarkNotificationDeviceDisabledAsync();
}
catch (Exception exception)
{
Debug.LogWarning("Android notification bridge unavailable: " + exception.Message);
}
#endif
        }

#if UNITY_ANDROID
private async void MarkNotificationDeviceDisabledAsync()
{
if (auth?.CurrentUser == null || firestore == null)
return;
try
{
await firestore.Collection("users").Document(auth.CurrentUser.UserId)
.Collection("devices").Document(HashedDeviceId()).SetAsync(new Dictionary<string, object>
{
{ "notifications_enabled", false },
{ "platform", "android" },
{ "updated_at", FieldValue.ServerTimestamp }
}, SetOptions.MergeAll);
}
catch (Exception exception)
{
Debug.LogWarning("Could not update notification preference: " + exception.Message);
}
}

    public async void OnFcmTokenReceived(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || auth?.CurrentUser == null || firestore == null)
            return;
        try
        {
            var device = firestore.Collection("users").Document(auth.CurrentUser.UserId)
                .Collection("devices").Document(HashedDeviceId());
            await device.SetAsync(new Dictionary<string, object>
            {
                { "fcm_token", token },
                { "platform", "android" },
                { "notifications_enabled", true },
                { "campaign_topic", "gravity_android" },
                { "updated_at", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);
            SetSupportStatus("Android game notifications are active.", Green);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetSupportStatus("The notification token could not be saved.", Coral);
        }
    }

    public void OnFcmRegistrationError(string message)
    {
        Debug.LogWarning("FCM registration: " + message);
        SetSupportStatus(string.IsNullOrWhiteSpace(message) ? "FCM registration failed." : message, Coral);
    }

#endif
    }
}
