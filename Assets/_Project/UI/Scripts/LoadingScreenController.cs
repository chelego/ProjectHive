using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class LoadingScreenController : MonoBehaviour
{
    [Header("Progress Bar")]
    [SerializeField] private Image progressBarFill;
    [SerializeField] private TMP_Text percentText;

    [Header("Tip Text")]
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private string[] tips;

    [Header("Scene Loading")]
    [SerializeField] private float minimumLoadTime = 1.5f;

    private string targetScene;

    //private void Start()
    //{
    //    if (tips != null && tips.Length > 0)
    //    {
    //        tipText.text = tips[Random.Range(0, tips.Length)];
    //    }
    //}

    private void Start()
    {
        if (tips != null && tips.Length > 0)
        {
            tipText.text = tips[Random.Range(0, tips.Length)];
        }

        // 테스트용: 씬 진입 시 자동으로 로딩 시작 (임시)
        StartLoading("TitleScene"); // 테스트할 씬 이름으로 변경
    }

    public void StartLoading(string sceneName)
    {
        targetScene = sceneName;
        StartCoroutine(LoadSceneRoutine());
    }

    //private IEnumerator LoadSceneRoutine()
    //{
    //    AsyncOperation op = SceneManager.LoadSceneAsync(targetScene);
    //    op.allowSceneActivation = false;

    //    while (op.progress < 0.9f)
    //    {
    //        float displayProgress = Mathf.Clamp01(op.progress / 0.9f);
    //        UpdateProgressUI(displayProgress);
    //        yield return null;
    //    }

    //    UpdateProgressUI(1f);
    //    yield return new WaitForSeconds(minimumLoadTime);

    //    op.allowSceneActivation = true;
    //}

    private IEnumerator LoadSceneRoutine()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        float fakeProgress = 0f;
        float fakeDuration = 2f; // 진행바가 채워지는 데 걸리는 시간 (원하는 만큼 조정)

        while (fakeProgress < 1f)
        {
            fakeProgress += Time.deltaTime / fakeDuration;
            float displayProgress = Mathf.Min(fakeProgress, Mathf.Clamp01(op.progress / 0.9f));
            UpdateProgressUI(displayProgress);
            yield return null;
        }

        UpdateProgressUI(1f);
        yield return new WaitForSeconds(minimumLoadTime);

        op.allowSceneActivation = true;
    }

    private void UpdateProgressUI(float progress)
    {
        progressBarFill.fillAmount = progress;
        percentText.text = Mathf.RoundToInt(progress * 100f) + "%";
    }
}