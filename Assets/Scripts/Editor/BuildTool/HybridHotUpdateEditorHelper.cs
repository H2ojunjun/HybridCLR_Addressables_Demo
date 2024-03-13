using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AOT;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.HotUpdate;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static AOT.GameLauncher;

namespace BuildTool
{
    public static class HybridHotUpdateEditorHelper
    {
        static string HotUpdateDllPath => $"{Application.dataPath}/../HybridCLRData/HotUpdateDlls/{EditorUserBuildSettings.activeBuildTarget}/";

        static string HotUpdateDestinationPath => $"{Application.dataPath}/HotUpdateDlls/HotUpdateDll/";

        static string MetaDataDLLPath => $"{Application.dataPath}/../HybridCLRData/AssembliesPostIl2CppStrip/{EditorUserBuildSettings.activeBuildTarget}/";

        static string MetaDataDestinationPath => $"{Application.dataPath}/HotUpdateDlls/MetaDataDll/";

        static string AOTGenericReferencesPath => $"{Application.dataPath}/HybridCLRGenerate/AOTGenericReferences.cs";

        static string GameLauncherSceneName => "Assets/Scenes/GameLauncher.unity";

        static string BuildDataPath => $"{Application.dataPath}/../BuildData/";

        static string CurrPlatformBuildDataPath => $"{BuildDataPath}{EditorUserBuildSettings.activeBuildTarget}/";

        /// <summary>
        /// 执行一次HybridCLR的generate all，并将生成的dll拷贝到assets中
        /// </summary>
        public static void BuildHotUpdateDlls(bool isBuildPlayer)
        {
            //如果未安装，安装
            var controller = new InstallerController();
            if (!controller.HasInstalledHybridCLR())
                controller.InstallDefaultHybridCLR();
            
            //执行HybridCLR
            PrebuildCommand.GenerateAll();
            
            //如果是更新，则检查热更代码中是否引用了被裁减的AOT代码
            if (!isBuildPlayer)
                if (!CheckAccessMissingMetadata())
                    return;
            //拷贝dll
            CopyHotUpdateDll();
            CopyMetaDataDll();
            
            //如果是发包，则拷贝AOT dll
            if (isBuildPlayer)
                CopyAotDllsForStripCheck();
            
            //收集RuntimeInitializeOnLoadMethod
            CollectRuntimeInitializeOnLoadMethod();
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
            var dir = new DirectoryInfo(HotUpdateDllPath);
            var files = dir.GetFiles();
            var destDir = HotUpdateDestinationPath;
            if (Directory.Exists(destDir))
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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("copy hot update dlls success!");
        }

        private static void CopyMetaDataDll()
        {
            List<string> assemblies = GetMetaDataDllList();
            var dir = new DirectoryInfo(MetaDataDLLPath);
            var files = dir.GetFiles();
            var destDir = MetaDataDestinationPath;
            if (Directory.Exists(destDir))
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
            {
                using (File.Create(META_DATA_DLLS_TO_LOAD_PATH))
                {
                }
            }

            File.WriteAllText(META_DATA_DLLS_TO_LOAD_PATH, metaDataDllListStr, Encoding.UTF8);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("copy meta data dll success!");
        }

        /// <summary>
        /// 热更代码中可能会调用到AOT中已经被裁剪的函数，需要检查一下
        /// https://hybridclr.doc.code-philosophy.com/docs/basic/codestriping#%E6%A3%80%E6%9F%A5%E7%83%AD%E6%9B%B4%E6%96%B0%E4%BB%A3%E7%A0%81%E4%B8%AD%E6%98%AF%E5%90%A6%E5%BC%95%E7%94%A8%E4%BA%86%E8%A2%AB%E8%A3%81%E5%89%AA%E7%9A%84%E7%B1%BB%E5%9E%8B%E6%88%96%E5%87%BD%E6%95%B0
        /// </summary>
        private static bool CheckAccessMissingMetadata()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string aotDir = CurrPlatformBuildDataPath;
            var checker = new MissingMetadataChecker(aotDir, new List<string>());

            string hotUpdateDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                string dllPath = $"{hotUpdateDir}/{dll}";
                bool notAnyMissing = checker.Check(dllPath);
                if (!notAnyMissing)
                {
                    Debug.LogError($"Update player failed!some hotUpdate dll:{dll} is using a stripped method or type in AOT dll!Please rebuild a player!");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 如果是发包，需要拷贝Aot dll到BuildData文件夹下，为后续更新时的代码裁剪检查做准备
        /// </summary>
        private static void CopyAotDllsForStripCheck()
        {
            if (!Directory.Exists(BuildDataPath))
                Directory.CreateDirectory(BuildDataPath);
            var dir = new DirectoryInfo(MetaDataDLLPath);
            var files = dir.GetFiles();
            var destDir = CurrPlatformBuildDataPath;
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);
            foreach (var file in files)
            {
                if (file.Extension == ".dll")
                {
                    var desPath = destDir + file.Name;
                    file.CopyTo(desPath, true);
                }
            }
        }

        //之所以采用读取C#文件的方式是因为如果直接读取代码中的列表会出现打包时更改了AOTGenericReferences.cs但Unity编译未完成导致
        //AOTGenericReferences中PatchedAOTAssemblyList还是代码修改前的数据的问题，是因为Unity还没有reload domain
        //https://docs.unity.cn/2023.2/Documentation/Manual/DomainReloading.html
        private static List<string> GetMetaDataDllList()
        {
            var aotGenericRefPath = AOTGenericReferencesPath;
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

        private static void CollectRuntimeInitializeOnLoadMethod()
        {
            RuntimeInitializeOnLoadMethodCollection runtimeInitializeOnLoadMethodCollection = new();
            var hotUpdateAssemblies = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;
            var runtimeInitializedAttributeType = typeof(RuntimeInitializeOnLoadMethodAttribute);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name;
                if (!hotUpdateAssemblies.Contains(assemblyName))
                {
                    continue;
                }

                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public |
                                                           BindingFlags.NonPublic))
                    {
                        if (!method.IsStatic)
                            continue;
                        var attribute =
                            method.GetCustomAttribute(runtimeInitializedAttributeType) as
                                RuntimeInitializeOnLoadMethodAttribute;
                        if (attribute == null)
                            continue;
                        var sequence = (int)attribute.loadType;
                        var methodInfo = new MethodExecutionInfo(assemblyName, type.FullName, method.Name, sequence);
                        runtimeInitializeOnLoadMethodCollection.methodExecutionInfos.Add(methodInfo);
                    }
                }
            }

            runtimeInitializeOnLoadMethodCollection.methodExecutionInfos.Sort(
                (a, b) => b.sequence.CompareTo(a.sequence));
            var json = JsonUtility.ToJson(runtimeInitializeOnLoadMethodCollection, true);
            if (!File.Exists(RUN_TIME_INITIALIZE_ON_LOAD_METHOD_COLLECTION_PATH))
            {
                using (File.Create(RUN_TIME_INITIALIZE_ON_LOAD_METHOD_COLLECTION_PATH))
                {
                }
            }

            File.WriteAllText(RUN_TIME_INITIALIZE_ON_LOAD_METHOD_COLLECTION_PATH, json, Encoding.UTF8);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}