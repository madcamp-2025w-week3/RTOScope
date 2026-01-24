# RTOScope

A real-time operating system (RTOS) simulator that visualizes task scheduling, priority management, and deadline behavior through an aircraft control simulation built in Unity.

---

## 1. Project Overview

### What is RTOScope?

RTOScope is an educational and demonstrative project that implements a simulated RTOS kernel in pure C#, using Unity as a visualization and hardware abstraction layer. The project models how real-time embedded systems manage concurrent tasks with strict timing constraints.

### What Problem Does It Explore?

Real-time systems—particularly those in safety-critical domains like avionics—must guarantee that tasks complete within specified deadlines. This project explores:

- How priority-based preemptive scheduling works
- What happens when tasks miss their deadlines
- How shared resources are protected in concurrent environments
- The relationship between software (RTOS) and hardware (sensors, actuators)

### Why Aircraft Simulation?

Aircraft control systems are canonical examples of hard real-time systems. A flight control loop must execute at precise intervals; failure to do so can result in loss of control. By using an aircraft as the visualization medium:

- Task priorities become intuitive (flight control > radar > logging)
- Deadline violations have visible consequences (unstable flight)
- The hardware/software boundary is clearly illustrated

This is not a flight simulator game. The aerodynamics are intentionally simplified. The focus is on demonstrating RTOS behavior, not realistic physics.

---

## 2. Design Philosophy

### Separation of Concerns

The project strictly separates three layers:

| Layer | Responsibility | Unity Dependency |
|-------|----------------|------------------|
| Hardware | Physical simulation (transforms, physics) | Yes |
| RTOS Kernel | Scheduling, timing, synchronization | No |
| Tasks | Application logic (flight control, radar) | No |

### Unity as Hardware + HAL

Unity serves two purposes:

1. **Hardware Simulation**: The aircraft GameObject represents physical hardware. Its Transform and Rigidbody simulate actuators and sensors.

2. **Hardware Abstraction Layer (HAL)**: `SensorArray` reads Unity state and writes to shared memory. `FlightActuator` reads commands from shared memory and applies them to Unity transforms.

### Pure C# RTOS Logic

All RTOS kernel code and task logic are written in pure C# without any `UnityEngine` dependencies. This design choice:

- Ensures the RTOS logic is portable and testable outside Unity
- Maintains a clear boundary between OS and hardware
- Reflects real embedded systems where OS code does not directly access hardware registers

---

## 3. System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Unity Runtime                            │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │ SensorArray │  │   Flight    │  │      RTOSRunner         │  │
│  │    (HAL)    │  │  Actuator   │  │   (Kernel Heartbeat)    │  │
│  └──────┬──────┘  └──────┬──────┘  └───────────┬─────────────┘  │
│         │                │                     │                 │
│         ▼                ▼                     │                 │
│  ┌─────────────────────────────┐               │                 │
│  │      AircraftState          │               │                 │
│  │     (Shared Memory)         │◄──────────────┘                 │
│  └─────────────┬───────────────┘                                 │
└────────────────┼─────────────────────────────────────────────────┘
                 │
