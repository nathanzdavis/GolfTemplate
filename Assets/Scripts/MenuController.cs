using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [Header("First Hole")]
    [SerializeField] private string firstHoleSceneName;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 0.5f;

    private bool loading;

    private void Start()
    {
        // Start fully transparent.
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;
        }
    }

    public void Play()
    {
        if (loading)
            return;

        if (string.IsNullOrEmpty(firstHoleSceneName))
        {
            Debug.LogError("First hole scene name has not been assigned.");
            return;
        }

        StartCoroutine(LoadFirstHole());
    }

    private IEnumerator LoadFirstHole()
    {
        loading = true;

        if (fadeImage != null)
        {
            float elapsed = 0f;

            Color color = fadeImage.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                color.a =
                    Mathf.Clamp01(
                        elapsed / fadeDuration
                    );

                fadeImage.color = color;

                yield return null;
            }

            color.a = 1f;
            fadeImage.color = color;
        }

        SceneManager.LoadScene(firstHoleSceneName);
    }

    public void Quit()
    {
        Debug.Log("Quitting game...");

        Application.Quit();
    }
}