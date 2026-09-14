using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using ValheimPictureFrame.Utils;

namespace ValheimPictureFrame
{
    public abstract class PictureFrameBase : MonoBehaviour, Hoverable, Interactable, TextReceiver
    {
        private static readonly string _basePath = Path.Combine(BepInEx.Paths.PluginPath, "ValheimPictureFrame/Assets/Images");
        private Renderer _frameRenderer;

        private int _characterLimit = 100;
        private float _interval = 5.0f;
        private TextureCache textureCache;
        private ZNetView _nview;
        private string[] _textureNames = new string[0];
        private int _nextIndex = 0;

        public Text TextWidget { get; set; }

        public abstract Vector3 PivotOffset { get; set; }
        public abstract string Name { get; set; }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                var option = arg.Split(new[] { '=' }, 2);
                if (option.Length == 2)
                    options[option[0].Trim()] = option[1].Trim();
                else
                    options[arg.Trim()] = "true";
            }

            return options;
        }
        
        private static void Shuffle(string[] names)
        {
            for (int i = names.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = names[i];
                names[i] = names[j];
                names[j] = tmp;
            }
        }

        public string GetHoverName()
        {
            return Name;
        }
        
        public float GetHoverOffset()
        {
            return 0f;
        }
        
        public string GetHoverText()
        {
            if (!PrivateArea.CheckAccess(base.transform.position, 0f, flash: false))
            {
                return $"\"{GetText()}\"";
            }

            string prompt = Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use";
            var loc = typeof(Player).Assembly.GetType("Localization");
            if (loc != null)
            {
                var inst = loc.GetProperty("instance")?.GetValue(null);
                var localize = loc.GetMethod("Localize", new[] { typeof(string) });
                if (inst != null && localize != null)
                    prompt = (string)localize.Invoke(inst, new object[] { prompt });
            }

            return $"\"{GetText()}\"\n{prompt}";
        }

        public string GetText()
        {
            return _nview.GetZDO().GetString("text");
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold)
            {
                return false;
            }

            if (!PrivateArea.CheckAccess(base.transform.position))
            {
                return false;
            }

            TextInput.instance.RequestText(this, "$piece_sign_input", _characterLimit);

            return true;
        }

        public void SetText(string text)
        {
            if (PrivateArea.CheckAccess(base.transform.position))
            {
                _nview.ClaimOwnership();
                TextWidget.text = text;
                UpdatePicture();
                _nview.GetZDO().Set("text", text);
            }
        }

        public void SetTexture(string fileName)
        {
            Renderer pictureRenderer = transform.Find("Pivot/New/Picture").gameObject.GetComponent<Renderer>();
            if (IsUrl(fileName))
            {
                StartCoroutine(textureCache.FetchFromWeb(fileName, texture => pictureRenderer.material.mainTexture = texture));
            }
            pictureRenderer.material.mainTexture = textureCache.Load(fileName);
        }

        public void StartAnimation(string[] textureNames)
        {
            _textureNames = textureNames;
            _nextIndex = 0;
            StartCoroutine(nameof(NextPicture));
        }

        public void StopAnimation()
        {
            StopCoroutine(nameof(NextPicture));
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        private void Awake()
        {
            _frameRenderer = transform.Find("Pivot/New/PictureFrame").gameObject.GetComponent<Renderer>();
            TextWidget = transform.Find("Pivot/Canvas/Text").GetComponent<Text>();
            textureCache = new TextureCache(_basePath);
            _nview = GetComponent<ZNetView>();
            if (_nview.GetZDO() == null)
            {
                return;
            }

            UpdateText();
            InvokeRepeating("UpdateText", 2f, 2f);
        }

        private IEnumerator NextPicture()
        {
            while (true)
            {

                if (_textureNames == null || _textureNames.Length == 0)
                {
                    yield break;
                }

                _nextIndex = _nextIndex % _textureNames.Length;
                SetTexture(_textureNames[_nextIndex]);
                _nextIndex += 1;

                yield return new WaitForSeconds(_interval);

            }
        }

        private void UpdatePicture()
        {
            var text = TextWidget.text.Split(':');
            Transform pivotObject = transform.Find("Pivot");
            transform.localScale = Vector3.one;
            pivotObject.transform.localPosition = Vector3.zero;
            _frameRenderer.enabled = true;

            var filePath = text[0].Trim();
            if (filePath.StartsWith("http"))
            {
                filePath = $"{text[0]}:{text[1]}";
            }
            StopAnimation();

            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (text.Length == 2 || text.Length == 3)
            {
                string[] args = text[text.Length - 1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                options = ParseOptions(args);

                Vector3 pivotOffset = new Vector3(0, 0, 0);

                if (options.ContainsKey("pivot") || options.ContainsKey("p"))
                {
                    string key = options.ContainsKey("pivot") ? "pivot" : "p";
                    string[] pivots = options[key].Split(',');
                    foreach (var pivot in pivots)
                    {
                        switch (pivot)
                        {
                            case "t":
                            case "top":
                                pivotOffset += new Vector3(0, -PivotOffset.y, 0);
                                break;
                            case "b":
                            case "bottom":
                                pivotOffset += new Vector3(0, PivotOffset.y, 0);
                                break;
                            case "r":
                            case "right":
                                pivotOffset += new Vector3(PivotOffset.x, 0, 0);
                                break;
                            case "l":
                            case "left":
                                pivotOffset += new Vector3(-PivotOffset.x, 0, 0);
                                break;
                        }
                    }
                    pivotOffset += new Vector3(0, 0, 0.023f);
                }

                if (options.ContainsKey("scale") || options.ContainsKey("s"))
                {
                    string key = options.ContainsKey("scale") ? "scale" : "s";
                    float scale = Math.Max(Math.Min(float.Parse(options[key]), 10.0f), 0.1f);
                    pivotObject.transform.localPosition = pivotOffset;
                    transform.localScale = Vector3.one * scale;
                    pivotObject.transform.localPosition -= pivotOffset / scale;
                }

                if (options.ContainsKey("interval") || options.ContainsKey("i"))
                {
                    string key = options.ContainsKey("interval") ? "interval" : "i";
                    float interval = Math.Max(float.Parse(options[key]), 0.01f);
                    _interval = interval;
                }

                if (options.ContainsKey("frame") || options.ContainsKey("f"))
                {
                    string key = options.ContainsKey("frame") ? "frame" : "f";
                    if (options[key] == "none")
                        _frameRenderer.enabled = false;
                }
            }

            if (textureCache.IsDirectory(filePath))
            {
                var names = textureCache.LoadTextureNames(filePath);
                if (names != null && names.Length > 1 && options.ContainsKey("shuffle"))
                    Shuffle(names);
                StartAnimation(names);
            }
            else
            {
                SetTexture(filePath);
            }
        }

        private void UpdateText()
        {
            string text = GetText();
            if (TextWidget.text == text)
            {
                return;
            }

            TextWidget.text = text;
            UpdatePicture();
        }
        private static bool IsUrl(string text)
        {
            Uri uriResult;
            return Uri.TryCreate(text, UriKind.Absolute, out uriResult)
                       && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}