using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    // ============================================================
    // COURSE
    // ============================================================

    [Header("Course")]
    [SerializeField] private int totalHoles = 18;
    [SerializeField] private int currentHole = 1;

    [Header("Hole Pars")]
    [Tooltip("Par for each hole, in order. Example: 4, 3, 5, 4...")]
    [SerializeField]
    private int[] holePars =
    {
        4, 4, 4, 4, 4, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 4, 4, 4
    };

    // ============================================================
    // SCORE
    // ============================================================

    [Header("Score")]
    [SerializeField] private int currentHoleScore = 0;
    [SerializeField] private int totalScore = 0;

    // ============================================================
    // SCORE UI
    // ============================================================

    [Header("Score UI")]
    [SerializeField] private Text holeScoreText;

    [Tooltip("How long after holing the ball before the score appears.")]
    [SerializeField] private float scoreDisplayDelay = 1.5f;

    [Tooltip("How long the score stays visible.")]
    [SerializeField] private float scoreDisplayDuration = 2f;

    // ============================================================
    // SCORE AUDIO
    // ============================================================

    [Header("Score Audio")]
    [SerializeField] private AudioSource scoreAudioSource;

    [SerializeField] private AudioClip scoreSound;

    // ============================================================
    // RESTART
    // ============================================================

    [Header("Restart")]
    [SerializeField] private InputActionReference restartAction;

    // ============================================================
    // HOLE SETTINGS
    // ============================================================

    [Header("Hole Settings")]
    [SerializeField] private bool automaticallyAdvanceHole = true;
    [SerializeField] private float nextHoleDelay = 2f;

    // ============================================================
    // RUNTIME
    // ============================================================

    private bool holeComplete;
    private bool courseComplete;
    private float nextHoleTimer;

    private Coroutine scoreDisplayCoroutine;

    // ============================================================
    // EVENTS
    // ============================================================

    public event Action<int> OnHoleChanged;
    public event Action<int> OnStrokeAdded;
    public event Action<int> OnHoleCompleted;
    public event Action OnCourseCompleted;
    public event Action OnLevelRestarted;

    // ============================================================
    // PROPERTIES
    // ============================================================

    public int CurrentHole => currentHole;
    public int CurrentHoleScore => currentHoleScore;
    public int TotalScore => totalScore;
    public bool HoleComplete => holeComplete;
    public bool CourseComplete => courseComplete;

    public int CurrentHolePar
    {
        get
        {
            if (holePars == null ||
                holePars.Length == 0)
            {
                return 4;
            }

            int index = currentHole - 1;

            if (index < 0 || index >= holePars.Length)
                return 4;

            return holePars[index];
        }
    }

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Make sure the array has enough holes.
        if (holePars == null ||
            holePars.Length != totalHoles)
        {
            Array.Resize(
                ref holePars,
                totalHoles
            );
        }

        // Make sure no hole has an invalid par.
        for (int i = 0; i < holePars.Length; i++)
        {
            if (holePars[i] <= 0)
                holePars[i] = 4;
        }

        HideScoreUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        if (restartAction != null)
            restartAction.action.Enable();
    }

    private void OnDisable()
    {
        if (restartAction != null)
            restartAction.action.Disable();
    }

    private void Update()
    {
        if (restartAction != null &&
            restartAction.action.WasPressedThisFrame())
        {
            RestartLevel();
        }

        if (!holeComplete ||
            !automaticallyAdvanceHole ||
            courseComplete)
        {
            return;
        }

        nextHoleTimer += Time.deltaTime;

        if (nextHoleTimer >= nextHoleDelay)
        {
            AdvanceToNextHole();
        }
    }

    // ============================================================
    // STROKES
    // ============================================================

    public void AddStroke()
    {
        if (holeComplete ||
            courseComplete)
        {
            return;
        }

        currentHoleScore++;
        totalScore++;

        OnStrokeAdded?.Invoke(currentHoleScore);
    }

    // ============================================================
    // COMPLETE HOLE
    // ============================================================

    public void CompleteHole()
    {
        if (holeComplete ||
            courseComplete)
        {
            return;
        }

        holeComplete = true;

        OnHoleCompleted?.Invoke(currentHoleScore);

        // Show the golf score after a short delay.
        ShowHoleScoreDelayed();

        if (currentHole >= totalHoles)
        {
            courseComplete = true;

            OnCourseCompleted?.Invoke();

            return;
        }

        nextHoleTimer = 0f;
    }

    // ============================================================
    // SCORE DISPLAY
    // ============================================================

    private void ShowHoleScoreDelayed()
    {
        if (scoreDisplayCoroutine != null)
        {
            StopCoroutine(scoreDisplayCoroutine);
        }

        scoreDisplayCoroutine =
            StartCoroutine(
                ScoreDisplayRoutine()
            );
    }

    private IEnumerator ScoreDisplayRoutine()
    {
        yield return new WaitForSeconds(
            scoreDisplayDelay
        );

        if (holeScoreText != null)
        {
            holeScoreText.text =
                GetGolfScoreText();

            holeScoreText.gameObject.SetActive(true);
        }

        if (scoreAudioSource != null &&
            scoreSound != null)
        {
            scoreAudioSource.PlayOneShot(
                scoreSound
            );
        }

        yield return new WaitForSeconds(
            scoreDisplayDuration
        );

        HideScoreUI();

        scoreDisplayCoroutine = null;
    }

    private void HideScoreUI()
    {
        if (holeScoreText != null)
        {
            holeScoreText.gameObject.SetActive(false);
        }
    }

    // ============================================================
    // GOLF SCORE
    // ============================================================

    private string GetGolfScoreText()
    {
        int strokes =
            currentHoleScore;

        int par =
            CurrentHolePar;

        int difference =
            strokes - par;

        // Hole in one.
        if (strokes == 1)
        {
            return "Hole in One!";
        }

        // Under par.
        if (difference == -3)
        {
            return "Albatross!";
        }

        if (difference == -2)
        {
            return "Eagle!";
        }

        if (difference == -1)
        {
            return "Birdie!";
        }

        // Even.
        if (difference == 0)
        {
            return "Par";
        }

        // One over.
        if (difference == 1)
        {
            return "Bogey";
        }

        // Two over.
        if (difference == 2)
        {
            return "Double Bogey";
        }

        // Three over.
        if (difference == 3)
        {
            return "Triple Bogey";
        }

        // Four or more over.
        return "+" + difference;
    }

    // ============================================================
    // NEXT HOLE
    // ============================================================

    public void AdvanceToNextHole()
    {
        if (!holeComplete ||
            courseComplete)
        {
            return;
        }

        currentHole++;

        currentHoleScore = 0;
        holeComplete = false;
        nextHoleTimer = 0f;

        HideScoreUI();

        OnHoleChanged?.Invoke(
            currentHole
        );
    }

    // ============================================================
    // RESTART
    // ============================================================

    public void RestartHole()
    {
        currentHoleScore = 0;
        holeComplete = false;
        nextHoleTimer = 0f;

        HideScoreUI();

        if (scoreDisplayCoroutine != null)
        {
            StopCoroutine(scoreDisplayCoroutine);
            scoreDisplayCoroutine = null;
        }

        OnLevelRestarted?.Invoke();

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void RestartLevel()
    {
        currentHoleScore = 0;
        totalScore = 0;
        currentHole = 1;

        holeComplete = false;
        courseComplete = false;
        nextHoleTimer = 0f;

        HideScoreUI();

        if (scoreDisplayCoroutine != null)
        {
            StopCoroutine(scoreDisplayCoroutine);
            scoreDisplayCoroutine = null;
        }

        OnLevelRestarted?.Invoke();

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    // ============================================================
    // SETTERS
    // ============================================================

    public void SetCurrentHole(int hole)
    {
        currentHole =
            Mathf.Clamp(
                hole,
                1,
                totalHoles
            );

        currentHoleScore = 0;
        holeComplete = false;
        nextHoleTimer = 0f;

        HideScoreUI();

        OnHoleChanged?.Invoke(
            currentHole
        );
    }

    public void SetTotalHoles(int holes)
    {
        totalHoles =
            Mathf.Max(
                1,
                holes
            );

        Array.Resize(
            ref holePars,
            totalHoles
        );

        for (int i = 0; i < holePars.Length; i++)
        {
            if (holePars[i] <= 0)
                holePars[i] = 4;
        }
    }

    public void SetHolePar(int hole, int par)
    {
        if (hole < 1 ||
            hole > totalHoles)
        {
            return;
        }

        if (holePars == null ||
            holePars.Length != totalHoles)
        {
            Array.Resize(
                ref holePars,
                totalHoles
            );
        }

        holePars[hole - 1] =
            Mathf.Max(
                1,
                par
            );
    }
}