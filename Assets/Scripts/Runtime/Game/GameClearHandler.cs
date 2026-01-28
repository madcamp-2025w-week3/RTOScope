using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTOScope.Runtime.Game
{
    /// <summary>
    /// 목표 달성 시 CLEAR 화면 출력 및 StartMenu 복귀
    /// </summary>
    public class GameClearHandler : MonoBehaviour
    {
        [Header("Clear Conditions")]
        [SerializeField] private int targetClearCount = 6;
        [SerializeField] private int clearScore = 300;

        [Header("Clear UI (GameOver와 동일 UI 사용)")]
        [SerializeField] private GameObject clearRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private string clearTitle = "CLEAR";
        [SerializeField] private string clearMessage = "MISSION CLEAR";
        [SerializeField] private Color clearColor = new Color(0.2f, 1f, 0.2f, 1f);

        [Header("Timing")]
        [SerializeField] private float clearDelay = 5f;
        [SerializeField] private bool returnToMenuAfterClear = true;
        [SerializeField] private float returnToMenuDelay = 3f;
        [SerializeField] private string menuSceneName = "StartMenu";

        private bool _cleared;

        private void Start()
        {
            if (GameSettings.Instance != null && GameSettings.Instance.TutorialMode)
            {
                if (clearRoot != null)
                    clearRoot.SetActive(false);
                enabled = false;
                return;
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
                ScoreManager.Instance.OnTargetDestroyed += OnTargetDestroyed;
            }

            // 시작 시점에 이미 조건을 만족했는지 확인
            CheckClear();
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
                ScoreManager.Instance.OnTargetDestroyed -= OnTargetDestroyed;
            }
        }

        private void OnScoreChanged(int score)
        {
            CheckClear();
        }

        private void OnTargetDestroyed(int count)
        {
            CheckClear();
        }

        private void CheckClear()
        {
            if (_cleared) return;
            if (ScoreManager.Instance == null) return;

            if (ScoreManager.Instance.TargetsDestroyed >= targetClearCount ||
                ScoreManager.Instance.Score >= clearScore)
            {
                TriggerClear();
            }
        }

        private void TriggerClear()
        {
            _cleared = true;
            StartCoroutine(ShowClearAfterDelay());
        }

        private IEnumerator ShowClearAfterDelay()
        {
            if (clearDelay > 0f)
                yield return new WaitForSeconds(clearDelay);

            if (clearRoot != null)
                clearRoot.SetActive(true);

            if (titleText != null)
            {
                titleText.text = clearTitle;
                titleText.color = clearColor;
            }

            if (messageText != null)
            {
                messageText.text = clearMessage;
                messageText.color = clearColor;
            }

            if (returnToMenuAfterClear && !string.IsNullOrEmpty(menuSceneName))
            {
                if (returnToMenuDelay > 0f)
                    yield return new WaitForSeconds(returnToMenuDelay);
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
            }
        }
    }
}