┌────────────────┼─────────────────────────────────────────────────┐
│                ▼           RTOS Layer (Pure C#)                  │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                      RTOSKernel                              │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │ │
│  │  │ TimeManager │  │  Scheduler  │  │  DeadlineManager    │  │ │
│  │  └─────────────┘  └─────────────┘  └─────────────────────┘  │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│       ┌──────────────────────┼──────────────────────┐            │
│       ▼                      ▼                      ▼            │
│  ┌─────────┐           ┌─────────┐            ┌─────────┐        │
│  │  TCB    │           │  TCB    │            │  TCB    │        │
│  │ Flight  │           │ Radar   │            │ Health  │        │
│  │ Control │           │  Task   │            │ Monitor │        │
│  └─────────┘           └─────────┘            └─────────┘        │
└──────────────────────────────────────────────────────────────────┘
```

### Component Descriptions

| Component | Description |
|-----------|-------------|
| **RTOSRunner** | Unity MonoBehaviour that drives the kernel. Converts `FixedUpdate` calls into kernel ticks. |
| **AircraftState** | Shared memory structure. Tasks read sensor data and write control commands here. |
| **SensorArray** | HAL input. Reads Unity Transform/Rigidbody data and populates AircraftState. |
| **FlightActuator** | HAL output. Reads commands from AircraftState and applies them to Unity Transform. |
| **RTOSKernel** | The scheduler. Manages TCBs, selects tasks to run, tracks time, checks deadlines. |
| **TCB** | Task Control Block. Contains task state, priority, timing info, and execution statistics. |
| **IRTOSTask** | Interface that all tasks implement. Defines `Execute()`, `Initialize()`, `Cleanup()`. |

---

## 4. RTOS Kernel Features

### Implemented

| Feature | Status | Description |
|---------|--------|-------------|
| Priority-based scheduling | Skeleton | Higher priority tasks preempt lower priority tasks |
| Task Control Block (TCB) | Implemented | Stores task state, priority, period, deadline, statistics |
| Periodic task activation | Skeleton | Tasks are activated based on their period |
| Deadline tracking | Skeleton | Absolute deadlines are calculated per task instance |
| Hard/Soft deadline types | Implemented | Distinguishes between critical and non-critical deadlines |
| Execution statistics | Implemented | Tracks execution count, WCET, average execution time |
| Context switch counting | Implemented | Records number of context switches |

### Planned

| Feature | Description |
|---------|-------------|
| Rate Monotonic Scheduling (RMS) | Static priority assignment based on period |
| Earliest Deadline First (EDF) | Dynamic priority based on absolute deadline |
| Priority inheritance | Prevents priority inversion when using mutexes |
| Deadline miss handling | Configurable responses to missed deadlines |
| Jitter measurement | Statistical analysis of timing variations |

### Synchronization Primitives

| Primitive | Status | Description |
|-----------|--------|-------------|
| Mutex | Skeleton | Mutual exclusion with priority inheritance support |
| Semaphore | Skeleton | Counting semaphore for resource management |
| MessageBus | Skeleton | Inter-task communication via message queues |

---

## 5. Task Model

Tasks implement the `IRTOSTask` interface and are managed through `TCB` (Task Control Block) structures.

### IRTOSTask Interface

```csharp
public interface IRTOSTask
{
    string Name { get; }
    void Initialize();
    void Execute(float deltaTime);
    void Cleanup();
    void OnDeadlineMiss();
}
```

### Defined Tasks

| Task | Priority | Period | Deadline Type | Description |
|------|----------|--------|---------------|-------------|
| FlightControlTask | Critical (0) | 10ms | Hard | PID-based attitude control. Must never miss deadline. |
| RadarTask | High (1) | 50ms | Soft | Target detection and tracking. Degraded performance on miss. |
| HealthMonitor | Medium (2) | 100ms | Soft | System watchdog. Monitors CPU usage and deadline violations. |

### Why Tasks Are Not MonoBehaviours

In real embedded systems, tasks are managed by the RTOS scheduler, not the underlying hardware or OS. By keeping tasks as pure C# classes:

- The scheduler has full control over when tasks execute
- Tasks cannot accidentally access Unity APIs
- The code structure mirrors real embedded software
- Tasks are unit-testable without Unity

---

## 6. Unity Integration

### RTOSRunner: The Kernel Heartbeat

`RTOSRunner` is a MonoBehaviour that bridges Unity and the RTOS kernel:

```csharp
private void FixedUpdate()
{
    if (_kernel != null && _kernel.State == KernelState.Running)
    {
        _kernel.Tick(Time.fixedDeltaTime);
    }
}
```

### FixedUpdate vs Update

The project uses `FixedUpdate` (default 50Hz) rather than `Update` for more consistent timing. In a real RTOS, a hardware timer would generate interrupts at precise intervals. `FixedUpdate` provides the closest approximation in Unity.

### Simplified Physics

The aircraft physics are intentionally simplified:

- No realistic aerodynamic model
- No stall simulation
- No wind or turbulence

The goal is to demonstrate RTOS behavior, not to create a realistic flight model. The simplified physics ensure that the effects of missed deadlines or incorrect scheduling are visible without complex tuning.

---

## 7. Folder Structure

```
Assets/Scripts/
├── RTOS/                    # Pure C# - No Unity dependencies
│   ├── Kernel/              # RTOS core components
│   │   ├── RTOSKernel.cs    # Main scheduler
│   │   ├── IRTOSTask.cs     # Task interface
│   │   ├── TCB.cs           # Task Control Block
│   │   ├── PriorityQueue.cs # Ready queue implementation
│   │   ├── TimeManager.cs   # Tick and timer management
│   │   ├── TaskStatistics.cs# Execution statistics
│   │   ├── DeadlineManager.cs# Deadline monitoring
│   │   └── MemoryManager.cs # Static memory pool (planned)
│   ├── Sync/                # Synchronization primitives
│   │   ├── Mutex.cs
│   │   ├── Semaphore.cs
│   │   └── MessageBus.cs
│   └── Tasks/               # Application tasks
│       ├── FlightControlTask.cs
│       ├── RadarTask.cs
│       └── HealthMonitor.cs
│
└── Runtime/                 # Unity-dependent code
    ├── Aircraft/            # Aircraft representation
    │   ├── AircraftState.cs # Shared memory
    │   ├── AircraftView.cs  # Visual effects
    │   └── FollowPlayerX.cs # Camera controller
    ├── Hardware/            # HAL components
    │   ├── SensorArray.cs   # Input: Unity → AircraftState
    │   ├── FlightActuator.cs# Output: AircraftState → Unity
    │   ├── PIDController.cs # Control algorithm
    │   └── PlayerControllerX.cs # Manual control (debug)
    ├── Bootstrap/
    │   └── RTOSRunner.cs    # Kernel driver
    └── UI/
        └── RTOSDashboard.cs # Runtime visualization
```

### Key Separation

- **RTOS/**: Zero `using UnityEngine` statements. Portable, testable, mirrors real embedded code.
- **Runtime/**: Uses Unity APIs. Handles visualization and hardware abstraction.

---

## 8. How to Run

### Requirements

- Unity 2021.3 LTS or later (2022.3 LTS recommended)
- No additional packages required

### Setup

1. Clone the repository
2. Open the project in Unity Hub
3. Open the main scene (if not auto-loaded)
4. Enter Play mode

### What to Expect

- The aircraft will maintain level flight (controlled by RTOS tasks)
- The RTOS Dashboard displays kernel state and task statistics
- Manual control is available via keyboard (for debugging):
  - Arrow keys: Pitch and roll
  - Q/E: Yaw
  - Shift/Ctrl: Throttle

---

## 9. Project Goals and Non-Goals

### Goals

- Demonstrate RTOS scheduling concepts visually
- Show the relationship between OS, tasks, and hardware
- Provide a foundation for experimenting with scheduling algorithms
- Create portfolio-quality code demonstrating systems programming knowledge

### Non-Goals

- Realistic flight dynamics or aerodynamics
- Accurate avionics simulation
- Multiplayer or networked operation
- Production-ready RTOS implementation

### Intentional Simplifications

| Aspect | Simplification | Reason |
|--------|---------------|--------|
| Physics | Basic transform manipulation | Focus on RTOS, not physics |
| Scheduling | Single-core simulation | Clarity over complexity |
| Memory | C# garbage collection | Demonstration, not optimization |
| Timing | Unity FixedUpdate-based | No access to hardware timers |

---

## 10. Future Work

### Scheduling Improvements

- Implement Rate Monotonic Scheduling (RMS)
- Implement Earliest Deadline First (EDF)
- Add priority inheritance protocol for mutexes
- Simulate priority inversion scenarios

### Visualization Enhancements

- Real-time Gantt chart of task execution
- Deadline miss highlighting
- CPU utilization graph
- Task state transition diagram

### Advanced Features

- Multiple aircraft (multiple RTOS instances)
- Multi-core scheduling simulation
- Configurable task sets via UI
- Scenario loading (demonstrate specific scheduling problems)

### Testing

- Unit tests for kernel components
- Automated scheduling verification
- Performance benchmarks

---

## License

This project is provided for educational and portfolio purposes.

---

## Author

Developed as a demonstration of RTOS concepts and embedded systems design principles.
