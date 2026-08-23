package com.mkanfani.gravityhalfdead;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import java.util.concurrent.Executor;

/** Android Credential Manager adapter kept deliberately small for Unity. */
public final class GoogleIdentityBridge {
    private GoogleIdentityBridge() {}

    public static void signIn(
            String webClientId,
            String unityObject,
            String successMethod,
            String errorMethod) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            send(unityObject, errorMethod, "Google sign-in could not find the Android activity.");
            return;
        }

        activity.runOnUiThread(() -> {
            try {
                CredentialManager manager = CredentialManager.create(activity);
                GetSignInWithGoogleOption googleOption =
                        new GetSignInWithGoogleOption.Builder(webClientId).build();
                GetCredentialRequest request = new GetCredentialRequest.Builder()
                        .addCredentialOption(googleOption)
                        .build();
                Executor mainExecutor = command -> activity.runOnUiThread(command);

                manager.getCredentialAsync(
                        activity,
                        request,
                        new CancellationSignal(),
                        mainExecutor,
                        new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                            @Override
                            public void onResult(GetCredentialResponse response) {
                                handleCredential(response.getCredential(), unityObject, successMethod, errorMethod);
                            }

                            @Override
                            public void onError(GetCredentialException error) {
                                String message = error.getMessage();
                                if (message == null || message.trim().isEmpty())
                                    message = error.getClass().getSimpleName();
                                send(unityObject, errorMethod, message);
                            }
                        });
            } catch (Exception error) {
                String message = error.getMessage();
                send(unityObject, errorMethod,
                        message == null ? "Unable to start Google sign-in." : message);
            }
        });
    }

    private static void handleCredential(
            Credential credential,
            String unityObject,
            String successMethod,
            String errorMethod) {
        if (!(credential instanceof CustomCredential)) {
            send(unityObject, errorMethod, "Google returned an unsupported credential.");
            return;
        }

        CustomCredential customCredential = (CustomCredential) credential;
        String type = customCredential.getType();
        boolean googleCredential =
                GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(type)
                        || GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_SIWG_CREDENTIAL.equals(type);
        if (!googleCredential) {
            send(unityObject, errorMethod, "Google returned an unexpected credential type.");
            return;
        }

        try {
            GoogleIdTokenCredential google =
                    GoogleIdTokenCredential.createFrom(customCredential.getData());
            send(unityObject, successMethod, google.getIdToken());
        } catch (Exception error) {
            send(unityObject, errorMethod, "The Google identity token could not be read.");
        }
    }

    private static void send(String objectName, String methodName, String message) {
        UnityPlayer.UnitySendMessage(objectName, methodName, message == null ? "" : message);
    }
}
