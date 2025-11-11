using UnityEngine;
using MasaickerLib.ModSetting;

namespace Jump
{
    /// <summary>
    /// 跳跃配置数据持有者 - 纯数据存储，不包含业务逻辑
    /// </summary>
    public static class JumpSetting
    {
        // 基础跳跃参数
        public static float MinJumpPower { get; private set; } = 5f;
        public static float MaxJumpPower { get; private set; } = 8f;
        public static float BoostAcceleration { get; private set; } = 10f;
        public static float AccelerationDecay { get; private set; } = 0.7f;
        public static KeyCode JumpKey { get; private set; } = KeyCode.Z;
        public static bool EnableJumpLog { get; private set; } = false;

        // 空中控制参数
        public static float AirControlFactor { get; private set; } = 0.6f;
        public static float AirDragFactor { get; private set; } = 0.8f;

        // Setter方法
        public static void SetMinJumpPower(float value)
        {
            MinJumpPower = Mathf.Clamp(value, 0.5f, 15f);
            // 确保最小值不大于最大值
            if (MinJumpPower > MaxJumpPower)
            {
                MaxJumpPower = MinJumpPower;
                ModSettingAPI.SetValue("maxJumpPower", MaxJumpPower);
            }
        }

        public static void SetMaxJumpPower(float value)
        {
            MaxJumpPower = Mathf.Clamp(value, 5f, 25f);
            // 确保最大值不小于最小值
            if (MaxJumpPower < MinJumpPower)
            {
                MinJumpPower = MaxJumpPower;
                ModSettingAPI.SetValue("minJumpPower", MinJumpPower);
            }
        }

        public static void SetBoostAcceleration(float value)
        {
            BoostAcceleration = Mathf.Clamp(value, 1f, 30f);
        }

        public static void SetAccelerationDecay(float value)
        {
            AccelerationDecay = Mathf.Clamp(value, 0.1f, 0.95f);
        }

        public static void SetJumpKey(KeyCode keyCode)
        {
            JumpKey = keyCode;
        }

        public static void SetAirControlFactor(float value)
        {
            AirControlFactor = Mathf.Clamp(value, 0.1f, 1.0f);
        }

        public static void SetAirDragFactor(float value)
        {
            AirDragFactor = Mathf.Clamp(value, 0.1f, 1.0f);
        }

        public static void SetEnableJumpLog(bool enabled)
        {
            EnableJumpLog = enabled;
            JumpLogger.enableLogs = enabled;
        }

        /// <summary>
        /// 从ModSetting加载保存的配置
        /// </summary>
        public static void Init()
        {
            if (!ModSettingAPI.HasConfig())
            {
                JumpLogger.LogWhite("未发现保存的配置，使用默认值");
                return;
            }

            JumpLogger.LogWhite("发现保存的配置，开始读取...");

            if (ModSettingAPI.GetSavedValue("minJumpPower", out float minJumpPower))
            {
                SetMinJumpPower(minJumpPower);
                JumpLogger.LogWhite($"读取最小跳跃力度: {minJumpPower}");
            }

            if (ModSettingAPI.GetSavedValue("maxJumpPower", out float maxJumpPower))
            {
                SetMaxJumpPower(maxJumpPower);
                JumpLogger.LogWhite($"读取最大跳跃力度: {maxJumpPower}");
            }

            if (ModSettingAPI.GetSavedValue("boostAcceleration", out float boostAcceleration))
            {
                SetBoostAcceleration(boostAcceleration);
                JumpLogger.LogWhite($"读取初始加速度: {boostAcceleration}");
            }

            if (ModSettingAPI.GetSavedValue("accelerationDecay", out float accelerationDecay))
            {
                SetAccelerationDecay(accelerationDecay);
                JumpLogger.LogWhite($"读取加速度衰减系数: {accelerationDecay}");
            }

            if (ModSettingAPI.GetSavedValue("jumpKey", out KeyCode jumpKey))
            {
                SetJumpKey(jumpKey);
                JumpLogger.LogWhite($"读取跳跃按键: {jumpKey}");
            }

            if (ModSettingAPI.GetSavedValue("airControlFactor", out float airControlFactor))
            {
                SetAirControlFactor(airControlFactor);
                JumpLogger.LogWhite($"读取空中控制强度: {airControlFactor}");
            }

            if (ModSettingAPI.GetSavedValue("airDragFactor", out float airDragFactor))
            {
                SetAirDragFactor(airDragFactor);
                JumpLogger.LogWhite($"读取空中惯性保持: {airDragFactor}");
            }

            if (ModSettingAPI.GetSavedValue("enableJumpLog", out bool enableJumpLog))
            {
                SetEnableJumpLog(enableJumpLog);
                JumpLogger.LogWhite($"读取日志开关: {enableJumpLog}");
            }

            JumpLogger.LogWhite("配置加载完成！");
        }

        /// <summary>
        /// 重置所有配置为默认值
        /// </summary>
        public static void ResetToDefaults()
        {
            JumpLogger.LogWhite("开始重置跳跃配置为默认值...");

            // 重置为默认值
            MinJumpPower = 5f;
            MaxJumpPower = 8f;
            BoostAcceleration = 10f;
            AccelerationDecay = 0.7f;
            JumpKey = KeyCode.Z;
            EnableJumpLog = false;
            AirControlFactor = 0.6f;
            AirDragFactor = 0.8f;

            // 同步更新ModSetting的UI显示值
            ModSettingAPI.SetValue("minJumpPower", 5f);
            ModSettingAPI.SetValue("maxJumpPower", 8f);
            ModSettingAPI.SetValue("boostAcceleration", 10f);
            ModSettingAPI.SetValue("accelerationDecay", 0.7f);
            ModSettingAPI.SetValue("jumpKey", KeyCode.Z);
            ModSettingAPI.SetValue("enableJumpLog", false);
            ModSettingAPI.SetValue("airControlFactor", 0.6f);
            ModSettingAPI.SetValue("airDragFactor", 0.8f);

            // 同步到JumpLogger
            JumpLogger.enableLogs = false;

            JumpLogger.LogWhite("所有配置已重置为默认值！");
        }
    }
}
