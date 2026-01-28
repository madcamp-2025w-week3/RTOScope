/*
 * StartMenuController.cs - 시작 화면 UI 컨트롤러
 *
 * [역할]
 * - 싱글/멀티 버튼 클릭 처리
 * - 배경 이미지/타이틀/버튼과 연결
 * - BGM은 별도 MenuBGM이 담당
 */

using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTOScope.Runtime.UI
{
    public class StartMenuController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string _singleSceneName = "main";
        [SerializeField] private string _multiSceneName = "main"; // 멀티 씬 준비 전 기본값

        [Header("Menu Root (Optional)")]
        [SerializeField] private GameObject _menuRoot;

        public void OnClickSingle()
        {
            LoadScene(_singleSceneName);
        }

        public void OnClickMulti()
        {
            LoadScene(_multiSceneName);
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
    }
}
