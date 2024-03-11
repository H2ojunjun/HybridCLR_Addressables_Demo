using System.Collections.Generic;
using System.IO;
using System.Text;
using AOT;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static AOT.GameLauncher;

namespace BuildTool
{
    public static class HybridHotUpdateEditorHelper
    {
        const string HOT_UPDATE_DLL_PATH = "/../HybridCLRData/HotUpdateDlls/";
        const string HOT_UPDATE_DESTINATION_PATH = "/HotUpdateDlls/HotUpdateDll/";

        const string META_DATA_DLL_PATH = "/../HybridCLRData/AssembliesPostIl2CppStrip/";
        const string META_DATA_DESTINATION_PATH = "/HotUpdateDlls/MetaDataDll/";
        const string META_DATA_DLLS_TO_LOAD_PATH = "Assets/HotUpdateDlls/MetaDataDllToLoad.txt";

        const string AOT_GENERIC_REFERENCES_PATH = "/HybridCLRGenerate/AOTGenericReferences.cs";

        const string GameLauncherSceneName = "Assets/Scenes/GameLauncher.unity";

        /// <summary>
        /// 执行一次HybridCLR的generate all，并将生成的dll拷贝到assets中
        /// </summary>
        public static void BuildHotUpdateDlls()
        {
            var controller = new InstallerController();
            if (!controller.HasInstalledHybridCLR())
                controller.InstallDefaultHybridCLR();
            PrebuildCommand.GenerateAll();
            CopyHotUpdateDll();
            CopyMetaDataDll();
        }

        /// <summary>
        /// 设置是否开启热更新
        /// </summary>
        /// <param name="isEnable"></param>
        public static void SetEnableHotUpdate(bool isEnable)
        {
            Debug.Log($"SetEnableHotUpdate:{isEnable}");
            var gameLauncherScene = EditorSceneManager.OpenScene(GameLauncherSceneName, OpenSceneMode.Additive);
            if (!gameLauncherScene.IsValid())
            {
                Debug.LogError($"can't open scene:{GameLauncherSceneName}");
                return;
            }

            var gameLauncher = UnityEngine.Object.FindObjectOfType<GameLauncher>();
            if (gameLauncher == null)
            {
                Debug.LogError("can't find GameLauncher");
                return;
            }

            gameLauncher.enableHybridCLR = isEnable;
            EditorSceneManager.MarkSceneDirty(gameLauncherScene);
            EditorSceneManager.SaveScene(gameLauncherScene);
            EditorSceneManager.CloseScene(gameLauncherScene, false);
            HybridCLR.Editor.SettingsUtil.Enable = isEnable;
        }

        private static void CopyHotUpdateDll()
        {
            var assemblies = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;
            var dir = new DirectoryInfo(Application.dataPath + HOT_UPDATE_DLL_PATH +
                                        EditorUserBuildSettings.activeBuildTarget);
            var files = dir.GetFiles();
            var destDir = Application.dataPath + HOT_UPDATE_DESTINATION_PATH;
            Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);
            foreach (var file in files)
            {
                if (file.Extension == ".dll" && assemblies.Contains(file.Name.Substring(0, file.Name.Length - 4)))
                {
                    var desPath = destDir + file.Name + ".bytes";
                    file.CopyTo(desPath, true);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("copy hot update dlls success!");
        }

        private static void CopyMetaDataDll()
        {
            List<string> assemblies = GetMetaDataDllList();
            var dir = new DirectoryInfo(Application.dataPath + META_DATA_DLL_PATH + EditorUserBuildSettings.activeBuildTarget);
            var files = dir.GetFiles();
            var destDir = Application.dataPath + META_DATA_DESTINATION_PATH;
            Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);
            foreach (var file in files)
            {
                if (file.Extension == ".dll" && assemblies.Contains(file.Name))
                {
                    var desPath = destDir + file.Name + ".bytes";
                    file.CopyTo(desPath, true);
                }
            }

            var metaDataDllListStr = string.Join(META_DATA_DLL_SEPARATOR, assemblies);
            if (!File.Exists(META_DATA_DLLS_TO_LOAD_PATH))
                File.Create(META_DATA_DLLS_TO_LOAD_PATH);
            File.WriteAllText(META_DATA_DLLS_TO_LOAD_PATH, metaDataDllListStr, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("copy meta data dll success!");
        }

        //之所以采用读取C#文件的方式是因为如果直接读取代码中的列表会出现打包时更改了AOTGenericReferences.cs但Unity编译未完成导致
        //AOTGenericReferences中PatchedAOTAssemblyList还是代码修改前的数据的问题，是因为Unity还没有reload domain
        //https://docs.unity.cn/2023.2/Documentation/Manual/DomainReloading.html
        private static List<string> GetMetaDataDllList()
        {
            var aotGenericRefPath = Application.dataPath + AOT_GENERIC_REFERENCES_PATH;
            List<string> result = new List<string>();
            using (StreamReader reader = new StreamReader(aotGenericRefPath))
            {
                var lineStr = "";
                while (!reader.ReadLine().Contains("new List<string>"))
                {
                }

                reader.ReadLine();
                while (true)
                {
                    lineStr = reader.ReadLine().Replace("\t", "");
                    if (lineStr.Contains("};"))
                        break;
                    var dllName = lineStr.Substring(1, lineStr.Length - 3);
                    result.Add(dllName);
                }
            }

            return result;
        }
    }
}