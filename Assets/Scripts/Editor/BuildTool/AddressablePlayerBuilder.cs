using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace BuildTool
{
    /// <summary>
    /// Addressable打包
    /// </summary>
    public static class AddressablePlayerBuilder
    {
        private const string CONTENT_UPDATE_GROUP_NAME = "Content Update";
        private const int RETRY_COUNT = 3;
        private const int TIME_OUT = 10;
        
        private static AddressableAssetSettings DefaultSettings => AddressableAssetSettingsDefaultObject.Settings;
        
        private static AddressableAssetGroup ContentUpdateGroup => DefaultSettings.groups.Find(g => g.name.Contains(CONTENT_UPDATE_GROUP_NAME));
        
        [MenuItem("Build/BuildContentAndPlayer")]
        private static void BuildContentAndPlayerWithHybridCLR()
        {
            HybridHotUpdateEditorHelper.SetEnableHotUpdate(true);
            HybridHotUpdateEditorHelper.BuildHotUpdateDlls(true);
            BuildContentAndPlayer();
        }

        [MenuItem("Build/UpdatePreviousBuild")]
        private static void UpdatePreviousPlayerWithHybridCLR()
        {
            HybridHotUpdateEditorHelper.BuildHotUpdateDlls(false);
            UpdatePreviousPlayer();
        }

        private static void BuildContentAndPlayer()
        {
            BuildAddressableContent();
            OnlyBuildPlayer();
        }

        private static void BuildAddressableContent()
        {
            DeleteContentUpdateGroup();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            bool success = string.IsNullOrEmpty(result.Error);

            if (!success)
            {
                Debug.LogError($"Addressables build error encountered: {result.Error}");
            }
        }

        private static void OnlyBuildPlayer()
        {
            var options = new BuildPlayerOptions();
            BuildPlayerOptions playerSettings = BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions(options);

            BuildPipeline.BuildPlayer(playerSettings);
        }
        
        private static void UpdatePreviousPlayer()
        {
            DeleteContentUpdateGroup();
            var path = ContentUpdateScript.GetContentStateDataPath(false);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"cant find the .bin file! path:{path}");
                return;
            }
            
            var modifiedEntries = ContentUpdateScript.GatherModifiedEntries(DefaultSettings, path);
            ContentUpdateScript.CreateContentUpdateGroup(DefaultSettings, modifiedEntries, CONTENT_UPDATE_GROUP_NAME);
            var schema = ContentUpdateGroup.GetSchema<BundledAssetGroupSchema>();
            //设置group的重试次数和超时时间，如果不设置，可能出现下载卡住的情况
            schema.RetryCount = RETRY_COUNT;
            schema.Timeout = TIME_OUT;
            ContentUpdateScript.BuildContentUpdate(DefaultSettings, path);
        }

        /// <summary>
        /// 删除content update group,还原group到最原始状态(但如果资源是直接放在某个group的根，会误删它)
        /// 建议使用版本控制工具，可以手动还原addressable group的更改，或者放到CI自动还原打包机更改，这样就可以屏蔽此函数了
        /// </summary>
        private static void DeleteContentUpdateGroup()
        {
            if(ContentUpdateGroup != null)
                DefaultSettings.RemoveGroup(ContentUpdateGroup);
        }
    }
}