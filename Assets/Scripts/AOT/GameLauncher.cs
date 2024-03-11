using HybridCLR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AOT
{
    /// <summary>
    /// 用于启动游戏，执行版本检查，版本更新，加载程序集，进入游戏
    /// 错误处理是用的打印日志，正式上线的话需要一个错误处理系统来给玩家显示错误信息
    /// </summary>
    public partial class GameLauncher : MonoBehaviour
    {
        #region Inner Class

        private class MethodExecutionInfo
        {
            public MethodInfo method;
            public int sequence;

            public MethodExecutionInfo(MethodInfo method, int sequence)
            {
                this.method = method;
                this.sequence = sequence;
            }
        }

        #endregion

        #region FieldsAndProperties

        const string START_SCENE_NAME = "Assets/Scenes/GameScenes/StartScene.unity";

        const string META_DATA_DLL_PATH = "Assets/HotUpdateDlls/MetaDataDll/";
        const string HOT_UPDATE_DLL_PATH = "Assets/HotUpdateDlls/HotUpdateDll/";
        const string GAMEPLAY_DLL_NAME = "GamePlay.dll";
        const string META_DATA_DLLS_TO_LOAD_PATH = "Assets/HotUpdateDlls/MetaDataDllToLoad.txt";

        private Coroutine _launchCoroutine;
        private byte[] _dllBytes;

        private Dictionary<string, Assembly> _allHotUpdateAssemblies = new();

        public const string META_DATA_DLL_SEPARATOR = "!";

        ///GamePlay程序集依赖的热更程序集，这些程序集要先于gameplay程序集加载，需要手动填写
        private readonly List<string> _gamePlayDependencyDlls = new List<string>()
        {
        };

        //热更程序集中所有使用了RuntimeInitializeOnLoadMethod attribute的程序集，需要手动填写,HybridCLR不支持该特性
        private readonly List<string> _hasRuntimeInitializeOnLoadMethodAssemblies = new List<string>()
        {
        };

        private IAssetManager _assetManager = new AddressableAssetManager();
        private UIVersionUpdate _versionUpdateUI;

        public bool enableHybridCLR = true;

        #endregion

        #region MainLife

        private void Start()
        {
            OptimizeHybridCLR();
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
        /// 反射执行被RuntimeInitializeOnLoadMethod attribute标注的函数，HybirdCLR不支持该attribute
        /// </summary>
        private void ExecuteRuntimeInitializeOnLoadMethodAttribute()
        {
            var runtimeInitializedAttributeType = typeof(RuntimeInitializeOnLoadMethodAttribute);
            List<MethodExecutionInfo> runtimeMethods = new();
            foreach (var assemblyName in _hasRuntimeInitializeOnLoadMethodAssemblies)
            {
                var assembly = GetAssembly(assemblyName);
                if (assembly == null)
                {
                    Debug.LogError($"cant find assembly,name:{assemblyName}");
                    continue;
                }

                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public |
                                                           BindingFlags.NonPublic))
                    {
                        if (!method.IsStatic)
                            return;
                        var attribute =
                            method.GetCustomAttribute(runtimeInitializedAttributeType) as
                                RuntimeInitializeOnLoadMethodAttribute;
                        if (attribute == null)
                            return;
                        var sequence = (int)attribute.loadType;
                        var methodInfo = new MethodExecutionInfo(method, sequence);
                        runtimeMethods.Add(methodInfo);
                    }
                }
            }

            runtimeMethods.Sort((a, b) => b.sequence.CompareTo(a.sequence));
            foreach (var methodInfo in runtimeMethods)
            {
                Debug.Log($"call method methodName:{methodInfo.method.Name} sequence:{methodInfo.sequence}");
                methodInfo.method.Invoke(null, null);
            }

            Debug.Log("RuntimeInitializeOnLoadMethod finish!");
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