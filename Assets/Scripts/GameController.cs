using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("Course")]
    [SerializeField] private int totalHoles = 18;
    [SerializeField] private int currentHole = 1;

    [Header("Score")]
    [SerializeField] private int currentHoleScore = 0;
    [SerializeField] private int totalScore = 0;

    [Header("Restart")]
    [SerializeField] private InputActionReference restartAction;

    [Header("Hole Settings")]
    [SerializeField] private bool automaticallyAdvanceHole = true;
    [SerializeField] private float nextHoleDelay = 2f;

    private bool holeComplete;
    private bool courseComplete;
    private float nextHoleTimer;

    public int CurrentHole => currentHole;
    public int CurrentHoleScore => currentHoleScore;
    public int TotalScore => totalScore;
    public bool HoleComplete => holeComplete;
    public bool CourseComplete => courseComplete;

    public event Action<int> OnHoleChanged;
    public event Action<int> OnStrokeAdded;
    public event Action<int> OnHoleCompleted;
    public event Action OnCourseCompleted;
    public event Action OnLevelRestarted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        if (restartAction != null && restartAction.action.WasPressedThisFrame())
        {
            RestartLevel();
        }

        if (!holeComplete || !automaticallyAdvanceHole || courseComplete)
            return;

        nextHoleTimer += Time.deltaTime;

        if (nextHoleTimer >= nextHoleDelay)
        {
            AdvanceToNextHole();
        }
    }

    public void AddStroke()
    {
        if (holeComplete || courseComplete)
            return;

        currentHoleScore++;
        totalScore++;

        OnStrokeAdded?.Invoke(currentHoleScore);
    }

    public void CompleteHole()
    {
        if (holeComplete || courseComplete)
            return;

        holeComplete = true;

        OnHoleCompleted?.Invoke(currentHoleScore);

        if (currentHole >= totalHoles)
        {
            courseComplete = true;
            OnCourseCompleted?.Invoke();
            return;
        }

        nextHoleTimer = 0f;
    }

    public void AdvanceToNextHole()
    {
        if (!holeComplete || courseComplete)
            return;

        currentHole++;

        currentHoleScore = 0;
        holeComplete = false;
        nextHoleTimer = 0f;

        OnHoleChanged?.Invoke(currentHole);
    }

    public void RestartHole()
    {
        currentHoleScore = 0;
        holeComplete = false;
        nextHoleTimer = 0f;

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

        OnLevelRestarted?.Invoke();

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void SetCurrentHole(int hole)
    {
        currentHole = Mathf.Clamp(hole, 1, totalHoles);

        currentHoleScore = 0;
        holeComplete = false;
        nextHoleTimer = 0f;

        OnHoleChanged?.Invoke(currentHole);
    }

    public void SetTotalHoles(int holes)
    {
        totalHoles = Mathf.Max(1, holes);
    }
}