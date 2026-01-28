using UnityEngine;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// RTOS 로그 전역 토글
    /// </summary>
    public static class RTOSDebug
    {
        public static bool EnableLogs = true;
        public static bool EnableWarnings = true;

        public static void Log(string message)
        {
            if (!EnableLogs) return;
            Debug.Log(message);
        }

        public static void LogWarning(string message)
        {
            if (!EnableWarnings) return;
            Debug.LogWarning(message);
        }
    }
}
