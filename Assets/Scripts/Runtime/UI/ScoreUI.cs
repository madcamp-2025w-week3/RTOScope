/*
 * ScoreUI.cs - 점수 표시 UI
 *
 * [역할]
 * - 화면 오른쪽 상단에 점수 표시
 * - ScoreManager 이벤트 구독하여 실시간 업데이트
 */

using RTOScope.Runtime.Game;
using UnityEngine;

namespace RTOScope.Runtime.UI
{
    public class ScoreUI : MonoBehaviour
    {
        [Header("UI 설정")]
        [SerializeField] private Vector2 _position = new Vector2(10, 10);
        [SerializeField] private int _fontSize = 16;
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _shadowColor = new Color(0, 0, 0, 0.7f);

        private GUIStyle _scoreStyle;
        private GUIStyle _shadowStyle;
        private int _currentScore = 0;
        private int _targetsDestroyed = 0;

        private void Start()
        {
            // ScoreManager 이벤트 구독
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
                ScoreManager.Instance.OnTargetDestroyed += OnTargetDestroyed;
            }
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
                ScoreManager.Instance.OnTargetDestroyed -= OnTargetDestroyed;
            }
        }

        private void OnScoreChanged(int newScore)
        {
            _currentScore = newScore;
        }

        private void OnTargetDestroyed(int count)
        {
            _targetsDestroyed = count;
        }

        private void OnGUI()
        {
            InitStyles();

            float boxWidth = 80;
            float boxHeight = 42;
            float x = Screen.width - boxWidth - _position.x;
            float y = _position.y;

            // 배경 박스
            GUI.Box(new Rect(x - 5, y - 3, boxWidth + 10, boxHeight + 6), "");

            // 점수 텍스트
            string scoreText = $"SCORE: {_currentScore}";
            string targetText = $"Targets: {_targetsDestroyed}/6";

            // 메인 텍스트
            GUI.Label(new Rect(x, y, boxWidth, 20), scoreText, _scoreStyle);
            GUI.Label(new Rect(x, y + 20, boxWidth, 22), targetText, _scoreStyle);
        }

        private void InitStyles()
        {
            // 매번 새로 생성
            _scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            _scoreStyle.normal.textColor = _textColor;

            _shadowStyle = new GUIStyle(_scoreStyle);
            _shadowStyle.normal.textColor = _shadowColor;
        }
    }
}
