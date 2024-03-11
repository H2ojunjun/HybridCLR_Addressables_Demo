using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AOT
{
    public class UIVersionUpdate : MonoBehaviour
    {
        const string DOWNLOAD_FORMAT = "Progress:{0}  {1}MB/{2}MB";
        
        private Slider _sliderProgress;
        private TMP_Text _textProgress;
        
        public Func<DownloadInfo> GetDownloadProgress;
        
        private void Awake()
        {
            _sliderProgress = transform.Find("slider_progress").GetComponent<Slider>();
            _textProgress = transform.Find("text_progress").GetComponent<TMP_Text>();
        }
        
        private void Update()
        {
            if (GetDownloadProgress != null)
            {
                var downloadInfo = GetDownloadProgress();
                _sliderProgress.value = downloadInfo.progress;
                _textProgress.text = string.Format(DOWNLOAD_FORMAT,downloadInfo.progress * 100,BytesToMB(downloadInfo.downloadedBytes), BytesToMB(downloadInfo.totalBytes));
            }
        }
        
        private float BytesToMB(float bytes)
        {
            return bytes / 1024 / 1024;
        }
        
        private void OnDestroy()
        {
            _sliderProgress = null;
            _textProgress = null;
            GetDownloadProgress = null;
        }
    }
}