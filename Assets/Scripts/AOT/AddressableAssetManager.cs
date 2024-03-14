using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace AOT
{
    /// <summary>
    /// Addressable的资源管理器（只用于启动游戏时更新）
    /// </summary>
    public class AddressableAssetManager : IAssetManager
    {
        #region InnerClass

        [Serializable]
        private class DownloadContent
        {
            public List<string> catalogs = new();
        }

        #endregion

        #region FieldsAndProperties

        //记录在playerPres里的需要下载的catalogs的ID
        const string DOWNLOAD_CATALOGS_ID = "DownloadCatalogs";

        private List<object> _KeysNeedToDownload = new();

        //此对象里保存了需要下载的catalog，每次获取新的catalog会将此对象保存到手机上，如果在下载的过程中关闭了游戏，下次打开还能拿到catalog继续下载
        private DownloadContent _downloadContent = new();

        private AsyncOperationHandle _downloadOP;

        public bool HasContentToDownload => _downloadContent != null && _downloadContent.catalogs != null &&
                                            _downloadContent.catalogs.Count > 0;

        #endregion

        #region API

        public T LoadAsset<T>(string path)
        {
            var op = Addressables.LoadAssetAsync<T>(path);
            if (!op.IsValid())
                return default;
            op.WaitForCompletion();
            return op.Result;
        }

        public void UnloadAsset(UnityEngine.Object asset)
        {
            if (asset != null)
                Addressables.Release(asset);
        }

        public IEnumerator CheckUpdate()
        {
            var checkUpdateOP = Addressables.CheckForCatalogUpdates(false);
            yield return checkUpdateOP;
            if (checkUpdateOP.Status == AsyncOperationStatus.Succeeded)
            {
                _downloadContent.catalogs = checkUpdateOP.Result;
                if (HasContentToDownload)
                {
                    Debug.Log("new version on server");
                    //说明服务器上有新的资源，记录要下载的catalog值在playerprefs中,如果下载的过程中被打断，下次打开游戏使用该值还能继续下载
                    var jsonStr = JsonUtility.ToJson(_downloadContent);
                    PlayerPrefs.SetString(DOWNLOAD_CATALOGS_ID, jsonStr);
                    PlayerPrefs.Save();
                }
                else
                {
                    if (PlayerPrefs.HasKey(DOWNLOAD_CATALOGS_ID))
                    {
                        //上一次的更新还没下载完
                        Debug.Log("there are some contents remains from last downloading");
                        var jsonStr = PlayerPrefs.GetString(DOWNLOAD_CATALOGS_ID);
                        JsonUtility.FromJsonOverwrite(jsonStr, _downloadContent);
                    }
                    else
                    {
                        //没有需要下载的内容
                        Debug.Log("there are no content to be downloaded");
                    }
                }

                if (HasContentToDownload)
                {
                    var updateCatalogOP = Addressables.UpdateCatalogs(_downloadContent.catalogs, false);
                    yield return updateCatalogOP;
                    if (updateCatalogOP.Status == AsyncOperationStatus.Succeeded)
                    {
                        _KeysNeedToDownload.Clear();
                        foreach (var resourceLocator in updateCatalogOP.Result)
                        {
                            _KeysNeedToDownload.AddRange(resourceLocator.Keys);
                        }
                    }
                    else
                    {
                        Debug.LogError($"Update catalog failed!exception:{updateCatalogOP.OperationException.Message}");
                    }

                    Addressables.Release(updateCatalogOP);
                }
            }
            else
            {
                Debug.LogError($"CheckUpdate failed!exception:{checkUpdateOP.OperationException.Message}");
            }

            Addressables.Release(checkUpdateOP);
            //更新完catalog后重新加载一下Addressable的Catalog
            yield return ReloadAddressableCatalog();
        }

        public IEnumerator DownloadAssets()
        {
            var downloadSizeOp = Addressables.GetDownloadSizeAsync((IEnumerable)_KeysNeedToDownload);
            yield return downloadSizeOp;
            Debug.Log($"download size:{downloadSizeOp.Result / (1024f * 1024f)}MB");

            if (downloadSizeOp.Result > 0)
            {
                Addressables.Release(downloadSizeOp);

                _downloadOP =
                    Addressables.DownloadDependenciesAsync((IEnumerable)_KeysNeedToDownload,
                        Addressables.MergeMode.Union, false);

                yield return _downloadOP;

                if (_downloadOP.Status == AsyncOperationStatus.Succeeded)
                    Debug.Log($"download finish!");
                else
                    Debug.LogError(
                        $"Download Failed! exception:{_downloadOP.OperationException.Message} \r\n {_downloadOP.OperationException.StackTrace}");

                Addressables.Release(_downloadOP);
            }

            //清除需要下载的内容
            Debug.Log($"delete key:{DOWNLOAD_CATALOGS_ID}");
            PlayerPrefs.DeleteKey(DOWNLOAD_CATALOGS_ID);
        }

        public DownloadInfo GetDownloadProgress()
        {
            if (!_downloadOP.IsValid())
                return default;
            var downloadStatus = _downloadOP.GetDownloadStatus();
            return new DownloadInfo(downloadStatus.Percent, downloadStatus.DownloadedBytes, downloadStatus.TotalBytes);
        }

        public IEnumerator AfterAllDllLoaded()
        {
            yield return ReloadAddressableCatalog();
        }

        public IEnumerator ChangeScene(string sceneName)
        {
            var op = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            yield return op;
            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError(
                    $"load scene failed,exception:{op.OperationException.Message} \r\n {op.OperationException.StackTrace}");
            }
        }

        #endregion

        #region Other

        /// <summary>
        /// 重新加载catalog
        /// Addressable初始化时热更新代码所对应的ScriptableObject的类型会被识别为System.Object，需要在热更新dll加载完后重新加载一下Addressable的Catalog
        /// https://hybridclr.doc.code-philosophy.com/docs/help/commonerrors#%E4%BD%BF%E7%94%A8addressable%E8%BF%9B%E8%A1%8C%E7%83%AD%E6%9B%B4%E6%96%B0%E6%97%B6%E5%8A%A0%E8%BD%BD%E8%B5%84%E6%BA%90%E5%87%BA%E7%8E%B0-unityengineaddressableassetsinvlidkeyexception-exception-of-type-unityengineaddressableassetsinvalidkeyexception-was-thrown-no-asset-found-with-for-key-xxxx-%E5%BC%82%E5%B8%B8
        /// </summary>
        /// <returns></returns>
        private IEnumerator ReloadAddressableCatalog()
        {
            var op = Addressables.LoadContentCatalogAsync($"{Addressables.RuntimePath}/catalog.json");
            yield return op;
            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError(
                    $"load content catalog failed, exception:{op.OperationException.Message} \r\n {op.OperationException.StackTrace}");
            }
        }

        #endregion
    }
}