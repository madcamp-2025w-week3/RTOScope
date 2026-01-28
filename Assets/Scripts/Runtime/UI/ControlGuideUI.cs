/*
 * ControlGuideUI.cs - 조작법 가이드 토글 UI
 *
 * [역할]
 * - "Control" 버튼 클릭 시 조작법 패널 펼침/접기
 * - 게임 조작법 표시
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RTOScope.Runtime.UI
{
    public class ControlGuideUI : MonoBehaviour
    {
        [Header("UI 연결")]
        [Tooltip("조작법 패널 (펼쳐질 내용)")]
        [SerializeField] private GameObject _guidePanel;
        
        [Tooltip("Control 버튼의 텍스트 (선택)")]
        [SerializeField] private TMP_Text _buttonText;

        [Header("버튼 텍스트")]
        [SerializeField] private string _openText = "Control ▼";
        [SerializeField] private string _closeText = "Control ▲";

        private bool _isOpen = false;

        private void Start()
        {
            // 시작 시 패널 닫기
            if (_guidePanel != null)
            {
                _guidePanel.SetActive(false);
            }
            UpdateButtonText();
        }

        /// <summary>
        /// 버튼 클릭 시 호출 - 패널 토글
        /// </summary>
        public void ToggleGuide()
        {
            _isOpen = !_isOpen;
            
            if (_guidePanel != null)
            {
                _guidePanel.SetActive(_isOpen);
            }
            
            UpdateButtonText();
        }

        private void UpdateButtonText()
        {
            if (_buttonText != null)
            {
                _buttonText.text = _isOpen ? _closeText : _openText;
            }
        }

        /// <summary>
        /// 패널 열기
        /// </summary>
        public void OpenGuide()
        {
            _isOpen = true;
            if (_guidePanel != null)
            {
                _guidePanel.SetActive(true);
            }
            UpdateButtonText();
        }

        /// <summary>
        /// 패널 닫기
        /// </summary>
        public void CloseGuide()
        {
            _isOpen = false;
            if (_guidePanel != null)
            {
                _guidePanel.SetActive(false);
            }
            UpdateButtonText();
        }
    }
}
