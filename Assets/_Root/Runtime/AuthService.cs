using System;
using System.Collections.Generic;
using System.Text;
using AppleAuth;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using Pancake.Common;
using Pancake.Leaderboard;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using Random = UnityEngine.Random;
using LoginResult = PlayFab.ClientModels.LoginResult;

namespace Pancake.GameService
{
    /// <summary>
    /// best practice
    /// 1. Anonymous login when logging in for the first time
    /// 2. You should also authentication other than anonymous login so that you can recover your account
    /// 3. I don't like entering the user name and password from the first time
    /// </summary>
    public class AuthService
    {
        //Events to subscribe to for this service
        public delegate void DisplayAuthenticationEvent();

        public delegate void LoginSuccessEvent(LoginResult success);

        public delegate void PlayFabErrorEvent(PlayFabError error);

        public delegate void UpdateUserTitleDisplayNameSuccessEvent(UpdateUserTitleDisplayNameResult success);

        public delegate void UpdatePlayerStatisticsSuccessEvent(UpdatePlayerStatisticsResult success);


        public static event DisplayAuthenticationEvent OnDisplayAuthentication;
        public static event LoginSuccessEvent OnLoginSuccess;
        public static event PlayFabErrorEvent OnPlayFabError;
        public static event UpdateUserTitleDisplayNameSuccessEvent OnUpdateUserTitleDisplayNameSuccess;
        public static event UpdatePlayerStatisticsSuccessEvent OnUpdatePlayerStatisticsSuccess;

        public string email;
        public string userName;
        public string password;
        public string authTicket;
        public GetPlayerCombinedInfoRequestParams infoRequestParams;
        public bool forceLink;
        public bool isLoggedIn;
        public bool isRequestCompleted;
        public static string PlayFabId { get; private set; }
        public static string SessionTicket { get; private set; }
        private const string LOGIN_REMEMBER_KEY = "PLAYFAB_LOGIN_REMEMBER";
        private const string AUTH_TYPE_KEY = "PLAYFAB_AUTH_TYPE";
        private const string CUSTOM_ID_STORE_KEY = "PLAYFAB_CUSTOM_ID_AUTH";
        private static AuthService instance;
#if UNITY_IOS
        public static byte[] identityToken;
        private static IAppleAuthManager appleAuthManager;
        private const string APPLE_USER_ID = "APPLE_USER_ID";
#endif

        public static AuthService Instance
        {
            get
            {
                if (instance == null) instance = new AuthService();
                return instance;
            }
        }

        public AuthService()
        {
            instance = this;

#if UNITY_IOS
            if (AppleAuthManager.IsCurrentPlatformSupported)
            {
                var deserializer = new PayloadDeserializer();
                appleAuthManager = new AppleAuthManager(deserializer);
            }
#endif
        }

        public bool RememberMe { get => PlayerPrefs.GetInt(LOGIN_REMEMBER_KEY, 0) != 0; set => PlayerPrefs.SetInt(LOGIN_REMEMBER_KEY, value ? 1 : 0); }

        public EAuthType AuthType { get => (EAuthType) PlayerPrefs.GetInt(AUTH_TYPE_KEY, 0); set => PlayerPrefs.SetInt(AUTH_TYPE_KEY, (int) value); }

        public string CustomId
        {
            get
            {
                string id = PlayerPrefs.GetString(CUSTOM_ID_STORE_KEY, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Ulid.NewUlid().ToString();
                    PlayerPrefs.SetString(CUSTOM_ID_STORE_KEY, id);
                }

                return id;
            }
            set => PlayerPrefs.SetString(CUSTOM_ID_STORE_KEY, value);
        }

        public void ClearData()
        {
            PlayerPrefs.DeleteKey(LOGIN_REMEMBER_KEY);
            PlayerPrefs.DeleteKey(CUSTOM_ID_STORE_KEY);
        }

        public void Authenticate(EAuthType authType)
        {
            AuthType = authType;
            Authenticate();
        }

        public void Authenticate()
        {
            switch (AuthType)
            {
                case EAuthType.None:
                    OnDisplayAuthentication?.Invoke();
                    break;
                case EAuthType.Silent:
                    SilentlyAuthenticate();
                    break;
                case EAuthType.UsernameAndPassword:

                    break;
                case EAuthType.EmailAndPassword:
                    AuthenticateEmailPassword();
                    break;
                case EAuthType.RegisterPlayFabAccount:
                    AddAccountAndPassword();
                    break;
                case EAuthType.Facebook:
                    AuthenticateFacebook();
                    break;
                case EAuthType.Google:
                    AuthenticateGooglePlayGames();
                    break;
                case EAuthType.Apple:
#if UNITY_IOS
                    AttemptQuickLoginApple();
#endif
                    break;
            }
        }

