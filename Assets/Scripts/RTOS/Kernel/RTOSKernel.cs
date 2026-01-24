/*
 * ============================================================================
 * RTOSKernel.cs
 * ============================================================================
 * 
 * [모듈 역할]
 * RTOS의 메인 커널 - 스케줄러의 심장부
 * 우선순위 기반 선점형(Preemptive) 스케줄링을 관리
 * 
 * [아키텍처 위치]
 * RTOS Layer > Kernel > RTOSKernel
 * - 순수 C# 코드로 작성 (Unity API 사용 금지)
 * - RTOSRunner(Unity MonoBehaviour)에 의해 호출됨
 * 
 * [주요 책임]
 * - TCB(Task Control Block) 관리
 * - 우선순위 기반 스케줄링
 * - 컨텍스트 스위칭 시뮬레이션
 * - 시스템 틱 처리
 * 
 * [미구현 사항]
 * - 실제 스케줄링 알고리즘 (Rate Monotonic, EDF 등)
 * - 선점형 스케줄링 로직
 * - 태스크 상태 전이
 * ============================================================================
 */

using System;
using System.Collections.Generic;

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
    /// 모든 태스크의 스케줄링과 실행을 관리한다.
    /// </summary>
    public class RTOSKernel
    {
        // =====================================================================
        // 필드
        // =====================================================================

        private KernelState _state;
        private readonly List<TCB> _registeredTasks;  // TCB로 변경
        private readonly PriorityQueue _readyQueue;
        private readonly TimeManager _timeManager;
        private readonly DeadlineManager _deadlineManager;
        private readonly TaskStatistics _statistics;

        private TCB _currentTcb;  // 현재 실행 중인 TCB
        private ulong _totalTicks;

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

        // =====================================================================
        // 생성자
        // =====================================================================

        public RTOSKernel()
        {
            _state = KernelState.Stopped;
            _registeredTasks = new List<TCB>();
            _readyQueue = new PriorityQueue();
            _timeManager = new TimeManager();
            _deadlineManager = new DeadlineManager();
            _statistics = new TaskStatistics();
            _totalTicks = 0;
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================

        /// <summary>
        /// RTOS 커널을 시작한다.
        /// </summary>
        public void Start()
        {
            // TODO: 커널 초기화 로직 구현
            // - 모든 등록된 태스크를 Ready 상태로 전환
            // - 첫 번째 태스크 선택
            // - 타이머 초기화

            foreach (var tcb in _registeredTasks)
            {
                tcb.Task.Initialize();
                tcb.SetState(TaskState.Ready);
            }

            _state = KernelState.Running;
        }

        /// <summary>
        /// RTOS 커널을 정지한다.
        /// </summary>
        public void Stop()
        {
            // 모든 태스크 정리
            foreach (var tcb in _registeredTasks)
            {
                tcb.Task.Cleanup();
                tcb.SetState(TaskState.Suspended);
            }

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

            // TODO: 중복 등록 검사

            var tcb = new TCB(task, priority, period, deadline, deadlineType);
            _registeredTasks.Add(tcb);
        }

        /// <summary>
        /// 시스템 틱을 처리한다. 매 프레임 RTOSRunner에서 호출됨.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_state != KernelState.Running)
                return;

            _totalTicks++;

            // 1. 시간 관리자 업데이트
            _timeManager.Update(deltaTime);

            // 2. 주기적 태스크 활성화 검사
            CheckPeriodicTasks();

            // 3. 스케줄링 수행
            ScheduleNextTask();

            // 4. 현재 태스크 실행
            if (_currentTcb != null)
            {
                ExecuteCurrentTask(deltaTime);
            }

            // 5. 데드라인 검사
            CheckDeadlines();

            // 6. 통계 업데이트
            _statistics.UpdateSystemTime(deltaTime);
        }

        /// <summary>
        /// 모든 TCB 목록 반환 (UI용)
        /// </summary>
        public IReadOnlyList<TCB> GetAllTasks() => _registeredTasks;

        // =====================================================================
        // 비공개 메서드
        // =====================================================================

        private void CheckPeriodicTasks()
        {
            // TODO: 주기적 태스크 활성화 로직 구현
            float currentTime = _timeManager.CurrentTime;

            foreach (var tcb in _registeredTasks)
            {
                if (tcb.Period > 0 && currentTime >= tcb.NextActivationTime)
                {
                    tcb.Activate(currentTime);
                }
            }
        }

        private void ScheduleNextTask()
        {
            // TODO: 선점형 스케줄링 로직 구현
            // - Rate Monotonic Scheduling (RMS)
            // - Earliest Deadline First (EDF)
            // - 우선순위 역전 방지

            // 현재는 단순히 가장 높은 우선순위의 Ready 태스크 선택
            TCB highestPriorityTcb = null;

            foreach (var tcb in _registeredTasks)
            {
                if (tcb.State == TaskState.Ready)
                {
                    if (highestPriorityTcb == null ||
                        tcb.CurrentPriority < highestPriorityTcb.CurrentPriority)
                    {
                        highestPriorityTcb = tcb;
                    }
                }
            }

            if (highestPriorityTcb != null)
            {
                // 컨텍스트 스위칭
                if (_currentTcb != highestPriorityTcb)
                {
                    if (_currentTcb != null && _currentTcb.State == TaskState.Running)
                    {
                        _currentTcb.SetState(TaskState.Ready);  // 선점됨
                    }
                    _currentTcb = highestPriorityTcb;
                    _statistics.RecordContextSwitch();
                }
                _currentTcb.SetState(TaskState.Running);
            }
        }

        private void ExecuteCurrentTask(float deltaTime)
        {
            if (_currentTcb == null) return;

            float startTime = _timeManager.CurrentTime;
            _currentTcb.RecordExecutionStart(startTime);

            // 태스크 실행
            _currentTcb.Task.Execute(deltaTime);

            // 실행 시간 기록
            float executionTime = _timeManager.CurrentTime - startTime;
            _currentTcb.RecordExecutionComplete(executionTime);
            _statistics.RecordExecution(_currentTcb, executionTime);
        }

        private void CheckDeadlines()
        {
            // TODO: 데드라인 검사 로직 개선
            float currentTime = _timeManager.CurrentTime;

            foreach (var tcb in _registeredTasks)
            {
                if (tcb.State == TaskState.Running &&
                    tcb.AbsoluteDeadline > 0 &&
                    currentTime > tcb.AbsoluteDeadline)
                {
                    tcb.RecordDeadlineMiss();
                    _deadlineManager.RecordDeadlineMiss(tcb, tcb.AbsoluteDeadline, currentTime);
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
                SystemTime = _timeManager.CurrentTime
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
        public float SystemTime;
    }
}
