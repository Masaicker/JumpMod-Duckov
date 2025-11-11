using System;
using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;
using MasaickerLib.ModSetting;

namespace Jump
{
    /// <summary>
    /// 跳跃MOD配置管理器 - 处理所有 ModSetting配置
    /// </summary>
    public class JumpConfigManager
    {
        private readonly ModInfo modInfo;

        // 配置数据
        public float MinJumpPower { get; private set; } = 5f;
        public float MaxJumpPower { get; private set; } = 8f;
        public float BoostAcceleration { get; private set; } = 10f;
        public float AccelerationDecay { get; private set; } = 0.7f;
        public KeyCode JumpKey { get; private set; } = KeyCode.Z;
        public bool EnableJumpLog { get; private set; } = false;

        // 空中控制参数
        public float AirControlFactor { get; private set; } = 0.6f;      // 空中控制力系数 
        public float AirDragFactor { get; private set; } = 0.8f;       // 空气阻力系数 

        public JumpConfigManager(ModInfo modInfo)
        {
            this.modInfo = modInfo;
        }

        /// <summary>
        /// 设置ModSetting配置
        /// </summary>
        public void SetupConfiguration()
        {
            // 先加载保存的配置
            LoadSavedConfiguration();

            // 跳跃参数滑块
            ModSettingAPI.AddSlider("minJumpPower", "最小跳跃力度 Min Jump Power", MinJumpPower,
                new Vector2(0.5f, 15f), OnMinJumpPowerChanged, decimalPlaces: 1);

            ModSettingAPI.AddSlider("maxJumpPower", "最大跳跃力度 Max Jump Power", MaxJumpPower,
                new Vector2(5f, 25f), OnMaxJumpPowerChanged, decimalPlaces: 1);

            ModSettingAPI.AddSlider("boostAcceleration", "初始加速度 Initial Acceleration", BoostAcceleration,
                new Vector2(1f, 30f), OnBoostAccelerationChanged, decimalPlaces: 1);

            ModSettingAPI.AddSlider("accelerationDecay", "加速度衰减系数 Acceleration Decay", AccelerationDecay,
                new Vector2(0.1f, 0.95f), OnAccelerationDecayChanged, decimalPlaces: 2);

            // 按键绑定
            ModSettingAPI.AddKeybinding("jumpKey", "跳跃按键 Jump Key", JumpKey, KeyCode.Z, OnJumpKeyChanged);

            // 空中控制参数
            ModSettingAPI.AddSlider("airControlFactor", "空中控制强度 Air Control Strength", AirControlFactor,
                new Vector2(0.1f, 1.0f), OnAirControlFactorChanged, decimalPlaces: 2);

            ModSettingAPI.AddSlider("airDragFactor", "空中惯性保持 Air Inertial Retention", AirDragFactor,
                new Vector2(0.1f, 1.0f), OnAirDragFactorChanged, decimalPlaces: 3);

            // 日志开关
            ModSettingAPI.AddToggle("enableJumpLog", "启用跳跃日志 Enable Jump Log", EnableJumpLog, OnEnableJumpLogChanged);

            // 重置按钮
            ModSettingAPI.AddButton("resetConfig", "", "重置所有配置 Reset All", OnResetConfigClicked);
        }

        /// <summary>
        /// 加载保存的配置
        /// </summary>
        private void LoadSavedConfiguration()
        {
            JumpLogger.LogWhite("开始加载跳跃配置...");

            // 检查是否有保存的配置
            if (ModSettingAPI.HasConfig())
            {
                JumpLogger.LogWhite("发现保存的配置，开始读取...");

                // 使用GetSavedValue读取已保存的配置
                if (ModSettingAPI.GetSavedValue("minJumpPower", out float minJumpPower))
                {
                    OnMinJumpPowerChanged(minJumpPower);
                    JumpLogger.LogWhite($"读取最小跳跃力度: {minJumpPower}");
                }

                if (ModSettingAPI.GetSavedValue("maxJumpPower", out float maxJumpPower))
                {
                    OnMaxJumpPowerChanged(maxJumpPower);
                    JumpLogger.LogWhite($"读取最大跳跃力度: {maxJumpPower}");
                }

                if (ModSettingAPI.GetSavedValue("boostAcceleration", out float boostAcceleration))
                {
                    OnBoostAccelerationChanged(boostAcceleration);
                    JumpLogger.LogWhite($"读取初始加速度: {boostAcceleration}");
                }

                if (ModSettingAPI.GetSavedValue("accelerationDecay", out float accelerationDecay))
                {
                    OnAccelerationDecayChanged(accelerationDecay);
                    JumpLogger.LogWhite($"读取加速度衰减系数: {accelerationDecay}");
                }

                if (ModSettingAPI.GetSavedValue("jumpKey", out KeyCode jumpKey))
                {
                    OnJumpKeyChanged(jumpKey);
                    JumpLogger.LogWhite($"读取跳跃按键: {jumpKey}");
                }

                if (ModSettingAPI.GetSavedValue("airControlFactor", out float airControlFactor))
                {
                    OnAirControlFactorChanged(airControlFactor);
                    JumpLogger.LogWhite($"读取空中控制强度: {airControlFactor}");
                }

                if (ModSettingAPI.GetSavedValue("airDragFactor", out float airDragFactor))
                {
                    OnAirDragFactorChanged(airDragFactor);
                    JumpLogger.LogWhite($"读取空中惯性保持: {airDragFactor}");
                }

                if (ModSettingAPI.GetSavedValue("enableJumpLog", out bool enableJumpLog))
                {
                    OnEnableJumpLogChanged(enableJumpLog);
                    JumpLogger.LogWhite($"读取日志开关: {enableJumpLog}");
                }

                JumpLogger.LogWhite("配置加载完成！");
            }
            else
            {
                JumpLogger.LogWhite("未发现保存的配置，使用默认值");
                // 保持默认值，不强制设置
            }
        }