        private void SilentlyAuthenticate(Action<LoginResult> onSuccess = null, Action<PlayFabError> onError = null)
        {
            if (ServiceSettings.UseCustomIdAsDefault)
            {
                LoginWithCustomId(onSuccess);
            }
            else
            {
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayFabClientAPI.LoginWithAndroidDeviceID(new LoginWithAndroidDeviceIDRequest()
                {
                    TitleId = PlayFabSettings.TitleId,
                    AndroidDevice = SystemInfo.deviceModel,
                    OS = SystemInfo.operatingSystem,
                    AndroidDeviceId = CustomId,
                    CreateAccount = true,
                    InfoRequestParameters = infoRequestParams
                },
                result =>
                {
                    SetResultInfo(result);

                    if (onSuccess == null && OnLoginSuccess != null)
                    {
                        OnLoginSuccess.Invoke(result);
                    }
                    else
                    {
                        onSuccess?.Invoke(result);
                    }
                },
                error =>
                {
                    isLoggedIn = false;
                    isRequestCompleted = true;
                    if (onSuccess == null && OnPlayFabError != null)
                    {
                        OnPlayFabError.Invoke(error);
                    }
                    else
                    {
                        //make sure the loop completes, callback with null
                        onSuccess?.Invoke(null);
                        Debug.LogError(error.GenerateErrorReport());
                    }
                });
#elif (UNITY_IPHONE || UNITY_IOS) && !UNITY_EDITOR
            PlayFabClientAPI.LoginWithIOSDeviceID(new LoginWithIOSDeviceIDRequest()
                {
                    TitleId = PlayFabSettings.TitleId,
                    DeviceModel = SystemInfo.deviceModel,
                    OS = SystemInfo.operatingSystem,
                    DeviceId = CustomId,
                    CreateAccount = true,
                    InfoRequestParameters = infoRequestParams
                },
                result =>
                {
                    SetResultInfo(result);

                    if (onSuccess == null && OnLoginSuccess != null)
                    {
                        OnLoginSuccess.Invoke(result);
                    }
                    else
                    {
                        onSuccess?.Invoke(result);
                    }
                },
                error =>
                {
                    isLoggedIn = false;
                    isRequestCompleted = true;
                    if (onSuccess == null && OnPlayFabError != null)
                    {
                        OnPlayFabError.Invoke(error);
                    }
                    else
                    {
                        //make sure the loop completes, callback with null
                        onSuccess?.Invoke(null);
                        Debug.LogError(error.GenerateErrorReport());
                    }
                });
#else
                LoginWithCustomId(onSuccess);
#endif
            }
        }

        private void LoginWithCustomId(Action<LoginResult> onSuccess)
        {
            PlayFabClientAPI.LoginWithCustomID(
                new LoginWithCustomIDRequest {TitleId = PlayFabSettings.TitleId, CustomId = CustomId, CreateAccount = true, InfoRequestParameters = infoRequestParams},
                result =>
                {
                    SetResultInfo(result);

                    if (onSuccess == null && OnLoginSuccess != null)
                    {
                        OnLoginSuccess.Invoke(result);
                    }
                    else
                    {
                        onSuccess?.Invoke(result);
                    }
                },
                error =>
                {
                    SetErrorInfo();
                    if (onSuccess == null && OnPlayFabError != null)
                    {
                        OnPlayFabError.Invoke(error);
                    }
                    else
                    {
                        //make sure the loop completes, callback with null
                        onSuccess?.Invoke(null);
                        Debug.LogError(error.GenerateErrorReport());
                    }
                });
        }

        /// <summary>
        /// Authenticate a user in PlayFab using an Email & Password
        /// </summary>
        private void AuthenticateEmailPassword()
        {
            //Check if the users has opted to be remembered.
            if (RememberMe && string.IsNullOrEmpty(CustomId))
            {
                PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest
                    {
                        TitleId = PlayFabSettings.TitleId, CustomId = CustomId, CreateAccount = true, InfoRequestParameters = infoRequestParams
                    },
                    result =>
                    {
                        SetResultInfo(result);
                        OnLoginSuccess?.Invoke(result);
                    },
                    error =>
                    {
                        SetErrorInfo();
                        OnPlayFabError?.Invoke(error);
                    });

                return;
            }

