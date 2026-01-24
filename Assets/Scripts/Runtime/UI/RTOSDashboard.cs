/*
 * RTOSDashboard.cs - RTOS 상태 대시보드
 * 
 * [역할] RTOS 시스템 상태의 실시간 시각화
 * [위치] Runtime Layer > UI (Unity MonoBehaviour)
 * 
 * [표시 정보]
 * - 커널 상태, 틱 카운트
 * - 태스크별 상태, CPU 사용률
 * - 데드라인 미스 현황
 * - 메모리 사용률
 */

using RTOScope.RTOS.Kernel;
using UnityEngine;

namespace RTOScope.Runtime.UI
{
    /// <summary>
    /// RTOS 실시간 상태 대시보드
    /// </summary>
    public class RTOSDashboard : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Bootstrap.RTOSRunner _runner;

        [Header("Display Settings")]
        [SerializeField] private bool _showDashboard = true;
        [SerializeField] private Rect _windowRect = new Rect(10, 100, 300, 400);

        private RTOSKernel _kernel;
        private KernelStatusInfo _status;

        private void Update()
        {
            if (_runner != null && _runner.Kernel != null)
            {
                _kernel = _runner.Kernel;
                _status = _kernel.GetStatus();
            }
        }

        private void OnGUI()
        {
            if (!_showDashboard || _kernel == null) return;

            _windowRect = GUI.Window(0, _windowRect, DrawDashboard, "RTOS Dashboard");
        }

        private void DrawDashboard(int windowId)
        {
            GUILayout.Label($"<b>Kernel State:</b> {_status.State}");
            GUILayout.Label($"<b>Ticks:</b> {_status.TotalTicks:N0}");
            GUILayout.Label($"<b>Virtual Time:</b> {_status.VirtualTime:F3}s");
            GUILayout.Label($"<b>Tasks:</b> {_status.RegisteredTaskCount}");
            GUILayout.Label($"<b>Current Task:</b> {_status.CurrentTaskName}");
            GUILayout.Label($"<b>CPU Utilization:</b> {_status.CpuUtilization:F1}%");
            GUILayout.Label($"<b>Idle Time:</b> {_status.IdleTime:F3}s");
            GUILayout.Label($"<b>Ready Queue:</b> {_status.ReadyTaskCount} tasks");
            GUILayout.Label($"<b>Context Switches:</b> {_status.ContextSwitchCount}");

            GUILayout.Space(10);
            GUILayout.Label("<b>--- Task List ---</b>");
            // TODO: 태스크 목록 및 상태 표시

            GUILayout.Space(10);
            GUILayout.Label("<b>--- Statistics ---</b>");
            // TODO: CPU 사용률, 데드라인 미스 등 표시

            GUI.DragWindow();
        }

        /// <summary>대시보드 표시 토글</summary>
        public void ToggleDashboard()
        {
            _showDashboard = !_showDashboard;
        }
    }
}
