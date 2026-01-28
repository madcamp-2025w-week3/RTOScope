/*
 * ScoreUI.cs - ?? ?? UI
 *
 * [??]
 * - ?? ??? ??? ??/?? ??? ??
 * - ScoreManager ??? ??
 */

using RTOScope.Runtime.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTOScope.Runtime.UI
{
    public class ScoreUI : MonoBehaviour
    {
        [Header("UI ??")]
        [SerializeField] private Vector2 _position = new Vector2(10, 10);
        [SerializeField] private int _fontSize = 16;
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _shadowColor = new Color(0, 0, 0, 0.7f);

        private GUIStyle _scoreStyle;
        private GUIStyle _shadowStyle;
        private int _currentScore = 0;
        private int _targetsDestroyed = 0;
        private bool _subscribed = false;

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
            ApplyVisibilityForScene(SceneManager.GetActiveScene());
            if (enabled)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unsubscribe();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyVisibilityForScene(scene);
        }

        private void ApplyVisibilityForScene(Scene scene)
        {
            if (scene.name == "StartMenu")
            {
                gameObject.SetActive(false);
                return;
            }

            bool tutorialMode = GameSettings.Instance != null && GameSettings.Instance.TutorialMode;
            gameObject.SetActive(true);
            enabled = !tutorialMode;

            if (!tutorialMode)
            {
                Subscribe();
            }
            else
            {
                Unsubscribe();
            }
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            if (ScoreManager.Instance == null) return;
            ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
            ScoreManager.Instance.OnTargetDestroyed += OnTargetDestroyed;
            _currentScore = ScoreManager.Instance.Score;
            _targetsDestroyed = ScoreManager.Instance.TargetsDestroyed;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
                ScoreManager.Instance.OnTargetDestroyed -= OnTargetDestroyed;
            }
            _subscribed = false;
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

            float boxWidth = 110;
            float boxHeight = 38;
            float x = Screen.width - boxWidth - _position.x;
            float y = _position.y;

            GUI.Box(new Rect(x - 5, y - 3, boxWidth + 10, boxHeight + 6), "");

            string scoreText = $"SCORE: {_currentScore}";
            string targetText = $"Targets: {_targetsDestroyed}/6";

            GUI.Label(new Rect(x + 1, y + 1, boxWidth, 18), scoreText, _shadowStyle);
            GUI.Label(new Rect(x + 1, y + 20, boxWidth, 18), targetText, _shadowStyle);

            GUI.Label(new Rect(x, y, boxWidth, 18), scoreText, _scoreStyle);
            GUI.Label(new Rect(x, y + 19, boxWidth, 18), targetText, _scoreStyle);
        }

        private void InitStyles()
        {
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