            // If username & password is empty, then do not continue, and Call back to Authentication UI Display 
            if (!RememberMe && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password))
            {
                OnDisplayAuthentication?.Invoke();
                return;
            }

            //We have not opted for remember me in a previous session, so now we have to login the user with email & password.
            PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest
                {
                    TitleId = PlayFabSettings.TitleId, Email = email, Password = password, InfoRequestParameters = infoRequestParams
                },
                result =>
                {
                    SetResultInfo(result);

                    //Note: At this point, they already have an account with PlayFab using a Username (email) & Password
                    //If RememberMe is checked, then generate a new Guid for Login with CustomId.
                    if (RememberMe)
                    {
                        PlayerPrefs.DeleteKey(CUSTOM_ID_STORE_KEY);
                        AuthType = EAuthType.EmailAndPassword;
                        //Fire and forget, but link a custom ID to this PlayFab Account.
                        PlayFabClientAPI.LinkCustomID(new LinkCustomIDRequest {CustomId = CustomId, ForceLink = forceLink}, null, null);
                    }

                    OnLoginSuccess?.Invoke(result);
                },
                error =>
                {
                    SetErrorInfo();
                    OnPlayFabError?.Invoke(error);
                });
        }

        /// <summary>
        /// Register a user with an Email & Password
        /// Note: We are not using the RegisterPlayFab API
        /// </summary>
        private void AddAccountAndPassword()
        {
            //Any time we attempt to register a player, first silently authenticate the player.
            //This will retain the players True Origination (Android, iOS, Desktop)
            SilentlyAuthenticate((result) =>
            {
                if (result == null)
                {
                    //something went wrong with Silent Authentication, Check the debug console.
                    OnPlayFabError?.Invoke(new PlayFabError() {Error = PlayFabErrorCode.UnknownError, ErrorMessage = "Silent Authentication by device failed"});
                }

                //Note: If silent auth is success, which is should always be and the following 
                //below code fails because of some error returned by the server ( like invalid email or bad password )
                //this is okay, because the next attempt will still use the same silent account that was already created.

                //Now add our username & password.
                PlayFabClientAPI.AddUsernamePassword(new AddUsernamePasswordRequest()
                    {
                        Username = !string.IsNullOrEmpty(userName) ? userName : CustomId, //Because it is required & Unique and not supplied by User.
                        Email = email,
                        Password = password,
                    },
                    _ =>
                    {
                        if (OnLoginSuccess != null)
                        {
                            //Store identity and session
                            SetResultInfo(result);

                            //If they opted to be remembered on next login.
                            if (RememberMe)
                            {
                                //Generate a new Guid 
                                PlayerPrefs.DeleteKey(CUSTOM_ID_STORE_KEY);
                                //Fire and forget, but link the custom ID to this PlayFab Account.
                                PlayFabClientAPI.LinkCustomID(new LinkCustomIDRequest() {CustomId = CustomId, ForceLink = forceLink}, null, null);
                            }

                            //Override the auth type to ensure next login is using this auth type.
                            AuthType = EAuthType.EmailAndPassword;

                            //Report login result back to subscriber.
                            OnLoginSuccess.Invoke(result);
                        }
                    },
                    error =>
                    {
                        SetErrorInfo();
                        //Report error result back to subscriber
                        OnPlayFabError?.Invoke(error);
                    });
            });
        }

        private void AuthenticateFacebook()
        {
#if FACEBOOK
        if (FB.IsInitialized && FB.IsLoggedIn && !string.IsNullOrEmpty(authTicket))
        {
            PlayFabClientAPI.LoginWithFacebook(new LoginWithFacebookRequest()
            {
                TitleId = PlayFabSettings.TitleId,
                AccessToken = authTicket,
                CreateAccount = true,
                InfoRequestParameters = infoRequestParams
            }, result =>
            {
                //Store Identity and session
                SetResultInfo(result);

                //check if we want to get this callback directly or send to event subscribers.
                //report login result back to the subscriber
                OnLoginSuccess?.Invoke(result);
            }, error =>
            {
                SetErrorInfo();
                //report errro back to the subscriber
                OnPlayFabError?.Invoke(error);
            });
        }
        else
        {
            OnDisplayAuthentication?.Invoke();
        }
#endif
        }

        private void AuthenticateGooglePlayGames()
        {
#if GOOGLEGAMES
        PlayFabClientAPI.LoginWithGoogleAccount(new LoginWithGoogleAccountRequest()
        {
            TitleId = PlayFabSettings.TitleId,
            ServerAuthCode = authTicket,
            InfoRequestParameters = infoRequestParams,
            CreateAccount = true
        }, (result) =>
        {
            //Store Identity and session
            SetResultInfo(result);

            //check if we want to get this callback directly or send to event subscribers.
            //report login result back to the subscriber
            OnLoginSuccess?.Invoke(result);
        }, (error) =>
        {
            SetErrorInfo();
            //report errro back to the subscriber
            OnPlayFabError?.Invoke(error);
        });
#endif
        }

        private void SetResultInfo(LoginResult result)
        {
            PlayFabId = result.PlayFabId;
            SessionTicket = result.SessionTicket;
            isLoggedIn = true;
            isRequestCompleted = true;
        }

        private void SetErrorInfo()
        {
            isLoggedIn = false;
            isRequestCompleted = true;
        }

        public void UnlinkSilentAuth()
        {
            SilentlyAuthenticate(result =>
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                //Fire and forget, unlink this android device.
                PlayFabClientAPI.UnlinkAndroidDeviceID(new UnlinkAndroidDeviceIDRequest() {AndroidDeviceId = CustomId}, null, null);

#elif (UNITY_IPHONE || UNITY_IOS) && !UNITY_EDITOR
                PlayFabClientAPI.UnlinkIOSDeviceID(new UnlinkIOSDeviceIDRequest() {DeviceId = CustomId}, null, null);
#else
                PlayFabClientAPI.UnlinkCustomID(new UnlinkCustomIDRequest {CustomId = CustomId}, null, null);
#endif
            });
        }

        public void Reset()
        {
            isLoggedIn = false;
            isRequestCompleted = false;
        }

        /// <summary>
        /// require enable put static score in setting dashboard
        /// </summary>
        /// <param name="value"></param>
        /// <param name="nameTable"></param>
        public void UpdatePlayerStatistics(int value, string nameTable)
        {
            PlayFabClientAPI.UpdatePlayerStatistics(
                new UpdatePlayerStatisticsRequest {Statistics = new List<StatisticUpdate> {new() {StatisticName = nameTable, Value = value}}},
                result => { OnUpdatePlayerStatisticsSuccess?.Invoke(result); },
                error => { OnPlayFabError?.Invoke(error); });
        }

