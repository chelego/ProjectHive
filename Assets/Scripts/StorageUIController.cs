using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StorageUIController : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("Scene")]
    [SerializeField] private string hideoutSceneName = "HideoutScene";

    public void OnBackButton()
    {
        Debug.Log("Button Click!"); // Ãß°¡
        StartCoroutine(TransitionToHideout());
    }

    private IEnumerator TransitionToHideout()
    {
        yield return StartCoroutine(FadeOut());
        SceneManager.LoadScene(hideoutSceneName);
    }

    private IEnumerator FadeOut()
    {
        float t = 0f;
        fadeOverlay.blocksRaycasts = true;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeOverlay.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        fadeOverlay.alpha = 1f;
    }
}