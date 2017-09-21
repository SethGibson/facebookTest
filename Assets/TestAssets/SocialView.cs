using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Facebook.Unity;

namespace FBAPITest {
    public class SocialView : MonoBehaviour
    {
        [SerializeField]private SocialManager socialManager;

        [SerializeField]private Button      codeBtn;
        [SerializeField]private Text        infoText;
        [SerializeField]private Text        codeText;
        [SerializeField]private Renderer    avatarRenderer;
        private Material                    avatarMtl;


	    public void Start()
        {
		    socialManager.OnCodeReceived.AddListener(onReceivedDeviceCode);
            socialManager.OnTokenRequested.AddListener(onRequestedToken);
            socialManager.OnTokenReceived.AddListener(onReceivedToken);
            socialManager.OnCodeExpired.AddListener(onDeviceCodeTimeout);

            avatarMtl = avatarRenderer.material;
	    }

        private void onDeviceCodeTimeout(string arg0)
        {
            infoText.text = arg0;
        }

        private void onReceivedDeviceCode(string userCode, string uri)
        {
            if(!string.IsNullOrEmpty(userCode)&&!string.IsNullOrEmpty(uri)) {
                codeBtn.enabled = false;
                codeText.text = userCode;
                infoText.text = uri;
            }
        }

        private void onRequestedToken()
        {
            //throw new NotImplementedException();
        }

        private void onReceivedToken(string arg0)
        {
            infoText.text = "Logged In Successfully";
            var getData = new Dictionary<string, string> { {"access_token", arg0 } };
            FB.API(GlobalStrings.kAvatarRequest, HttpMethod.GET, onProfileInfoReceived, getData);
        }

        private void onProfileInfoReceived(IGraphResult result)
        {
            if(string.IsNullOrEmpty(result.Error)) {
                if(result.Texture!=null) {
                    Texture2D avatarTx = (Texture2D)result.Texture;
                    avatarMtl.mainTexture = avatarTx;
                }
                string userName = "";
                if(result.ResultDictionary!=null)
                    result.ResultDictionary.TryGetValue("name", out userName);
                codeText.text = userName;
            }
        }

        public void OnGetCodeButton()
        {
            socialManager.ButtonShim();
        }
    }
}