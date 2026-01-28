/*
 * ScoreManager.cs - 점수 관리 싱글톤
 *
 * [역할]
 * - 전역 점수 관리
 * - 과녁 파괴 시 점수 추가
 * - UI 업데이트 이벤트 발생
 */

using System;
using UnityEngine;

namespace RTOScope.Runtime.Game
{
    public class ScoreManager : MonoBehaviour
    {
        // 싱글톤 인스턴스
        public static ScoreManager Instance { get; private set; }

        // 점수 설정
        public const int POINTS_PER_TARGET = 50;

        // 현재 점수
        private int _score = 0;
        private int _targetsDestroyed = 0;

        // 이벤트: 점수 변경 시
        public event Action<int> OnScoreChanged;
        public event Action<int> OnTargetDestroyed;

        // Properties
        public int Score => _score;
        public int TargetsDestroyed => _targetsDestroyed;

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 과녁 파괴 시 호출 - 점수 추가
        /// </summary>
        public void AddTargetScore()
        {
            _score += POINTS_PER_TARGET;
            _targetsDestroyed++;

            Debug.Log($"[ScoreManager] 과녁 파괴! +{POINTS_PER_TARGET}점, 총 점수: {_score}");

            OnScoreChanged?.Invoke(_score);
            OnTargetDestroyed?.Invoke(_targetsDestroyed);
        }

        /// <summary>
        /// 점수 리셋
        /// </summary>
        public void ResetScore()
        {
            _score = 0;
            _targetsDestroyed = 0;
            OnScoreChanged?.Invoke(_score);
        }
    }
}