#if UNITY_IOS
        /// <summary>
        /// use for apple login
        /// </summary>
        private void AttemptQuickLoginApple()
        {
            var quickLoginArgs = new AppleAuthQuickLoginArgs();

            appleAuthManager.QuickLogin(quickLoginArgs, OnAppleLoginSuccess, OnAppleLoginFail);
        }

        private void OnAppleLoginSuccess(ICredential credential)
        {
            if (credential is IAppleIDCredential appleIdCredential)
            {
                PlayerPrefs.SetString(APPLE_USER_ID, appleIdCredential.User);
                identityToken = appleIdCredential.IdentityToken;

                PlayFabClientAPI.LoginWithApple(new LoginWithAppleRequest()
                    {
                        TitleId = PlayFabSettings.TitleId,
                        IdentityToken = Encoding.UTF8.GetString(identityToken),
                        CreateAccount = true,
                        InfoRequestParameters = infoRequestParams
                    },
                    result =>
                    {
                        //Store Identity and session
                        SetResultInfo(result);

                        //check if we want to get this callback directly or send to event subscribers.
                        //report login result back to the subscriber
                        OnLoginSuccess?.Invoke(result);
                    },
                    error =>
                    {
                        //report errro back to the subscriber
                        OnPlayFabError?.Invoke(error);
                    });
            }
        }

        private static void OnAppleLoginFail(IAppleError error) { Debug.LogWarning("[Login Apple]: failed by " + error); }
#endif

        private static void UpdateUserTitleDisplayName(string name)
        {
            PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest {DisplayName = name},
                result => { OnUpdateUserTitleDisplayNameSuccess?.Invoke(result); },
                error => { OnPlayFabError?.Invoke(error); });
        }
    }
}