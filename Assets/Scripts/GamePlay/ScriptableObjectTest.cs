using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GamePlay
{
    public class ScriptableObjectTest : ScriptableObject
    {
        public const string TEST_OBJ_PATH = "Assets/ScriptableObjects/ScriptableObjectTest.asset";
        
        public int intValue;
        
        #if UNITY_EDITOR
        [MenuItem("Test/CreateScriptableObjectTest")]
        private static void CreateAsset()
        {
            ScriptableObjectTest asset = CreateInstance<ScriptableObjectTest>();
            AssetDatabase.CreateAsset(asset, TEST_OBJ_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        #endif
    }
}
