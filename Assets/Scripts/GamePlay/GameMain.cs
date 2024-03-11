using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GamePlay
{
    public class GameMain : MonoBehaviour
    {
        public int fontSize = 15;
        
        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1200.0f, Screen.height / 800.0f, 1.0f));
            GUI.Label(new Rect(10, 10, 200, 100), "Hello World", new GUIStyle() { fontSize = Math.Max(10, fontSize) });
        }
    }
}