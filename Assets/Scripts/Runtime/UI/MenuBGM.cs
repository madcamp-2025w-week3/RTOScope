/*
 * MenuBGM.cs - 시작 화면 배경 음악 유지
 *
 * [역할]
 * - 시작 메뉴 씬에서 BGM 재생
 * - 씬 전환 시 유지/중지 옵션 제공
 */

using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTOScope.Runtime.UI
{
    [RequireComponent(typeof(AudioSource))]
    public class MenuBGM : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _dontDestroyOnLoad = true;
        [SerializeField] private bool _stopOnGameScene = true;
        [SerializeField] private string _gameSceneName = "main";

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_stopOnGameScene) return;
            if (scene.name == _gameSceneName)
            {
                if (_audioSource != null)
                {
                    _audioSource.Stop();
                }
            }
        }
    }
}
