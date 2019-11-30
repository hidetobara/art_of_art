using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Linq;


namespace Art
{
	using Hash = Dictionary<string, System.Object>;
	using Bins = List<System.Object>;

	public class TimelineController : MonoBehaviour
	{
		const string URL_TIMELINE = "https://art-of-art-api-bv7fhayawq-an.a.run.app/timeline.json";
		const int CELL_LIMIT = 8;
		private string _RootDir;

		public GameObject Content;
		public GameObject Base;

		private List<RawImage> _Cells = new List<RawImage>();

		void Start()
		{
			_RootDir = System.IO.Path.Combine(Application.persistentDataPath, "timeline");
			if (!System.IO.Directory.Exists(_RootDir)) System.IO.Directory.CreateDirectory(_RootDir);

			StartCoroutine(GettingTimeline());
		}

		IEnumerator GettingTimeline()
		{
			List<string> urls = new List<string>();

			UnityWebRequest request = UnityWebRequest.Get(URL_TIMELINE);
			yield return request.SendWebRequest();
			if (request.isNetworkError || request.responseCode != 200)
			{
				Debug.Log(request.responseCode + " " + request.error);
			}
			else
			{
				var text = request.downloadHandler.text;
				Debug.Log(text);
				var hash = Json.Deserialize(text) as Hash;
				if (hash == null || (string)hash["result"] != "ok") yield break;
				var bins = hash["paths"] as Bins;
				if (bins == null) yield break;
				foreach (var o in bins)
				{
					var bin = o as Hash;
					if (bin["url"] == null) continue;
					urls.Add((string)bin["url"]);
				}
			}

			urls = urls.OrderBy(a => Guid.NewGuid()).ToList();
			foreach (var url in urls)
			{
				Debug.Log("url=" + url);
				Texture2D texture = LoadTexture(url);
				if(texture == null)
				{
					request = UnityWebRequest.Get(url);
					yield return request.SendWebRequest();
					texture = SaveTexture(request);
				}
				if (texture == null) continue;
				var image = MakeOneCell();
				image.texture = texture;
				if (_Cells.Count >= CELL_LIMIT) break;
			}
		}

		private Texture2D LoadTexture(string url)
		{
			string filename = System.IO.Path.GetFileName(url);
			string path = System.IO.Path.Combine(Application.persistentDataPath, "timeline", filename);
			if (!System.IO.File.Exists(path)) return null;

			Texture2D texture = new Texture2D(256, 256);
			texture.LoadImage(System.IO.File.ReadAllBytes(path));
			Debug.Log("loaded texture=" + filename);
			return texture;
		}

		private Texture2D SaveTexture(UnityWebRequest request)
		{
			if (request.isNetworkError || request.responseCode != 200) return null;
			var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
			texture.LoadImage(request.downloadHandler.data);

			string filename = System.IO.Path.GetFileName(request.url);
			string path = System.IO.Path.Combine(Application.persistentDataPath, "timeline", filename);
			System.IO.File.WriteAllBytes(path, request.downloadHandler.data);

			return texture;
		}

		private RawImage MakeOneCell()
		{
			var root = Content.GetComponent<RectTransform>();

			var o = Instantiate(Base) as GameObject;
			o.name = String.Format("RawImage{0:00}", _Cells.Count);
			o.transform.SetParent(root, false);
			var cell = o.GetComponent<RectTransform>();
			float x = _Cells.Count % 2 == 0 ? 128 + 64 : 64;
			float y = -192 - _Cells.Count * 128;

			cell.localPosition = new Vector3(x, y, 0);
			cell.localScale = new Vector3(1, 1, 1);
			root.sizeDelta = new Vector2(root.sizeDelta.x, (_Cells.Count + 2) * 128);

			var image = o.GetComponent<RawImage>();
			_Cells.Add(image);
			return image;
		}

		public void OnPressReload()
		{
			ClearCells();
			StartCoroutine(GettingTimeline());
		}

		public void OnPressCamera()
		{
			SceneManager.LoadScene(ArtScene.CAMERA);
		}

		private void ClearCells()
		{
			foreach(var c in _Cells)
			{
				Destroy(c.gameObject);
				Destroy(c.texture);
			}
			_Cells.Clear();
		}
	}
}