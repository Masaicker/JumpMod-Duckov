using UnityEngine;

namespace Jump
{
    /// <summary>
    /// 跳跃MOD日志管理器 - 统一的日志输出控制
    /// 本小姐的代码当然要有一致的日志风格！( ´-ω-` )
    /// </summary>
    public static class JumpLogger
    {
        [Header("日志控制")]
        public static bool enableLogs = false;           // 是否启用日志输出

        /// <summary>
        /// 白色日志 - 普通信息
        /// </summary>
        public static void LogWhite(string message)
        {
            if (enableLogs)
            {
                Debug.Log($"[JumpMod] {message}");
            }
        }

        /// <summary>
        /// 黄色日志 - 警告信息
        /// </summary>
        public static void LogYellow(string message)
        {
            if (enableLogs)
            {
                Debug.LogWarning($"[JumpMod] {message}");
            }
        }

        /// <summary>
        /// 红色日志 - 错误信息
        /// </summary>
        public static void LogRed(string message)
        {
            if (enableLogs)
            {
                Debug.LogError($"[JumpMod] {message}");
            }
        }
    }
}