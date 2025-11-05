using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Duckov;
using Jump;
using HarmonyLib;
using ModSetting;

namespace Jump
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private static Harmony? harmonyInstance;
        private static JumpConfigManager? configManager;

        protected override void OnAfterSetup()
        {
            // 初始化ModSetting配置系统
            if (ModSettingAPI.Init(info))
            {
                configManager = new JumpConfigManager(info);
                configManager.SetupConfiguration();
                JumpLogger.LogWhite("ModSetting配置系统初始化成功！");
            }
            else
            {
                JumpLogger.LogRed("ModSetting初始化失败！使用默认配置。");
            }

            // 初始化Harmony补丁
            harmonyInstance = new Harmony("Mhz.jumpmod");
            harmonyInstance.PatchAll();
            // 监听关卡初始化完成事件
            LevelManager.OnAfterLevelInitialized += OnLevelInitialized;
        }

        protected override void OnBeforeDeactivate()
        {
            // 清理事件监听
            LevelManager.OnAfterLevelInitialized -= OnLevelInitialized;

            // 清理Harmony补丁
            harmonyInstance?.UnpatchAll();
            harmonyInstance = null;
        }

        private void OnLevelInitialized()
        {
            // 每次场景加载完成后重新安装跳跃功能
            InstallJumpMod();
        }

        private void InstallJumpMod()
        {
            CharacterMainControl mainCharacter = LevelManager.Instance.MainCharacter;

            // 检查是否已经安装了跳跃控制器
            if (mainCharacter.GetComponent<CharacterJumpController>() != null)
            {
                JumpLogger.LogWhite("跳跃功能已安装！");
                return;
            }

            // 添加跳跃控制器
            mainCharacter.gameObject.AddComponent<CharacterJumpController>();

            JumpLogger.LogWhite("跳跃功能安装成功！按Z键跳跃！");
        }

        /// <summary>
        /// 获取配置管理器实例
        /// </summary>
        public static JumpConfigManager? GetConfigManager()
        {
            return configManager;
        }

        // Harmony补丁：阻止跳跃时的闪避
        [HarmonyPatch(typeof(CharacterMainControl), "Dash")]
        private class DashPrefix
        {
            [HarmonyPrefix]
            static bool Prefix(CharacterMainControl __instance)
            {
                // 如果正在跳跃中，阻止闪避
                if (CharacterJumpController.isJumping)
                {
                    JumpLogger.LogWhite("跳跃中：闪避动作被Harmony补丁阻止");
                    return false; // 阻止原方法执行
                }
                return true; // 允许原方法执行
            }
        }

    }
}