        // 配置变更回调方法
        private void OnMinJumpPowerChanged(float value)
        {
            MinJumpPower = Mathf.Clamp(value, 0.5f, 15f);
            // 确保最小值不大于最大值
            if (MinJumpPower > MaxJumpPower)
            {
                MaxJumpPower = MinJumpPower;
                ModSettingAPI.SetValue("maxJumpPower", MaxJumpPower);
            }
            JumpLogger.LogWhite($"最小跳跃力度更新为: {MinJumpPower}");
        }

        private void OnMaxJumpPowerChanged(float value)
        {
            MaxJumpPower = Mathf.Clamp(value, 5f, 25f);
            // 确保最大值不小于最小值
            if (MaxJumpPower < MinJumpPower)
            {
                MinJumpPower = MaxJumpPower;
                ModSettingAPI.SetValue("minJumpPower", MinJumpPower);
            }
            JumpLogger.LogWhite($"最大跳跃力度更新为: {MaxJumpPower}");
        }

        private void OnBoostAccelerationChanged(float value)
        {
            BoostAcceleration = Mathf.Clamp(value, 1f, 30f);
            JumpLogger.LogWhite($"初始加速度更新为: {BoostAcceleration}");
        }

        private void OnAccelerationDecayChanged(float value)
        {
            AccelerationDecay = Mathf.Clamp(value, 0.1f, 0.95f);
            JumpLogger.LogWhite($"加速度衰减系数更新为: {AccelerationDecay}");
        }

        private void OnJumpKeyChanged(KeyCode keyCode)
        {
            JumpKey = keyCode;
            JumpLogger.LogWhite($"跳跃按键更新为: {keyCode}");
        }

        private void OnAirControlFactorChanged(float value)
        {
            AirControlFactor = Mathf.Clamp(value, 0.1f, 1.0f);
            JumpLogger.LogWhite($"空中控制强度更新为: {AirControlFactor:F2} (地面强度的{AirControlFactor * 100:F0}%)");
        }

        private void OnAirDragFactorChanged(float value)
        {
            AirDragFactor = Mathf.Clamp(value, 0.5f, 1.0f);
            JumpLogger.LogWhite($"空中惯性保持更新为: {AirDragFactor:F3} (每秒保留{AirDragFactor * 100:F0}%速度)");
        }

        private void OnEnableJumpLogChanged(bool enabled)
        {
            EnableJumpLog = enabled;
            JumpLogger.enableLogs = enabled; // 同步到JumpLogger
            JumpLogger.LogWhite($"跳跃日志开关更新为: {enabled}");
        }

        /// <summary>
        /// 重置按钮回调函数
        /// </summary>
        private void OnResetConfigClicked()
        { 
            ResetToDefaults();
        }

        /// <summary>
        /// 重置所有配置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            JumpLogger.LogWhite("开始重置跳跃配置为默认值...");

            // 重置所有配置为默认值，同时更新UI显示
            MinJumpPower = 5f;
            MaxJumpPower = 8f;
            BoostAcceleration = 10f;
            AccelerationDecay = 0.7f;
            JumpKey = KeyCode.Z;
            EnableJumpLog = false;

            // 重置空中控制参数
            AirControlFactor = 0.6f;
            AirDragFactor = 0.8f;

            // 同步更新ModSetting的UI显示值
            ModSettingAPI.SetValue("minJumpPower", 5f);
            ModSettingAPI.SetValue("maxJumpPower", 8f);
            ModSettingAPI.SetValue("boostAcceleration", 10f);
            ModSettingAPI.SetValue("accelerationDecay", 0.7f);
            ModSettingAPI.SetValue("jumpKey", KeyCode.Z);
            ModSettingAPI.SetValue("enableJumpLog", false);

            // 重置空中控制参数UI显示
            ModSettingAPI.SetValue("airControlFactor", 0.6f);
            ModSettingAPI.SetValue("airDragFactor", 0.8f);

            // 同步到JumpLogger
            JumpLogger.enableLogs = false;

            JumpLogger.LogWhite("所有配置已重置为默认值！");
        }

      }
}