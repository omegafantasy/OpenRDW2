using UnityEditor;
using UnityEngine;
using System.IO;

[CustomEditor(typeof(BatchExperimentGenerator))]
public class BatchExperimentGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BatchExperimentGenerator script = (BatchExperimentGenerator)target;
        EditorGUI.BeginChangeCheck();
        drawProperty("filepath", "Batch File Path");
        drawProperty("savepath", "Save Directory Path");
        drawProperty("saveName", "Save File Name");
        bool inputPath = GUILayout.Button("Choose Batch File Path", GUILayout.ExpandWidth(true));
        bool outputPath = GUILayout.Button("Choose Save Directory Path", GUILayout.ExpandWidth(true));
        bool generate = GUILayout.Button("Generate", GUILayout.ExpandWidth(true));

        if (inputPath)
        {
            script.filepath = EditorUtility.OpenFilePanel("Choose Batch File", "", "txt");
            if (script.filepath != "")
            {
                var paths = script.filepath.Split('/');
                string name = paths[paths.Length - 1];
                name = name.Replace(".txt", "");
                script.saveName = name;
            }
        }
        if (outputPath)
        {
            script.savepath = EditorUtility.OpenFolderPanel("Choose Save Directory", "", "");
        }

        if (generate)
        {
            script.readFileAndGenerate();
        }
        if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();
    }

    private void drawProperty(string property, string label)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label), false);
    }

}