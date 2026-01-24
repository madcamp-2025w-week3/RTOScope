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
        [SerializeField] private Rect _windowRect = new Rect(10, 100, 320, 500);

        private RTOSKernel _kernel;
        private KernelStatusInfo _status;
        private Vector2 _scrollPosition;

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
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // === Kernel Status ===
            GUILayout.Label("<b>=== Kernel Status ===</b>");
            GUILayout.Label($"State: {_status.State}");
            GUILayout.Label($"Ticks: {_status.TotalTicks:N0}");
            GUILayout.Label($"Virtual Time: {_status.VirtualTime:F3}s");
            GUILayout.Label($"CPU Utilization: {_status.CpuUtilization:F1}%");
            GUILayout.Label($"Idle Time: {_status.IdleTime:F3}s");
            GUILayout.Label($"Ready Queue: {_status.ReadyTaskCount} tasks");
            GUILayout.Label($"Context Switches: {_status.ContextSwitchCount}");

            GUILayout.Space(10);

            // === Task List ===
            GUILayout.Label("<b>=== Task List ===</b>");

            var tasks = _kernel.GetAllTasks();
            foreach (var tcb in tasks)
            {
                string stateIcon = GetStateIcon(tcb.State);
                string priorityStr = tcb.BasePriority.ToString();
                string stepInfo = $"[{tcb.Task.CurrentStep}/{tcb.Task.TotalSteps}]";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{stateIcon} {tcb.Task.Name}", GUILayout.Width(120));
                GUILayout.Label($"{priorityStr}", GUILayout.Width(60));
                GUILayout.Label($"{stepInfo}", GUILayout.Width(50));
                GUILayout.Label($"P:{tcb.Period * 1000:F0}ms", GUILayout.Width(60));
                GUILayout.EndHorizontal();
            }

            // Idle Task
            var idleTask = _kernel.GetIdleTask();
            if (idleTask != null)
            {
                string stateIcon = GetStateIcon(idleTask.State);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{stateIcon} {idleTask.Task.Name}", GUILayout.Width(120));
                GUILayout.Label("Idle", GUILayout.Width(60));
                GUILayout.Label("-", GUILayout.Width(50));
                GUILayout.Label("-", GUILayout.Width(60));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // === Statistics ===
            GUILayout.Label("<b>=== Statistics ===</b>");

            foreach (var tcb in tasks)
            {
                float execTime = tcb.TotalExecutionTime;
                int deadlineMisses = tcb.DeadlineMissCount;
                float taskCpu = _status.VirtualTime > 0 ? (execTime / _status.VirtualTime) * 100f : 0f;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{tcb.Task.Name}:", GUILayout.Width(100));
                GUILayout.Label($"CPU:{taskCpu:F1}%", GUILayout.Width(70));
                GUILayout.Label($"Miss:{deadlineMisses}", GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            GUILayout.Label($"Current Task: {_status.CurrentTaskName}");

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private string GetStateIcon(TaskState state)
        {
            switch (state)
            {
                case TaskState.Running: return "▶";
                case TaskState.Ready: return "●";
                case TaskState.Waiting: return "○";
                case TaskState.Blocked: return "■";
                case TaskState.Suspended: return "✕";
                default: return "?";
            }
        }

        /// <summary>대시보드 표시 토글</summary>
        public void ToggleDashboard()
        {
            _showDashboard = !_showDashboard;
        }
    }
}
