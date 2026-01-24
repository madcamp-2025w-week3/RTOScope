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
 * - ReadyList: FreeRTOS 스타일 우선순위별 연결 리스트
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
    /// ReadyList 기반 선점형 스케줄링을 수행한다.
    /// </summary>
    public class RTOSKernel
    {
        // =====================================================================
        // 필드
        // =====================================================================

        private KernelState _state;
        private readonly List<TCB> _allTasks;         // 모든 등록된 태스크 (관리용)
        private readonly ReadyList _readyList;        // Ready 상태 태스크들
        private readonly TimeManager _timeManager;
        private readonly DeadlineManager _deadlineManager;
        private readonly TaskStatistics _statistics;

        private TCB _currentTcb;          // 현재 실행 중인 TCB (Running 상태)
        private TCB _idleTaskTcb;         // Idle 태스크 TCB (항상 Ready)
        private ulong _totalTicks;
        private float _virtualTime;       // 가상 시간 (시뮬레이션 시간)
        private float _totalIdleTime;     // 총 Idle 시간
        private int _contextSwitchCount;  // 컨텍스트 스위칭 횟수

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public KernelState State => _state;
        public TCB CurrentTcb => _currentTcb;
        public string CurrentTaskName => _currentTcb?.Task.Name ?? "Idle";
        public ulong TotalTicks => _totalTicks;
        public int RegisteredTaskCount => _allTasks.Count;
        public TimeManager TimeManager => _timeManager;
        public TaskStatistics Statistics => _statistics;
        public float VirtualTime => _virtualTime;
        public float TotalIdleTime => _totalIdleTime;
        public ReadyList ReadyList => _readyList;
        public int ContextSwitchCount => _contextSwitchCount;

        // =====================================================================
        // 생성자
        // =====================================================================

        public RTOSKernel()
        {
            _state = KernelState.Stopped;
            _allTasks = new List<TCB>();
            _readyList = new ReadyList();
            _timeManager = new TimeManager();
            _deadlineManager = new DeadlineManager();
            _statistics = new TaskStatistics();
            _totalTicks = 0;
            _virtualTime = 0f;
            _totalIdleTime = 0f;
            _contextSwitchCount = 0;
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================

        /// <summary>
        /// RTOS 커널을 시작한다.
        /// </summary>
        public void Start()
        {
            // 1. Idle 태스크 생성 (가장 먼저)
            var idleTask = new IdleTask();
            _idleTaskTcb = new TCB(idleTask, TaskPriority.Idle, 0f, 0f, DeadlineType.None);
            _idleTaskTcb.Task.Initialize();
            _idleTaskTcb.SetState(TaskState.Ready);
            // Idle은 ReadyList에 추가하지 않음 (특별 관리)

            // 2. 모든 등록된 태스크 초기화
            foreach (var tcb in _allTasks)
            {
                tcb.Task.Initialize();

                if (tcb.Period > 0)
                {
                    // 주기적 태스크: Waiting 상태로 시작
                    // NextActivationTime = 0이므로 첫 틱에서 활성화됨
                    tcb.SetState(TaskState.Waiting);
                }
                else
                {
                    // 비주기적 태스크: 즉시 Ready 상태로 ReadyList에 추가
                    tcb.SetState(TaskState.Ready);
                    _readyList.Add(tcb);
                }
            }

            _state = KernelState.Running;
        }

        /// <summary>
        /// RTOS 커널을 정지한다.
        /// </summary>
        public void Stop()
        {
            // 현재 태스크가 있으면 정리
            if (_currentTcb != null && _currentTcb != _idleTaskTcb)
            {
                _currentTcb.SetState(TaskState.Suspended);
            }
            _currentTcb = null;

            // 모든 태스크 정리
            foreach (var tcb in _allTasks)
            {
                tcb.Task.Cleanup();
                tcb.SetState(TaskState.Suspended);
            }

            // ReadyList 비우기
            _readyList.Clear();

            _idleTaskTcb?.Task.Cleanup();
            _state = KernelState.Stopped;
        }

        /// <summary>
        /// 태스크를 커널에 등록한다 (TCB 생성).
        /// Start() 호출 전에 등록해야 함.
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
            _allTasks.Add(tcb);
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
                // 1. 주기적 태스크 활성화 검사 (Waiting -> Ready)
                ActivatePeriodicTasks();

                // 2. 스케줄링 (ReadyList에서 최고 우선순위 선택)
                TCB nextTcb = Schedule();

                // 3. 컨텍스트 스위칭 처리
                if (nextTcb != _currentTcb)
                {
                    ContextSwitch(nextTcb);
                }

                // 4. 현재 태스크 없으면 Idle 실행
                if (_currentTcb == null)
                {
                    _currentTcb = _idleTaskTcb;
                }

                // 5. 현재 Step의 WCET 가져오기
                float stepWCET = _currentTcb.Task.CurrentStepWCET;
                float execTime = Math.Min(stepWCET, virtualBudget);

                // 6. 태스크 한 Step 실행
                _currentTcb.RecordExecutionStart(_virtualTime);
                _currentTcb.Task.ExecuteStep();
                _currentTcb.RecordExecutionComplete(execTime);

                // 7. 통계 기록
                if (_currentTcb == _idleTaskTcb)
                {
                    _totalIdleTime += execTime;
                }
                else
                {
                    _statistics.RecordExecution(_currentTcb, execTime);
                }

                // 8. 가상 시간 전진
                _virtualTime += execTime;
                virtualBudget -= execTime;

                // 9. 태스크 완료 처리
                if (_currentTcb.Task.IsWorkComplete)
                {
                    HandleTaskCompletion();
                }

                // 10. 데드라인 검사
                CheckDeadlines();
            }

            // 통계 시간 업데이트
            _statistics.UpdateSystemTime(deltaTime);
        }

        /// <summary>
        /// 모든 TCB 목록 반환 (UI용)
        /// </summary>
        public IReadOnlyList<TCB> GetAllTasks() => _allTasks;

        /// <summary>
        /// Idle TCB 반환
        /// </summary>
        public TCB GetIdleTask() => _idleTaskTcb;

        // =====================================================================
        // 비공개 메서드 - 스케줄링
        // =====================================================================

        /// <summary>
        /// 주기적 태스크 활성화 (Waiting -> Ready)
        /// 가상 시간이 NextActivationTime에 도달하면 Ready로 전환
        /// </summary>
        private void ActivatePeriodicTasks()
        {
            foreach (var tcb in _allTasks)
            {
                if (tcb.Period > 0 &&
                    tcb.State == TaskState.Waiting &&
                    _virtualTime >= tcb.NextActivationTime)
                {
                    // 태스크 활성화
                    tcb.Activate(_virtualTime);
                    tcb.Task.ResetForNextPeriod();

                    // ReadyList에 추가
                    _readyList.Add(tcb);
                }
            }
        }

        /// <summary>
        /// 스케줄링: ReadyList에서 가장 높은 우선순위 태스크 선택
        /// </summary>
        private TCB Schedule()
        {
            // ReadyList에서 최고 우선순위 태스크 확인 (제거 안함)
            TCB highest = _readyList.PeekHighest();

            if (highest == null)
            {
                // Ready 태스크 없음 -> Idle 반환
                return null;
            }

            // 현재 실행 중인 태스크와 비교
            if (_currentTcb != null &&
                _currentTcb != _idleTaskTcb &&
                _currentTcb.State == TaskState.Running)
            {
                // 현재 태스크보다 높은 우선순위면 선점
                if (highest.CurrentPriority < _currentTcb.CurrentPriority)
                {
                    return highest;
                }
                // 동일 또는 낮은 우선순위면 현재 태스크 계속
                return _currentTcb;
            }

            return highest;
        }

        /// <summary>
        /// 컨텍스트 스위칭 수행
        /// </summary>
        private void ContextSwitch(TCB nextTcb)
        {
            // 이전 태스크 처리
            if (_currentTcb != null && _currentTcb != _idleTaskTcb)
            {
                if (_currentTcb.State == TaskState.Running)
                {
                    // 선점된 태스크를 ReadyList에 다시 추가
                    _currentTcb.SetState(TaskState.Ready);
                    _readyList.Add(_currentTcb);
                }
            }

            // 새 태스크로 전환
            if (nextTcb != null && nextTcb != _idleTaskTcb)
            {
                // ReadyList에서 제거
                _readyList.Remove(nextTcb);
                nextTcb.SetState(TaskState.Running);
            }

            _currentTcb = nextTcb;
            _contextSwitchCount++;
            _statistics.RecordContextSwitch();
        }

        /// <summary>
        /// 태스크 완료 처리
        /// </summary>
        private void HandleTaskCompletion()
        {
            if (_currentTcb == _idleTaskTcb)
            {
                // Idle은 즉시 리셋 (항상 Ready)
                _currentTcb.Task.ResetForNextPeriod();
            }
            else
            {
                // 주기적 태스크: Waiting 상태로 전환
                _currentTcb.SetState(TaskState.Waiting);
                _currentTcb.Task.ResetForNextPeriod();
                _currentTcb = null;
            }
        }

        /// <summary>
        /// 데드라인 검사
        /// </summary>
        private void CheckDeadlines()
        {
            foreach (var tcb in _allTasks)
            {
                // Ready 또는 Running 상태이고 데드라인이 지난 경우
                if ((tcb.State == TaskState.Running || tcb.State == TaskState.Ready) &&
                    tcb.AbsoluteDeadline > 0 &&
                    _virtualTime > tcb.AbsoluteDeadline)
                {
                    // 데드라인 미스 기록
                    tcb.RecordDeadlineMiss();
                    _deadlineManager.RecordDeadlineMiss(tcb, tcb.AbsoluteDeadline, _virtualTime);

                    // ReadyList에서 제거
                    if (tcb.State == TaskState.Ready)
                    {
                        _readyList.Remove(tcb);
                    }

                    // Running 상태였다면 현재 태스크 클리어
                    if (tcb == _currentTcb)
                    {
                        _currentTcb = null;
                    }

                    // Waiting 상태로 전환 (다음 주기 대기)
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
                RegisteredTaskCount = _allTasks.Count,
                CurrentTaskName = _currentTcb?.Task.Name ?? "Idle",
                VirtualTime = _virtualTime,
                IdleTime = _totalIdleTime,
                CpuUtilization = _virtualTime > 0 ? (1f - _totalIdleTime / _virtualTime) * 100f : 0f,
                ReadyTaskCount = _readyList.Count,
                ContextSwitchCount = _contextSwitchCount
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
        public int ReadyTaskCount;
        public int ContextSwitchCount;
    }
}
