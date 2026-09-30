using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameView))]
public class GameViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty prop = serializedObject.GetIterator();
        prop.NextVisible(true); // skip the Script field
        while (prop.NextVisible(false))
        {
            EditorGUILayout.PropertyField(prop, true);
            if (prop.name == "_levelToLoad")
            {
                if (GUILayout.Button("Display Map"))
                {
                    ((GameView)target).DisplayLoadedLevel();
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
