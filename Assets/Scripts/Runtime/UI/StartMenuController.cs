/*
 * StartMenuController.cs - 시작 화면 UI 컨트롤러
 *
 * [역할]
 * - 싱글/멀티 버튼 클릭 처리
 * - 스케줄러 선택 드롭다운 처리
 * - 배경 이미지/타이틀/버튼과 연결
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace RTOScope.Runtime.UI
{
    public class StartMenuController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string _singleSceneName = "main";
        [SerializeField] private string _multiSceneName = "main";
        [SerializeField] private string _tutorialSceneName = "main";

        [Header("Menu Root (Optional)")]
        [SerializeField] private GameObject _menuRoot;

        [Header("Scheduler Selection (Optional)")]
        [Tooltip("TMP_Dropdown 또는 Dropdown 컴포넌트")]
        [SerializeField] private TMP_Dropdown _schedulerDropdown;

        private void Start()
        {
            // GameSettings 인스턴스 확인/생성
            EnsureGameSettings();

            ResetPersistentState();
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.TutorialMode = false;
            }

            // 드롭다운 초기화
            if (_schedulerDropdown != null)
            {
                _schedulerDropdown.ClearOptions();
                _schedulerDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "Priority",
                    "Round Robin",
                    "FCFS",
                    "SJF"
                });
                _schedulerDropdown.value = GameSettings.Instance != null ? (int)GameSettings.Instance.SelectedScheduler : 0;
                _schedulerDropdown.onValueChanged.AddListener(OnSchedulerChanged);
                OnSchedulerChanged(_schedulerDropdown.value);
            }
        }

        private void EnsureGameSettings()
        {
            if (GameSettings.Instance == null)
            {
                GameObject go = new GameObject("GameSettings");
                go.AddComponent<GameSettings>();
            }
        }

        public void OnSchedulerChanged(int index)
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.SetSchedulerByIndex(index);
            }
        }

        public void OnClickSingle()
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.TutorialMode = false;
            }
            ApplySchedulerSelection();
            LoadScene(_singleSceneName);
        }

        public void OnClickMulti()
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.TutorialMode = false;
            }
            ApplySchedulerSelection();
            LoadScene(_multiSceneName);
        }

        public void OnClickTutorial()
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.TutorialMode = true;
            }
            ApplySchedulerSelection();
            LoadScene(string.IsNullOrEmpty(_tutorialSceneName) ? _singleSceneName : _tutorialSceneName);
        }

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[StartMenu] Scene name is empty.");
                return;
            }

            if (_menuRoot != null)
            {
                _menuRoot.SetActive(false);
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private void ApplySchedulerSelection()
        {
            if (_schedulerDropdown != null)
            {
                OnSchedulerChanged(_schedulerDropdown.value);
            }
        }

        private void ResetPersistentState()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ResetScore();
            }
        }
    }
}

