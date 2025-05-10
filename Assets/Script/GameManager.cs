using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    #region Inspector Variables
    [Header("Win Panel Settings")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private List<GameObject> balloons = new List<GameObject>();
    [SerializeField] private float balloonAnimationDuration = 0.5f;
    [SerializeField] private float balloonScaleUp = 1.2f;
    #endregion

    #region Private Variables
    private List<exitLine> exitLines = new List<exitLine>();
    private float gameStartTime;
    private bool isGameFinished = false;
    #endregion

    #region Unity Methods
    void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        foreach (var balloon in balloons)
        {
            if (balloon != null)
                balloon.SetActive(false);
        }

        gameStartTime = Time.time;
        exitLines.AddRange(FindObjectsOfType<exitLine>());
    }

    void Update()
    {
        if (isGameFinished)
            return;

        bool allWagonsFull = true;
        foreach (var exitLine in exitLines)
        {
            if (!exitLine.IsWeganFull)
            {
                allWagonsFull = false;
                break;
            }
        }

        if (allWagonsFull)
        {
            FinishGame();
        }
    }
    #endregion

    #region Private Methods
    private void FinishGame()
    {
        isGameFinished = true;
        float gameTime = Time.time - gameStartTime;
        ShowWinPanel(gameTime);
    }

    private void ShowWinPanel(float gameTime)
    {
        if (winPanel == null)
            return;

        winPanel.SetActive(true);

        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(gameTime / 60);
            int seconds = Mathf.FloorToInt(gameTime % 60);
            timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        int balloonCount = 1;
        if (gameTime <= 120f)
            balloonCount = 3;
        else if (gameTime <= 180f)
            balloonCount = 2;

        StartCoroutine(ShowBalloons(balloonCount));
    }
    #endregion

    #region Coroutines
    private IEnumerator ShowBalloons(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (i < balloons.Count)
            {
                GameObject balloon = balloons[i];
                if (balloon != null)
                {
                    balloon.SetActive(true);
                    balloon.transform.localScale = Vector3.zero;

                    Sequence balloonSequence = DOTween.Sequence();
                    balloonSequence.Append(balloon.transform.DOScale(Vector3.one * balloonScaleUp, balloonAnimationDuration).SetEase(Ease.OutBack))
                                  .Append(balloon.transform.DOScale(Vector3.one, balloonAnimationDuration).SetEase(Ease.InOutQuad));

                    yield return balloonSequence.WaitForCompletion();
                }
            }
        }
    }
    #endregion
} 