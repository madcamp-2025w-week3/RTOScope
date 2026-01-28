/*
 * GameSettings.cs - 게임 설정 싱글톤 (씬 간 데이터 유지)
 *
 * [역할]
 * - 시작 메뉴에서 선택한 스케줄러 타입 저장
 * - 씬 전환 후에도 설정 유지 (DontDestroyOnLoad)
 */

using RTOScope.RTOS.Kernel;
using UnityEngine;

namespace RTOScope.Runtime.Game
{
    public class GameSettings : MonoBehaviour
    {
        // 싱글톤 인스턴스
        public static GameSettings Instance { get; private set; }

        // 선택된 스케줄러 타입
        public SchedulerType SelectedScheduler { get; set; } = SchedulerType.Priority;

        private void Awake()
        {
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
        /// 스케줄러 타입을 인덱스로 설정 (드롭다운용)
        /// </summary>
        public void SetSchedulerByIndex(int index)
        {
            SelectedScheduler = (SchedulerType)index;
            Debug.Log($"[GameSettings] 스케줄러 선택: {SelectedScheduler}");
        }
    }
}
