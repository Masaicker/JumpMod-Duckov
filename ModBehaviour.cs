using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Duckov;
using Jump;
using HarmonyLib;
using ModSetting;
using Random = UnityEngine.Random;

namespace Jump
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private static Harmony? harmonyInstance;
        private static JumpConfigManager? configManager;
        private static List<string> cachedAudioFiles = new List<string>();

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

            // 缓存音效文件
            CacheAudioFile();

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
            harmonyInstance?.UnpatchAll(harmonyInstance.Id);
            JumpLogger.LogWhite($"已卸载 {harmonyInstance?.Id}");
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
        /// 缓存音效文件路径（只执行一次）
        /// </summary>
        private void CacheAudioFile()
        {
            var assemblyDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var audioDirectory = System.IO.Path.Combine(assemblyDirectory ?? string.Empty, "audio");

            if (!System.IO.Directory.Exists(audioDirectory))
            {
                cachedAudioFiles.Clear();
                JumpLogger.LogWhite("音频文件夹不存在: audio");
                return;
            }

            cachedAudioFiles.Clear();
            string[] audioExtensions = { ".wav", ".mp3", ".ogg" };

            foreach (var extension in audioExtensions)
            {
                var files = System.IO.Directory.GetFiles(audioDirectory, $"*{extension}");
                cachedAudioFiles.AddRange(files);
            }

            JumpLogger.LogWhite($"缓存音频文件数量: {cachedAudioFiles.Count}");
            if (cachedAudioFiles.Count == 0)
            {
                JumpLogger.LogWhite("未找到任何音效文件");
            }
        }

        /// <summary>
        /// 获取配置管理器实例
        /// </summary>
        public static JumpConfigManager? GetConfigManager()
        {
            return configManager;
        }

        /// <summary>
        /// 随机获取一个音效文件路径
        /// </summary>
        public static string? GetRandomAudioFile()
        {
            if (cachedAudioFiles.Count == 0) return null;
            var index = Random.Range(0, cachedAudioFiles.Count);
            return cachedAudioFiles[index];
        }

        // Harmony补丁：阻止跳跃时的闪避
        [HarmonyPatch(typeof(CharacterMainControl), "Dash")]
        private class DashPrefix
        {
            [HarmonyPrefix]
            static bool Prefix(CharacterMainControl __instance)
            {
                // 如果正在跳跃中，阻止闪避
                return !CharacterJumpController.isJumping;
            }
        }

        // Harmony补丁：跳跃时禁用脚步声
        [HarmonyPatch(typeof(CharacterSoundMaker), "Update")]
        private class CharacterSoundMakerUpdatePrefix
        {
            [HarmonyPrefix]
            static bool Prefix(CharacterSoundMaker __instance)
            {
                return !CharacterJumpController.isJumping;
            }
        }

    }
}
