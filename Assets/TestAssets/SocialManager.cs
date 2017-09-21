using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Facebook.Unity;

namespace FBAPITest {
    [Serializable]public class OnDeviceCodeReceived : UnityEvent<string, string> { }        // got a device login code from FB
    [Serializable]public class OnUserAccessTokenRequested : UnityEvent { }  // waiting on the user to enter the code
    [Serializable]public class OnUserAccessTokenReceived : UnityEvent<string> { }   // got a user access token, i.e. user entered the device code and fb took it
    [Serializable]public class OnDeviceCodeExpired : UnityEvent<string> { }         // user didn't enter the device code in time

    public class SocialManager : MonoBehaviour
    {
        private bool pollingFinished = false;
        private string FB_APP_ID = "";
        private string FB_CLIENT_TOKEN = "";
        private string FB_SESSION_TICKET = "";
        private string FB_USER_TOKEN = "";
        private Coroutine pollingCoroutine;

        private long pollingInterval;

        [SerializeField]public OnDeviceCodeReceived         OnCodeReceived;
        [SerializeField]public OnUserAccessTokenRequested   OnTokenRequested;
        [SerializeField]public OnUserAccessTokenReceived    OnTokenReceived;
        [SerializeField]public OnDeviceCodeExpired          OnCodeExpired;

        public string FBAccessToken {
            get {
                if(!string.IsNullOrEmpty(FB_APP_ID)&&!string.IsNullOrEmpty(FB_CLIENT_TOKEN))
                    return FB_APP_ID+"|"+FB_CLIENT_TOKEN;
                return "";
            }
        }

	    public void Start()
        {
            FB_APP_ID = Facebook.Unity.Settings.FacebookSettings.AppId;
            FB_CLIENT_TOKEN = Facebook.Unity.Settings.FacebookSettings.ClientToken;		

            if(!FB.IsInitialized)
                FB.Init(onFacebookInitialized);
	    }

        #region Coroutines
        private IEnumerator waitForDeviceCodeEntry(string code, long expiresIn, long interval)
        {
            double start = Time.time;
            pollingFinished = false;
            pollingInterval = interval+1;
            while((Time.time-start)<expiresIn) {
                var pollData = new Dictionary<string, string>() { { "access_token", FB_APP_ID+"|"+FB_CLIENT_TOKEN }, { "code", code } };
                FB.API(GlobalStrings.kLoginStatus, HttpMethod.POST, onFacebookDeviceCodePollResult, pollData);
                if(OnTokenRequested!=null)
                    OnTokenRequested.Invoke();

                yield return new WaitForSeconds(pollingInterval);
            }
        
            pollingFinished = true;
            pollingCoroutine = null;
        }
        #endregion

        #region Callbacks
        private void onFacebookInitialized()
        {
            if(FB.IsLoggedIn) { }

            FB.ActivateApp();
        }

        // We've made our intitial API call to /device/login, have gotten a code back
        // and are going to start the polling coroutine here
        private void onFacebookDeviceCodeReceived(IGraphResult result)
        {
            if(!string.IsNullOrEmpty(result.Error)) {
                // run error handling
                // Not sure what error codes we would get other than HTTP codes
                // Soooo...just have a beer here?
                return;
            }

            var resultData = result.ResultDictionary;
            string requestCode, userCode, uri;
            long expiry, interval;
            
            if(!resultData.TryGetValue("code", out requestCode))
                return;
            if(!resultData.TryGetValue("user_code", out userCode))
                return;
            if(!resultData.TryGetValue("verification_uri", out uri))
                return;
            if(!resultData.TryGetValue("expires_in", out expiry))
                return;
            if(!resultData.TryGetValue("interval", out interval))
                return;
            
            if(pollingCoroutine!=null)
                StopCoroutine(pollingCoroutine);
            pollingCoroutine = StartCoroutine(waitForDeviceCodeEntry(requestCode, expiry, interval));
            if(OnCodeReceived!=null)
                OnCodeReceived.Invoke(userCode, uri);
        }

        // We've submitted an API call to /device/login_status and gotten a result object back
        // All we care about is an access token or an error
        private void onFacebookDeviceCodePollResult(IGraphResult result)
        {
            if(!string.IsNullOrEmpty(result.Error)) {
                string subCode = "";
                result.ResultDictionary.TryGetValue("error_subcode", out subCode);
                if(gotPollingError(subCode)) {
                    // we've timed out so we need to make the user request another code
                    if(OnCodeExpired!=null) {
                        OnCodeExpired.Invoke("Device Access Code Expired, Please Re-attempt Login");
                    }
                }
                return;
            }

            StopCoroutine(pollingCoroutine);
            Debug.Log("<color=orange>Code Entered</color>");

            string t = setUserToken(result.ResultDictionary, "");
            if(!string.IsNullOrEmpty(t)) {
                FB_USER_TOKEN = t;
                if(OnTokenReceived!=null)
                    OnTokenReceived.Invoke(FB_USER_TOKEN);
            }
        }

        #endregion

        #region Funcs
        private void doFacebookLogin(string token, string scope, FacebookDelegate<IGraphResult> loginCallback)
        {
            var loginData = new Dictionary<string, string>() { { "access_token", token }, { "scope", scope } };
            FB.API(GlobalStrings.kDeviceLogin, HttpMethod.POST, loginCallback, loginData);
        }

        private string setUserToken(IDictionary<string, object> resultsDict, string prefsKey)
        {
            string token = "";
            if(!resultsDict.TryGetValue("access_token", out token))
                return null;

            return token;
        }

        private bool gotPollingError(string errorSubcode)
        {
            bool retval = true;
            switch(errorSubcode) {
                case "1349174": { // keep polling
                    retval = false;
                }
                break;
                case "1349172": { // pad out polling interval and continue (this should never happen)
                    if(pollingCoroutine!=null&&!pollingFinished) {
                        pollingInterval+=1;
                        retval = false;
                    }
                }

                break;
                case "1349152": {// bail and start request flow over
                    retval = true;
                }
                break;
                default: {
                    retval = false;
                }
                break;
            }

            return retval;
        }
        #endregion

        #region Stuff I Don't Want To Do In This Class
        public void ButtonShim()
        {
            doFacebookLogin(FB_APP_ID+"|"+FB_CLIENT_TOKEN, "public_profile", onFacebookDeviceCodeReceived);
        }
        #endregion
    }
}
