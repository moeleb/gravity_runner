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
        private void OpenPrivacyPolicy()
        {
            Application.OpenURL("https://gravity-half-dead.web.app/privacy");
        }

        private void FocusSupportForm()
        {
            supportSubjectInput?.Select();
            supportSubjectInput?.ActivateInputField();
        }

        private void OpenSupportImagePicker()
        {

#if UNITY_ANDROID && !UNITY_EDITOR
try
{
using var bridge = new AndroidJavaClass("com.mkanfani.gravityhalfdead.SupportImagePicker");
bridge.CallStatic("pickImages");
SetSupportStatus("Choose one or more images. Combined limit: 10 MB.", Cyan);
}
catch (Exception exception)
{
Debug.LogException(exception);
SetSupportStatus("Could not open the Android image picker.", Coral);
}
#elif UNITY_IOS && !UNITY_EDITOR
GHD_PickSupportImages();
SetSupportStatus("Choose one or more images. Combined limit: 10 MB.", Cyan);
#else
            SetSupportStatus("Image selection is available in Android and iOS builds.", Muted);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
[DllImport("__Internal")]
private static extern void GHD_PickSupportImages();
#endif

        public void OnSupportImagesPicked(string newlineSeparatedPaths)
        {
            supportImagePaths.Clear();
            long totalBytes = 0;
            foreach (var path in newlineSeparatedPaths.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!File.Exists(path))
                    continue;

                var length = new FileInfo(path).Length;
                if (length <= 0 || totalBytes + length > SupportUploadLimitBytes)
                {
                    supportImagePaths.Clear();
                    SetSupportStatus("The selected images exceed the 10 MB combined limit.", Coral);
                    return;
                }

                totalBytes += length;
                supportImagePaths.Add(path);
            }

            if (supportAttachmentText != null)
                supportAttachmentText.text = supportImagePaths.Count + " image(s) · " + FormatMegabytes(totalBytes) + " MB / 10 MB";
            SetSupportStatus(supportImagePaths.Count > 0 ? "Images ready to upload." : "No images selected.",
                supportImagePaths.Count > 0 ? Green : Muted);
        }

        public void OnSupportImagePickerError(string message)
        {
            SetSupportStatus(string.IsNullOrWhiteSpace(message) ? "Image selection was cancelled." : message, Coral);
        }

        private async void SubmitSupportRequest()
        {
            var subject = supportSubjectInput != null ? supportSubjectInput.text.Trim() : string.Empty;
            var body = supportBodyInput != null ? supportBodyInput.text.Trim() : string.Empty;
            if (subject.Length < 3 || body.Length < 10)
            {
                SetSupportStatus("Add a subject and a clear description before sending.", Coral);
                return;
            }

            if (auth?.CurrentUser == null)
            {
                SetSupportStatus("Your player session is not ready. Please reopen Settings.", Coral);
                return;
            }

            SetSupportControls(false);
            var requestId = Guid.NewGuid().ToString("N");
            try
            {
                var token = await auth.CurrentUser.TokenAsync(false);
                var uploadedPaths = new List<string>();
                for (var i = 0; i < supportImagePaths.Count; i++)
                {
                    SetSupportStatus("Uploading image " + (i + 1) + " of " + supportImagePaths.Count + "…", Cyan);
                    uploadedPaths.Add(await UploadSupportImageAsync(supportImagePaths[i], requestId, i, token));
                }

                SetSupportStatus("Sending your support request…", Cyan);
                var payload = new SupportRequestPayload
                {
                    requestId = requestId,
                    subject = subject,
                    body = body,
                    attachmentPaths = uploadedPaths.ToArray()
                };
                await SendSupportEmailRequestAsync(JsonUtility.ToJson(payload), token);

                supportSubjectInput.text = string.Empty;
                supportBodyInput.text = string.Empty;
                foreach (var path in supportImagePaths)
                {
                    try { File.Delete(path); } catch { }
                }
                supportImagePaths.Clear();
                supportAttachmentText.text = "Multiple images · 10 MB total maximum";
                SetSupportStatus("Sent. Support request " + requestId[..8].ToUpperInvariant() + " was received.", Green);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetSupportStatus("Could not send: " + FriendlyError(exception), Coral);
            }
            finally
            {
                SetSupportControls(true);
            }
        }

        private async Task<string> UploadSupportImageAsync(string localPath, string requestId, int index, string token)
        {
            var bucket = FirebaseApp.DefaultInstance.Options.StorageBucket;
            if (string.IsNullOrWhiteSpace(bucket))
                throw new InvalidOperationException("Firebase Storage bucket is not configured.");

            var extension = Path.GetExtension(localPath).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".webp")
                extension = ".jpg";
            var objectPath = "support/" + auth.CurrentUser.UserId + "/" + requestId + "/image-" + (index + 1) + extension;
            var endpoint = "https://firebasestorage.googleapis.com/v0/b/" + Uri.EscapeDataString(bucket)
                + "/o?uploadType=media&name=" + Uri.EscapeDataString(objectPath);
            var bytes = await File.ReadAllBytesAsync(localPath);
            using var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", ImageContentType(extension));
            await SendWebRequestAsync(request);
            return objectPath;
        }

        private static async Task SendWebRequestAsync(UnityWebRequest request)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(request.responseCode + ": " + request.downloadHandler.text);
        }

        private async Task SendSupportEmailRequestAsync(string json, string token)
        {
            const string endpoint = "https://europe-west1-gravity-half-dead.cloudfunctions.net/submitSupportRequest";
            using var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");
            await SendWebRequestAsync(request);
        }

        private void SetSupportControls(bool enabled)
        {
            if (supportAttachButton != null) supportAttachButton.interactable = enabled;
            if (supportSendButton != null) supportSendButton.interactable = enabled;
        }

        private void SetSupportStatus(string message, Color color)
        {
            if (supportStatusText == null)
                return;
            supportStatusText.text = message;
            supportStatusText.color = color;
        }

        private static string ImageContentType(string extension)
        {
            return extension == ".png" ? "image/png" : extension == ".webp" ? "image/webp" : "image/jpeg";
        }

        private static string FormatMegabytes(long bytes)
        {
            return (bytes / (1024f * 1024f)).ToString("0.0");
        }

        private static string HashedDeviceId()
        {
            var raw = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrWhiteSpace(raw) || raw == SystemInfo.unsupportedIdentifier)
                raw = SystemInfo.deviceModel + "|" + SystemInfo.operatingSystem;
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class SupportRequestPayload
        {
            public string requestId;
            public string subject;
            public string body;
            public string[] attachmentPaths;
        }
    }
}
