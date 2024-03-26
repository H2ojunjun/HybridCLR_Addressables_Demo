using System;
using UnityEngine;

namespace GamePlay
{
    public class GameMain : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("Hello World");
            GameTest.Instance.Test();
        }
    }
}