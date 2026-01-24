✈️ AI Agent Instruction: Professional RTOS-Based Flight Simulator
1. Core Architecture: Strict Layer Separation
You must strictly follow the Dual-Layer Architecture. Never mix Unity dependencies into the RTOS core.

Layer 1: RTOS Core (Assets/Scripts/RTOS/)

Nature: Pure C# logic. Strictly NO using UnityEngine;.

Kernel Components:

TCB.cs: The "Ledger." Stores Task ID, State (Ready, Running, Blocked), Priority, Period, Deadline, and Memory Limits.

RTOSKernel.cs: The "Brain." A preemptive scheduler that manages TCB objects.

IRTOSTask.cs: The "Contract." Defines behavior: Initialize(), Execute(), OnPreempted(), and OnDeadlineMissed().

Utilities: PriorityQueue, TimeManager (μs precision), DeadlineManager, MemoryManager (Static Pooling).

Layer 2: Unity Runtime (Assets/Scripts/Runtime/)

Nature: The Bridge between Unity's physics engine and the RTOS.

Key Modules:

RTOSRunner.cs: Heartbeat. Triggers Kernel.Tick() within FixedUpdate().

Hardware (HAL): SensorArray (Refinement/Filtering) and FlightActuator (Force/Torque conversion).

AircraftState.cs: Shared memory for telemetry and sensor data.

2. Operational Logic & Constraints
The TCB-Logic Relationship

Kernel-Side (TCB): Holds the metadata (When and How to run). Only the Kernel modifies this.

Task-Side (IRTOSTask): Implements the Execute() logic (What to do). It should not know its own priority or deadline.

Real-time Determinism

Zero-GC Policy: No new allocations in Execute() or Tick(). Use MemoryManager for pooling.

Deadline Enforcement: If a task exceeds its TCB.DeadlineMs, DeadlineManager must trigger IRTOSTask.OnDeadlineMissed().

Inter-Process Communication (IPC): Tasks must communicate via MessageBus or AircraftState. No direct referencing between tasks.

3. Avionics Task Matrix (Implementation Reference)
Task Name	Priority	Type	Cycle	Description
HealthMonitor	0	Hard	10ms	System Watchdog & Resource Guardian
Auto-GCAS	0	Hard	10ms	Ground Collision Avoidance (Override Control)
FlightControl	1	Hard	10ms	PID-based Flight Attitude Control
FireControl	1	Hard	20ms	Weapon Release & Guidance Logic
EW / RWR	2	Hard	30ms	Electronic Warfare & Radar Warning
Navigation	2	Soft	50ms	GPS/Waypoint Pathfinding
RadarSystem	3	Soft	100ms	Periodic Environment Scanning
Fuel & Engine	4	Soft	500ms	Consumption & Mass Physics Updates
4. Coding Standards for Agent
Strict Layering: If a file is in RTOS/, do not use GameObject, Transform, or Vector3. Use custom struct or float arrays.

Interface-Driven: Always code against IRTOSTask. The Kernel should never know the concrete class of a task.

Encapsulation: Ensure TCB properties are modified only by the Kernel/Scheduler.
5.Project structure
Assets/
└── Scripts/
    ├── RTOS/                         # [Pure C#] 엔진과 분리된 순수 커널 및 로직
    │   ├── Kernel/                   # OS의 심장부 (스케줄링 및 시간 관리)
    │   │   ├── RTOSKernel.cs         # 선점형 스케줄러: 우선순위 기반 태스크 제어
    │   │   ├── TCB.cs                # [추가] Task Control Block (태스크 관리 장부)
    │   │   ├── IRTOSTask.cs          # 인터페이스: 순수 실행 로직(Execute)만 담당
    │   │   ├── PriorityQueue.cs      # 알고리즘: 우선순위에 따른 태스크 정렬
    │   │   ├── TimeManager.cs        # [정밀도] 마이크로초(μs) 단위 커널 틱 관리
    │   │   ├── TaskStatistics.cs     # [성능] 실행 시간, 지터, Deadline 위반 횟수 수집
    │   │   ├── DeadlineManager.cs    # [추가] Hard/Soft Deadline 위반 감시 및 예외 처리
    │   │   └── MemoryManager.cs      # [추가] 정적 메모리 풀 관리 (C# GC 최소화 및 자원 제한)
    │   ├── Sync/                     # 태스크 간 동기화 및 자원 보호
    │   │   ├── Mutex.cs              # 자원 잠금: 우선순위 상속으로 교착 상태 방지
    │   │   ├── Semaphore.cs          # 신호 전달: 하드웨어 이벤트 알림
    │   │   └── MessageBus.cs         # 통신망: 태스크 간 안전한 데이터 교환
    │   └── Tasks/                    # 실제 동작하는 비행 제어 및 부가 로직
    │       ├── FlightControlTask.cs  # [우선순위 1] PID 기반 기체 제어 (Hard Deadline)
    │       ├── HealthMonitor.cs      # [Watchdog] 시스템 결함 및 자원 임계치 감시
    │       └── RadarTask.cs          # 적기 탐색 및 타겟팅 (Soft Deadline)
    │
    └── Runtime/                      # [Unity] 물리 엔진 및 하드웨어 브리지
        ├── Aircraft/                 # 기체의 상태 및 데이터
        │   ├── AircraftState.cs      # [공유 메모리] 현재 비행 데이터 저장소
        │   └── AircraftView.cs       # 렌더링: 시각적 표현 담당
        ├── Hardware/                 # HAL (Hardware Abstraction Layer)
        │   ├── FlightActuator.cs     # 출력: 커널 명령을 물리적인 힘으로 변환
        │   ├── SensorArray.cs        # 입력: 물리 엔진 데이터를 RTOS용으로 정제
        │   └── PIDController.cs      # 알고리즘: 제어 공학 기반 수학 모델
        ├── Bootstrap/                # 시스템 시작점
        │   └── RTOSRunner.cs         # 하트비트: FixedUpdate를 커널 틱으로 연결
        └── UI/                       # 시각화
            └── RTOSDashboard.cs      # [추가] 메모리 사용량 및 Deadline 위반 실시간 그래프