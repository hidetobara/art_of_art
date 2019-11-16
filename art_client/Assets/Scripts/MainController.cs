using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;


public class MainController : MonoBehaviour
{
	enum CameraStatus { Playing, Uploading, Responsed }
	enum StyleType { AZS, JS, INK, KLMT }
	delegate Color[] ModifyColorsDelegate(Color[] colors, int block);

	const string URL_TRANSFER = "https://art-of-art-api-bv7fhayawq-an.a.run.app/transfer";
	const string URL_TWITTER_INTENT = "https://twitter.com/intent/tweet?text=";

	public RawImage RawImage;
	public RawImage BackImage;

	WebCamTexture _WebCamTexture;
	CameraStatus _Status = CameraStatus.Playing;
	StyleType _CurrentStyle = StyleType.AZS;
	Dictionary<string, Texture> _Images = new Dictionary<string, Texture>();

	ModifyColorsDelegate _ModifyTexture = null;
	Quaternion _CameraRotation;
	string _TransferedUrl = null;

	void Start()
    {
		_WebCamTexture = new WebCamTexture(512, 512);
		_WebCamTexture.Play();
		RawImage.texture = _WebCamTexture;
		Debug.Log(_WebCamTexture.height + "," + _WebCamTexture.width);

#if UNITY_EDITOR
		_CameraRotation = Quaternion.Euler(0, 0, 0);
#elif UNITY_ANDROID
		_CameraRotation = Quaternion.Euler(0, 0, -_WebCamTexture.videoRotationAngle); // -90
		_ModifyTexture = RotateColors;
#endif
		RawImage.transform.rotation = _CameraRotation;

		foreach (var s in GameObject.FindObjectsOfType<StyleButton>())
		{
			if (s.Name == null) continue;
			var i = s.gameObject.GetComponent<Image>();
			if (i == null) continue;
			_Images[s.Name] = i.mainTexture;
		}
	}

    void Update()
    {
    }

	public void OnPressTimeline()
	{

	}

	public void OnPressTwitter()
	{
		if (_TransferedUrl == null) return;
		string text = URL_TWITTER_INTENT + System.Web.HttpUtility.UrlEncode(GenerateHashTag(_CurrentStyle) + " " + _TransferedUrl);
		Debug.Log(text);
		Application.OpenURL(text);
	}

	public void OnPressShotAzs(){ OnPressStyle(StyleType.AZS); }
	public void OnPressShotJs() { OnPressStyle(StyleType.JS); }
	public void OnPressShotInk() { OnPressStyle(StyleType.INK); }
	public void OnPressShotKlmt() { OnPressStyle(StyleType.KLMT); }
	public void OnPressCamera() { StartCoroutine(Uploading(_CurrentStyle)); }

	private void OnPressStyle(StyleType type)
	{
		if (_Status == CameraStatus.Playing)
		{
			_CurrentStyle = type;
			BackImage.texture = _Images[type.ToString().ToLower()];
		}
		if (_Status == CameraStatus.Responsed)
		{
			_TransferedUrl = null;
			_WebCamTexture.Play();
			RawImage.texture = _WebCamTexture;
			_Status = CameraStatus.Playing;
			RawImage.transform.rotation = _CameraRotation;
		}
	}

	private IEnumerator Uploading(StyleType type)
	{
		// 画像を正方形に
		int x, y, block;
		if(_WebCamTexture.width > _WebCamTexture.height)
		{
			x = (_WebCamTexture.width - _WebCamTexture.height) / 2;
			y = 0;
			block = _WebCamTexture.height;
		}
		else
		{
			x = 0;
			y = (_WebCamTexture.height - _WebCamTexture.width) / 2;
			block = _WebCamTexture.width;
		}
		Texture2D takenPhoto = new Texture2D(block, block, TextureFormat.RGB24, false);
		var colors = _WebCamTexture.GetPixels(x, y, block, block);
		if (_ModifyTexture != null) colors = _ModifyTexture(colors, block);
		takenPhoto.SetPixels(colors);
		takenPhoto.Apply();

		Texture2D loadingPhoto = new Texture2D(_WebCamTexture.width, _WebCamTexture.height, TextureFormat.RGB24, false);
		loadingPhoto.SetPixels(_WebCamTexture.GetPixels());
		loadingPhoto.Apply();
		
		// アップロード開始
		_WebCamTexture.Stop();
		_Status = CameraStatus.Uploading;
		RawImage.texture = loadingPhoto;
		StartCoroutine(GettingUploadingRaw());

		WWWForm form = new WWWForm();
		form.AddBinaryData("target", takenPhoto.EncodeToJPG());
		form.AddField("mode", type.ToString().ToLower());
		Destroy(takenPhoto);
		using (UnityWebRequest www = UnityWebRequest.Post(URL_TRANSFER, form))
		{
			yield return www.SendWebRequest();

			if (www.isNetworkError || www.isHttpError)
			{
				Debug.Log(www.error);
			}
			else
			{
				Debug.Log("Transfered ! " + www.url);
				RawImage.transform.rotation = Quaternion.Euler(0, 0, 0);
				byte[] bytes = www.downloadHandler.data;
				Texture2D texture = new Texture2D(256, 256);
				texture.LoadImage(bytes);
				RawImage.texture = texture;
				RawImage.color = Color.white;
				_TransferedUrl = www.url;
			}
			_Status = CameraStatus.Responsed;
		}
	}

	private IEnumerator GettingUploadingRaw()
	{
		const float TOTAL_MAX = 5f;
		float remain = 0;
		while(_Status == CameraStatus.Uploading)
		{
			remain += Time.deltaTime;
			float rate = remain / TOTAL_MAX;
			RawImage.color = new Color(Mathf.Cos(rate) * 0.5f + 0.5f, Mathf.Cos(rate * 7) * 0.5f + 0.5f, Mathf.Cos(rate * 5) * 0.5f + 0.5f);
			if (rate >= TOTAL_MAX) break;
			yield return null;
		}
	}

	private Color[] RotateColors(Color[] colors, int block)
	{
		Color[] new_colors = new Color[colors.Length];
		for (int h = 0; h < block; h++)
		{
			for (int w = 0; w < block; w++)
			{
				new_colors[h * block + w] = colors[w * block + (block - h - 1)];
			}
		}
		return new_colors;
	}

	private string GenerateHashTag(StyleType type)
	{
		switch(type)
		{
			case StyleType.AZS: return "#人工あずさ";
			case StyleType.INK: return "#人工水墨画";
			case StyleType.JS: return "#人工小学生";
			case StyleType.KLMT: return "#人工クリムト";
			default: return "#人工画家";
		}
	}
}
