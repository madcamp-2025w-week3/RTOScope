/*
 * ============================================================================
 * RTOSKernel.cs
 * ============================================================================
 *
 * [모듈 역할]
 * RTOS의 메인 커널 - 스케줄러의 심장부
 * 상태 머신 기반 선점형(Preemptive) 스케줄링을 관리
 *
 * [아키텍처 위치]
 * RTOS Layer > Kernel > RTOSKernel
 * - 순수 C# 코드로 작성 (Unity API 사용 금지)
 * - RTOSRunner(Unity MonoBehaviour)에 의해 호출됨
 *
 * [스케줄링 방식]
 * - 각 태스크는 상태 머신(State Machine)으로 구현
 * - 커널은 매 Step 실행 후 선점 여부를 판단
 * - 가상 시간(Virtual Time) 기반으로 정확한 타이밍 시뮬레이션
 * ============================================================================
 */

using System;
using System.Collections.Generic;
using RTOScope.RTOS.Tasks;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// RTOS 커널의 상태를 나타내는 열거형
    /// </summary>
    public enum KernelState
    {
        Stopped,    // 커널 정지 상태
        Running,    // 커널 실행 중
        Suspended   // 커널 일시 정지
    }

    /// <summary>
    /// RTOS 메인 커널 클래스
    /// 상태 머신 기반 선점형 스케줄링을 수행한다.
    /// </summary>
    public class RTOSKernel
    {
        // =====================================================================
        // 필드
        // =====================================================================

        private KernelState _state;
        private readonly List<TCB> _registeredTasks;
        private readonly TimeManager _timeManager;
        private readonly DeadlineManager _deadlineManager;
        private readonly TaskStatistics _statistics;

        private TCB _currentTcb;          // 현재 실행 중인 TCB
        private TCB _idleTaskTcb;         // Idle 태스크 TCB (항상 존재)
        private ulong _totalTicks;
        private float _virtualTime;       // 가상 시간 (시뮬레이션 시간)
        private float _totalIdleTime;     // 총 Idle 시간

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public KernelState State => _state;
        public TCB CurrentTcb => _currentTcb;
        public string CurrentTaskName => _currentTcb?.Task.Name ?? "None";
        public ulong TotalTicks => _totalTicks;
        public int RegisteredTaskCount => _registeredTasks.Count;
        public TimeManager TimeManager => _timeManager;
        public TaskStatistics Statistics => _statistics;
        public float VirtualTime => _virtualTime;
        public float TotalIdleTime => _totalIdleTime;

        // =====================================================================
        // 생성자
        // =====================================================================

        public RTOSKernel()
        {
            _state = KernelState.Stopped;
            _registeredTasks = new List<TCB>();
            _timeManager = new TimeManager();
            _deadlineManager = new DeadlineManager();
            _statistics = new TaskStatistics();
            _totalTicks = 0;
            _virtualTime = 0f;
            _totalIdleTime = 0f;
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================

        /// <summary>
        /// RTOS 커널을 시작한다.
        /// </summary>
        public void Start()
        {
            // 1. Idle 태스크 생성 및 등록 (가장 먼저)
            var idleTask = new IdleTask();
            _idleTaskTcb = new TCB(idleTask, TaskPriority.Idle, 0f, 0f, DeadlineType.None);
            _idleTaskTcb.Task.Initialize();
            _idleTaskTcb.SetState(TaskState.Ready);

            // 2. 모든 등록된 태스크 초기화
            // 주기적 태스크는 Waiting 상태로 시작하고, 첫 틱에서 활성화됨
            foreach (var tcb in _registeredTasks)
            {
                tcb.Task.Initialize();
                if (tcb.Period > 0)
                {
                    // 주기적 태스크: Waiting 상태로 시작 (NextActivationTime = 0이므로 즉시 활성화)
                    tcb.SetState(TaskState.Waiting);
                }
                else
                {
                    // 비주기적 태스크: Ready 상태로 시작
                    tcb.SetState(TaskState.Ready);
                }
            }

            _state = KernelState.Running;
        }

        /// <summary>
        /// RTOS 커널을 정지한다.
        /// </summary>
        public void Stop()
        {
            foreach (var tcb in _registeredTasks)
            {
                tcb.Task.Cleanup();
                tcb.SetState(TaskState.Suspended);
            }

            _idleTaskTcb?.Task.Cleanup();
            _state = KernelState.Stopped;
        }

        /// <summary>
        /// 태스크를 커널에 등록한다 (TCB 생성).
        /// </summary>
        public void RegisterTask(
            IRTOSTask task,
            TaskPriority priority,
            float period = 0f,
            float deadline = 0f,
            DeadlineType deadlineType = DeadlineType.Soft)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            var tcb = new TCB(task, priority, period, deadline, deadlineType);
            _registeredTasks.Add(tcb);
        }

        /// <summary>
        /// 시스템 틱을 처리한다.
        /// 가상 시간 기반 이산 이벤트 시뮬레이션.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_state != KernelState.Running)
                return;

            _totalTicks++;
            _timeManager.Update(deltaTime);

            // 이 틱에서 사용할 수 있는 가상 시간 예산
            float virtualBudget = deltaTime;

            // 가상 시간 예산이 남아있는 동안 태스크 실행
            while (virtualBudget > 0.00001f) // 부동소수점 오차 고려
            {
                // 1. 주기적 태스크 활성화 검사
                ActivatePeriodicTasks();

                // 2. 스케줄링 (가장 높은 우선순위 Ready 태스크 선택)
                TCB nextTcb = Schedule();

                // 3. 선점 처리
                if (_currentTcb != null && nextTcb != _currentTcb)
                {
                    if (_currentTcb.State == TaskState.Running)
                    {
                        // 현재 태스크가 선점됨
                        _currentTcb.SetState(TaskState.Ready);
                        _statistics.RecordContextSwitch();
                    }
                }

                _currentTcb = nextTcb;

                if (_currentTcb == null)
                {
                    // Ready 태스크가 없으면 Idle 실행
                    _currentTcb = _idleTaskTcb;
                }

                _currentTcb.SetState(TaskState.Running);

                // 4. 현재 Step의 WCET 가져오기
                float stepWCET = _currentTcb.Task.CurrentStepWCET;

                // 남은 예산과 비교하여 실제 실행 시간 결정
                float execTime = Math.Min(stepWCET, virtualBudget);

                // 5. 태스크의 한 Step 실행
                _currentTcb.RecordExecutionStart(_virtualTime);
                _currentTcb.Task.ExecuteStep();
                _currentTcb.RecordExecutionComplete(execTime);

                // 6. 통계 기록
                if (_currentTcb == _idleTaskTcb)
                {
                    _totalIdleTime += execTime;
                }
                else
                {
                    _statistics.RecordExecution(_currentTcb, execTime);
                }

                // 7. 가상 시간 전진
                _virtualTime += execTime;
                virtualBudget -= execTime;

                // 8. 태스크 완료 체크
                if (_currentTcb.Task.IsWorkComplete)
                {
                    if (_currentTcb == _idleTaskTcb)
                    {
                        // Idle은 즉시 리셋
                        _currentTcb.Task.ResetForNextPeriod();
                    }
                    else
                    {
                        // 일반 태스크는 Waiting 상태로 전환
                        _currentTcb.SetState(TaskState.Waiting);
                        _currentTcb.Task.ResetForNextPeriod();
                        _currentTcb = null;
                    }
                }

                // 9. 데드라인 검사
                CheckDeadlines();
            }

            // 통계 시간 업데이트
            _statistics.UpdateSystemTime(deltaTime);
        }

        /// <summary>
        /// 모든 TCB 목록 반환 (UI용)
        /// </summary>
        public IReadOnlyList<TCB> GetAllTasks() => _registeredTasks;

        /// <summary>
        /// Idle TCB 반환
        /// </summary>
        public TCB GetIdleTask() => _idleTaskTcb;

        // =====================================================================
        // 비공개 메서드
        // =====================================================================

        private void ActivatePeriodicTasks()
        {
            foreach (var tcb in _registeredTasks)
            {
                // 주기적 태스크이고, 활성화 시간이 되었고, Waiting 상태인 경우
                if (tcb.Period > 0 &&
                    _virtualTime >= tcb.NextActivationTime &&
                    tcb.State == TaskState.Waiting)
                {
                    tcb.Activate(_virtualTime);
                    tcb.Task.ResetForNextPeriod();
                }
            }
        }

        private TCB Schedule()
        {
            // 가장 높은 우선순위(낮은 숫자)의 Ready 태스크 선택
            TCB highestPriorityTcb = null;

            foreach (var tcb in _registeredTasks)
            {
                if (tcb.State == TaskState.Ready || tcb.State == TaskState.Running)
                {
                    if (highestPriorityTcb == null ||
                        tcb.CurrentPriority < highestPriorityTcb.CurrentPriority)
                    {
                        highestPriorityTcb = tcb;
                    }
                }
            }

            return highestPriorityTcb; // null이면 Idle이 실행됨
        }

        private void CheckDeadlines()
        {
            foreach (var tcb in _registeredTasks)
            {
                // Running 또는 Ready 상태이고 데드라인이 지난 경우
                if ((tcb.State == TaskState.Running || tcb.State == TaskState.Ready) &&
                    tcb.AbsoluteDeadline > 0 &&
                    _virtualTime > tcb.AbsoluteDeadline)
                {
                    tcb.RecordDeadlineMiss();
                    _deadlineManager.RecordDeadlineMiss(tcb, tcb.AbsoluteDeadline, _virtualTime);

                    // 데드라인 미스 후 처리: 다음 주기로 넘어감
                    tcb.SetState(TaskState.Waiting);
                    tcb.Task.ResetForNextPeriod();
                }
            }
        }

        /// <summary>
        /// 커널 상태 정보를 반환한다.
        /// </summary>
        public KernelStatusInfo GetStatus()
        {
            return new KernelStatusInfo
            {
                State = _state,
                TotalTicks = _totalTicks,
                RegisteredTaskCount = _registeredTasks.Count,
                CurrentTaskName = _currentTcb?.Task.Name ?? "None",
                VirtualTime = _virtualTime,
                IdleTime = _totalIdleTime,
                CpuUtilization = _virtualTime > 0 ? (1f - _totalIdleTime / _virtualTime) * 100f : 0f
            };
        }
    }

    /// <summary>
    /// 커널 상태 정보를 담는 구조체
    /// </summary>
    public struct KernelStatusInfo
    {
        public KernelState State;
        public ulong TotalTicks;
        public int RegisteredTaskCount;
        public string CurrentTaskName;
        public float VirtualTime;
        public float IdleTime;
        public float CpuUtilization;
    }
}
