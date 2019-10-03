using UnityEditor;

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class MeshSaver {

	public static void SaveMesh (Mesh mesh, string name) {
		string path = EditorUtility.SaveFilePanel("Save Separate Mesh Asset", "Assets/", name, "asset");
		if (string.IsNullOrEmpty(path)) return;
        
		path = FileUtil.GetProjectRelativePath(path);
        
		AssetDatabase.CreateAsset(mesh, path);
		AssetDatabase.SaveAssets();
	}
	
}