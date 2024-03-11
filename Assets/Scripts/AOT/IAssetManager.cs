using System.Collections;

namespace AOT
{
    //用于热更阶段的资源管理，游戏逻辑使用的资源加载不要使用这个接口
    public interface IAssetManager
    {
        public T LoadAsset<T>(string path);

        public void UnloadAsset(UnityEngine.Object asset);
        
        public IEnumerator CheckUpdate();

        public bool HasContentToDownload { get; }
        
        public IEnumerator DownloadAssets();
    
        public DownloadInfo GetDownloadProgress();

        public IEnumerator AfterAllDllLoaded();

        public IEnumerator ChangeScene(string sceneName);
    }
    
    public struct DownloadInfo
    {
        //progress range:[0-1]
        public float progress;
        public float downloadedBytes;
        public float totalBytes;

        public DownloadInfo(float progress, float downloadedBytes, float totalBytes)
        {
            this.progress = progress;
            this.downloadedBytes = downloadedBytes;
            this.totalBytes = totalBytes;
        }
    }
}
