/*
 * RTOSDashboardWebPublisher.cs - RTOS 상태를 웹으로 전송
 *
 * [역할] RTOS 커널 상태를 JSON으로 직렬화해 HTTP로 전송
 * [위치] Runtime Layer > UI (Unity MonoBehaviour)
 */

using System;
using System.Collections;
using System.Collections.Generic;
using RTOScope.RTOS.Kernel;
using UnityEngine;
using UnityEngine.Networking;

namespace RTOScope.Runtime.UI
{
    /// <summary>
    /// RTOS 상태를 로컬 웹 대시보드로 송신
    /// </summary>
    public class RTOSDashboardWebPublisher : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Bootstrap.RTOSRunner _runner;

        [Header("Network")]
        [SerializeField] private string endpointUrl = "http://localhost:8080/ingest";
        [SerializeField] private float sendIntervalSeconds = 0.1f;

        private RTOSKernel _kernel;
        private KernelStatusInfo _status;
        private float _nextSendTime;

        private void Update()
        {
            if (_runner != null && _runner.Kernel != null)
            {
                _kernel = _runner.Kernel;
                _status = _kernel.GetStatus();
            }

            if (_kernel == null) return;
            if (Time.time < _nextSendTime) return;
            _nextSendTime = Time.time + sendIntervalSeconds;

            var payload = BuildPayload();
            string json = JsonUtility.ToJson(payload);
            StartCoroutine(PostJson(json));
        }

        private RTOSDashboardPayload BuildPayload()
        {
            var payload = new RTOSDashboardPayload
            {
                state = _status.State.ToString(),
                ticks = (long)_status.TotalTicks,
                virtualTime = (float)_status.VirtualTime,
                cpuUtilization = _status.CpuUtilization,
                idleTime = (float)_status.IdleTime,
                readyTaskCount = _status.ReadyTaskCount,
                contextSwitchCount = _status.ContextSwitchCount,
                currentTaskName = _status.CurrentTaskName,
                schedulerName = _kernel.Scheduler?.Name ?? "Unknown",
                tasks = new List<RTOSDashboardTask>()
            };

            var tasks = _kernel.GetAllTasks();
            float totalExecTime = 0f;
            foreach (var tcb in tasks)
            {
                totalExecTime += tcb.TotalExecutionTime;
            }

            foreach (var tcb in tasks)
            {
                // CPU 사용량 계산: 해당 태스크의 실행시간 / 전체 실행시간 * 100
                float cpuPercent = totalExecTime > 0 
                    ? (tcb.TotalExecutionTime / totalExecTime) * 100f 
                    : 0f;

                payload.tasks.Add(new RTOSDashboardTask
                {
                    name = tcb.Task.Name,
                    state = tcb.State.ToString(),
                    priority = (int)tcb.BasePriority,
                    currentStep = tcb.Task.CurrentStep,
                    totalSteps = tcb.Task.TotalSteps,
                    periodMs = Mathf.RoundToInt(tcb.Period * 1000f),
                    cpuUsage = cpuPercent,
                    missCount = tcb.DeadlineMissCount
                });
            }

            var idleTask = _kernel.GetIdleTask();
            if (idleTask != null)
            {
                payload.idle = new RTOSDashboardTask
                {
                    name = idleTask.Task.Name,
                    state = idleTask.State.ToString(),
                    priority = 0,
                    currentStep = 0,
                    totalSteps = 0,
                    periodMs = 0
                };
            }

            return payload;
        }

        private IEnumerator PostJson(string json)
        {
            using (var request = new UnityWebRequest(endpointUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();
            }
        }

        [Serializable]
        private class RTOSDashboardPayload
        {
            public string state;
            public long ticks;
            public float virtualTime;
            public float cpuUtilization;
            public float idleTime;
            public int readyTaskCount;
            public int contextSwitchCount;
            public string currentTaskName;
            public string schedulerName;
            public List<RTOSDashboardTask> tasks;
            public RTOSDashboardTask idle;
        }

        [Serializable]
        private class RTOSDashboardTask
        {
            public string name;
            public string state;
            public int priority;
            public int currentStep;
            public int totalSteps;
            public int periodMs;
            public float cpuUsage;
            public int missCount;
        }
    }
}
