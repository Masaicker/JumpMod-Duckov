using UnityEngine;
using UnityEngine.InputSystem;
using Duckov.Modding;
using MasaickerLib.ModSetting;

namespace Jump
{
    /// <summary>
    /// 跳跃MOD配置UI绑定器 - 只负责ModSetting UI绑定 
    /// </summary>
    public class JumpConfigManager
    {
        private readonly ModInfo modInfo;

        public JumpConfigManager(ModInfo modInfo)
        {
            this.modInfo = modInfo;
        }

        /// <summary>
        /// 设置ModSetting UI绑定
        /// </summary>
        public void SetupConfiguration()
        {
            // 跳跃参数滑块
            ModSettingAPI.AddSlider("minJumpPower", "最小跳跃力度 Min Jump Power",
                JumpSetting.MinJumpPower, new Vector2(0.5f, 15f),
                JumpSetting.SetMinJumpPower, decimalPlaces: 1);

            ModSettingAPI.AddSlider("maxJumpPower", "最大跳跃力度 Max Jump Power",
                JumpSetting.MaxJumpPower, new Vector2(5f, 25f),
                JumpSetting.SetMaxJumpPower, decimalPlaces: 1);

            ModSettingAPI.AddSlider("boostAcceleration", "初始加速度 Initial Acceleration",
                JumpSetting.BoostAcceleration, new Vector2(1f, 30f),
                JumpSetting.SetBoostAcceleration, decimalPlaces: 1);

            ModSettingAPI.AddSlider("accelerationDecay", "加速度衰减系数 Acceleration Decay",
                JumpSetting.AccelerationDecay, new Vector2(0.1f, 0.95f),
                JumpSetting.SetAccelerationDecay, decimalPlaces: 2);

            // 按键绑定
            ModSettingAPI.AddKeybinding("jumpKey", "跳跃按键 Jump Key",
                JumpSetting.JumpKey, Key.Z,
                JumpSetting.SetJumpKey);

            // 空中控制参数
            ModSettingAPI.AddSlider("airControlFactor", "空中控制强度 Air Control Strength",
                JumpSetting.AirControlFactor, new Vector2(0.1f, 1.0f),
                JumpSetting.SetAirControlFactor, decimalPlaces: 2);

            ModSettingAPI.AddSlider("airDragFactor", "空中惯性保持 Air Inertial Retention",
                JumpSetting.AirDragFactor, new Vector2(0.1f, 1.0f),
                JumpSetting.SetAirDragFactor, decimalPlaces: 3);

            // 日志开关
            ModSettingAPI.AddToggle("enableJumpLog", "启用跳跃日志 Enable Jump Log",
                JumpSetting.EnableJumpLog,
                JumpSetting.SetEnableJumpLog);

            // 重置按钮
            ModSettingAPI.AddButton("resetConfig", "", "重置所有配置 Reset All",
                JumpSetting.ResetToDefaults);
        }
    }
}
