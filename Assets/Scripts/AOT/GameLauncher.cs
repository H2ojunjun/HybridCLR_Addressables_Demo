using HybridCLR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AOT
{
    /// <summary>
    /// 用于启动游戏，执行版本检查，版本更新，加载程序集，进入游戏
    /// 错误处理是用的打印日志，正式上线的话需要一个错误处理系统来给玩家显示错误信息
    /// </summary>
    public class GameLauncher : MonoBehaviour
    {
        #region Inner Class
        
        [Serializable]
        public class MethodExecutionInfo
        {
            public string assemblyName;

            public string typeName;
            
            public string methodName;
            
            public int sequence;
            
            public MethodExecutionInfo(string assemblyName, string typeName, string methodName, int sequence)
            {
                this.assemblyName = assemblyName;
                this.typeName = typeName;
                this.methodName = methodName;
                this.sequence = sequence;
            }

            public void Execute()
            {
                var assembly = FindObjectOfType<GameLauncher>().GetAssembly(assemblyName);
                if (assembly == null)
                {
                    Debug.LogError($"cant find assembly,name:{assemblyName}");
                    return;
                }

                var type = assembly.GetType(typeName);
                if (type == null)
                {
                    Debug.LogError($"cant find type,name:{typeName}");
                    return;
                }

                var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    Debug.LogError($"cant find method,name:{methodName}");
                    return;
                }

                method.Invoke(null, null);
            }
        }

        [Serializable]
        public class RuntimeInitializeOnLoadMethodCollection
        {
            public List<MethodExecutionInfo> methodExecutionInfos = new List<MethodExecutionInfo>();
        }
        
        #endregion

        #region FieldsAndProperties

        const string START_SCENE_NAME = "Assets/Scenes/GameScenes/StartScene.unity";

        const string META_DATA_DLL_PATH = "Assets/HotUpdateDlls/MetaDataDll/";
        const string HOT_UPDATE_DLL_PATH = "Assets/HotUpdateDlls/HotUpdateDll/";
        const string GAMEPLAY_DLL_NAME = "GamePlay.dll";
        public const string META_DATA_DLLS_TO_LOAD_PATH = "Assets/HotUpdateDlls/MetaDataDllToLoad.txt";
        public const string RUN_TIME_INITIALIZE_ON_LOAD_METHOD_COLLECTION_PATH = "Assets/HotUpdateDlls/RuntimeInitializeOnLoadMethodCollection.txt";
        public const string META_DATA_DLL_SEPARATOR = "!";
        
        private Coroutine _launchCoroutine;
        private byte[] _dllBytes;

        private Dictionary<string, Assembly> _allHotUpdateAssemblies = new();

        ///GamePlay程序集依赖的热更程序集，这些程序集要先于gameplay程序集加载，需要手动填写
        private readonly List<string> _gamePlayDependencyDlls = new List<string>()
        {
        };

        private IAssetManager _assetManager = new AddressableAssetManager();
        private UIVersionUpdate _versionUpdateUI;

        public bool enableHybridCLR = true;
        
        #endregion

        #region MainLife

        private void Start()
        {
            if(enableHybridCLR)
                HybridCLROptimizer.OptimizeHybridCLR();
            _launchCoroutine = StartCoroutine(Launch());
        }

        private void OnDestroy()
        {
            StopCoroutine(_launchCoroutine);
            _launchCoroutine = null;
        }

        private IEnumerator Launch()
        {
#if UNITY_EDITOR
            enableHybridCLR = false;
#endif
            Debug.Log($"Launch Game! enableHybridCLR:{enableHybridCLR}");
            yield return VersionCheck();
            yield return VersionUpdate();
            yield return LoadAssemblies();
            yield return EnterGame();
        }

        private IEnumerator VersionCheck()
        {
            Debug.Log($"VersionCheck start!");
            yield return CheckNewAPPVersion();
            yield return _assetManager.CheckUpdate();
            Debug.Log($"VersionCheck finish,has Content to download:{_assetManager.HasContentToDownload}");
        }

        /// <summary>
        /// todo 如果包体版本有更新则提示用户去app store重新下载
        /// </summary>
        /// <returns></returns>
        private IEnumerator CheckNewAPPVersion()
        {
            yield return null;
        }

        private IEnumerator VersionUpdate()
        {
            if (!_assetManager.HasContentToDownload)
                yield break;
            Debug.Log($"VersionUpdate start!");
            yield return OpenVersionUpdateUI();
            yield return Download();
            Debug.Log($"VersionUpdate finish!");
        }

        //打开版本更新UI
        private IEnumerator OpenVersionUpdateUI()
        {
            _versionUpdateUI = FindObjectOfType<UIVersionUpdate>(true);
            if (_versionUpdateUI == null)
            {
                Debug.LogError("cant find UIVersionUpdate");
                return null;
            }

            _versionUpdateUI.gameObject.SetActive(true);
            _versionUpdateUI.GetDownloadProgress = _assetManager.GetDownloadProgress;
            return null;
        }

        //下载资源
        private IEnumerator Download()
        {
            yield return _assetManager.DownloadAssets();
            _versionUpdateUI.GetDownloadProgress = null;
        }

        private IEnumerator LoadAssemblies()
        {
            if (!enableHybridCLR)
                yield break;
            Debug.Log("LoadAssemblies start!");
            yield return LoadMetadataForAOTAssemblies();
            yield return LoadGamePlayDependencyAssemblies();
            yield return LoadGamePlayAssemblies();
            yield return _assetManager.AfterAllDllLoaded();
            ExecuteRuntimeInitializeOnLoadMethodAttribute();
            Debug.Log("LoadAssemblies finish!");
            yield return null;
        }

        //补充元数据
        private IEnumerator LoadMetadataForAOTAssemblies()
        {
            var aotAssemblies = GetMetaDataDllToLoad();
            if (aotAssemblies == null)
            {
                yield break;
            }
            
            foreach (var aotDllName in aotAssemblies)
            {
                if(string.IsNullOrEmpty(aotDllName))
                    continue;
                var path = $"{META_DATA_DLL_PATH}{aotDllName}.bytes";
                ReadDllBytes(path);
                if (_dllBytes != null)
                {
                    var err = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(_dllBytes, HomologousImageMode.SuperSet);
                    Debug.Log($"LoadMetadataForAOTAssembly:{aotDllName}. ret:{err}");
                }
            }

            Debug.Log("LoadMetadataForAOTAssemblies finish!");
        }

        private string[] GetMetaDataDllToLoad()
        {
            string[] result = null;
            var metaDataToLoad = _assetManager.LoadAsset<TextAsset>(META_DATA_DLLS_TO_LOAD_PATH);
            if (metaDataToLoad == null)
            {
                Debug.LogError($"cant load metaDataText,path:{META_DATA_DLLS_TO_LOAD_PATH}");
            }
            else
            {
                var text = metaDataToLoad.text;
                result = text.Split(META_DATA_DLL_SEPARATOR);
                _assetManager.UnloadAsset(metaDataToLoad);
            }

            return result;
        }

        //加载GamePlay依赖的第三方程序集
        private IEnumerator LoadGamePlayDependencyAssemblies()
        {
            foreach (var dllName in _gamePlayDependencyDlls)
            {
                yield return LoadSingleHotUpdateAssembly(dllName);
            }

            Debug.Log("LoadGamePlayDependencyAssemblies finish!");
        }

        //加载GamePlay程序集
        private IEnumerator LoadGamePlayAssemblies()
        {
            yield return LoadSingleHotUpdateAssembly(GAMEPLAY_DLL_NAME);
            Debug.Log("LoadGamePlayAssemblies finish!");
        }

        private IEnumerator EnterGame()
        {
            yield return _assetManager.ChangeScene(START_SCENE_NAME);
            Debug.Log("EnterGame finish!");
        }

        #endregion

        #region Other

        /// <summary>
        /// 反射执行被RuntimeInitializeOnLoadMethod attribute标注的函数，HybridCLR不支持该attribute
        /// </summary>
        private void ExecuteRuntimeInitializeOnLoadMethodAttribute()
        {
            var runtimeInitializeOnLoadMethodCollection = _assetManager.LoadAsset<TextAsset>(RUN_TIME_INITIALIZE_ON_LOAD_METHOD_COLLECTION_PATH);
            var json = runtimeInitializeOnLoadMethodCollection.text;
            var collection = JsonUtility.FromJson<RuntimeInitializeOnLoadMethodCollection>(json);
            foreach (var methodInfo in collection.methodExecutionInfos)
            {
                methodInfo.Execute();
            }
            
            Debug.Log("execute RuntimeInitializeOnLoadMethod finish!");
        }

        private void ReadDllBytes(string path)
        {
            var dllText = _assetManager.LoadAsset<TextAsset>(path);

            if (dllText == null)
            {
                Debug.LogError($"cant load dllText,path:{path}");
                _dllBytes = null;
            }
            else
            {
                _dllBytes = dllText.bytes;
            }

            _assetManager.UnloadAsset(dllText);
        }

        private IEnumerator LoadSingleHotUpdateAssembly(string dllName)
        {
            var path = $"{HOT_UPDATE_DLL_PATH}{dllName}.bytes";
            ReadDllBytes(path);
            if (_dllBytes != null)
            {
                var assembly = Assembly.Load(_dllBytes);
                _allHotUpdateAssemblies.Add(assembly.FullName, assembly);
                Debug.Log($"Load Assembly success,assembly Name:{assembly.FullName}");
            }

            yield return null;
        }

        private Assembly GetAssembly(string assemblyName)
        {
            assemblyName = assemblyName.Replace(".dll", "");
            IEnumerable<Assembly> allAssemblies =
                enableHybridCLR ? _allHotUpdateAssemblies.Values : AppDomain.CurrentDomain.GetAssemblies();
            return allAssemblies.First(assembly => assembly.FullName.Contains(assemblyName));
        }

        #endregion
    }
}