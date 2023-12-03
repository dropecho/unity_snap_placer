using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dropecho {
  public static class AssetDatabaseUtils {
    public static T[] GetAssetsOfType<T>() where T : Object {
      var typeName = typeof(T).Name;

      return AssetDatabase.FindAssets("t:" + typeName)
        .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
        .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
        .ToArray();
    }

    public static T CreateAsset<T>(string path) where T : ScriptableObject {
      var instance = ScriptableObject.CreateInstance<T>();
      AssetDatabase.CreateAsset(instance, path);
      return instance;
    }
  }
}