using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BetterPlanets
{
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;
        public static ScreenFader Instance
        {
            get
            {
                if (!_instance) CreateInstance();
                return _instance;
            }
        }

        private Image _fadeImage;

        private static void CreateInstance()
        {
            var go = new GameObject("BetterPlanets_ScreenFader");
            DontDestroyOnLoad(go);
            
            _instance = go.AddComponent<ScreenFader>();
            
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            
            var imgGo = new GameObject("FadeImage");
            imgGo.transform.SetParent(go.transform, false);
            
            _instance._fadeImage = imgGo.AddComponent<Image>();
            _instance._fadeImage.color = new Color(0, 0, 0, 0);
            _instance._fadeImage.raycastTarget = false;
            
            var rt = _instance._fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        
        private static readonly Color DefaultFadeInColor = Color.black;
        private static readonly Color DefaultFadeOutColor = Color.black;

        public IEnumerator FadeInOut(
            float fadeDuration,
            Action midAction,
            float waitAfterFadeIn = 0f,
            Color fadeInColor = default,
            Color fadeOutColor = default
            )
        {
            if (!_fadeImage) yield break;
            
            fadeInColor = fadeInColor == default ? DefaultFadeInColor : fadeInColor;
            fadeOutColor = fadeOutColor == default ? DefaultFadeOutColor : fadeOutColor;
            
            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime; 
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                _fadeImage.color = new Color(fadeInColor.r, fadeInColor.g, fadeInColor.b, alpha);
                yield return null;
            }
            _fadeImage.color = new Color(fadeInColor.r, fadeInColor.g, fadeInColor.b, 1);
            
            midAction?.Invoke();
            
            if (waitAfterFadeIn > 0f)
            {
                yield return new WaitForSecondsRealtime(waitAfterFadeIn);
            }
            
            yield return new WaitForEndOfFrame(); 

            // --- FADE OUT ---
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                _fadeImage.color = new Color(fadeOutColor.r, fadeOutColor.g, fadeOutColor.b, alpha);
                yield return null;
            }
            _fadeImage.color = new Color(fadeOutColor.r, fadeOutColor.g, fadeOutColor.b, 0);
        }
    }
}