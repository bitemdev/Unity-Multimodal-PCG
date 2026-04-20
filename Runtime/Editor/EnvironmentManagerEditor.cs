using UnityEngine;
using UnityEditor;
using PCG.Modules.Environment;

namespace PCG.Editor
{
    [CustomEditor(typeof(EnvironmentManager))]
    public class EnvironmentManagerEditor : UnityEditor.Editor
    {
        private EnvironmentManager _environmentManager;
        private GUIStyle _mainBodyStyle;
        private GUIStyle _headerStyle;
        private Texture2D _backgroundTexture;

        private void OnEnable()
        {
            _environmentManager = (EnvironmentManager)target; // Cache reference to edited script

            _backgroundTexture = CreateTexture(1, 1, new Color(0f, 0.121f, 0.247f));
        }

        private void OnDisable()
        {
            if (_backgroundTexture != null)
            {
                DestroyImmediate(_backgroundTexture);
            }
        }

        /// <summary>
        /// This method creates a custom inspector
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (_mainBodyStyle == null)
            {
                _mainBodyStyle = new GUIStyle(EditorStyles.helpBox);
                _mainBodyStyle.normal.background = _backgroundTexture;
                _mainBodyStyle.border = new RectOffset(0, 0, 0, 0);
                _mainBodyStyle.padding = new RectOffset(15, 15, 15, 15);
                _mainBodyStyle.margin = new RectOffset(0, 0, 0, 0);
                _mainBodyStyle.normal.textColor = new Color(0.968f, 0.905f, 0.807f);
            }

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.968f, 0.905f, 0.807f) }
                };
            }

            EditorGUILayout.BeginVertical(_mainBodyStyle);

            // Header
            EditorGUILayout.LabelField("PCG FRAMEWORK - ENVIRONMENT MANAGER", _headerStyle);
            EditorGUILayout.Space(10);

            Color lineColor = new Color(0.968f, 0.905f, 0.807f);
            DrawSeparator(lineColor);

            EditorGUILayout.Space(10);

            // Real-time validation
            serializedObject.Update();
            SerializedProperty configProp = serializedObject.FindProperty("_config");

            if (configProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("PCGConfiguration is missing! Please assign a valid configuration asset.", MessageType.Warning);
                EditorGUILayout.PropertyField(configProp);
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                // State panel
                SerializedProperty widthProp = GetConfigProperty(configProp, "_width");
                SerializedProperty heightProp = GetConfigProperty(configProp, "_height");

                bool isConfigValid = true;
                string errorMessage = "";

                if (widthProp != null && heightProp != null)
                {
                    int width = widthProp.intValue;
                    int height = heightProp.intValue;

                    if (width < 20 || height < 20)
                    {
                        isConfigValid = false;
                        errorMessage = "Map dimensions are too small (min 20x20). Generation unsafe.";
                    }
                }

                if (isConfigValid)
                {
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                    EditorGUILayout.HelpBox("STATUS: READY TO GENERATE", MessageType.Info);
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    EditorGUILayout.HelpBox($"STATUS: INVALID CONFIGURATION\n{errorMessage}", MessageType.Error);
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.Space(5);

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = isConfigValid;

                if (GUILayout.Button("GENERATE LEVEL", GUILayout.Height(30)))
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    sw.Start();

                    _environmentManager.GenerateLevel();

                    sw.Stop();
                    Debug.Log($"[Editor] Generation request time: {sw.Elapsed.TotalMilliseconds:F2}ms");
                }

                GUI.enabled = true;

                if (GUILayout.Button("CLEAR MEMORY", GUILayout.Height(30)))
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    sw.Start();

                    _environmentManager.ClearMemory();

                    sw.Stop();
                    Debug.Log($"[Editor] Memory cleared");
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);
                DrawSeparator(lineColor);
            }

            EditorGUILayout.Space(10);
            // Default inspector
            EditorGUILayout.LabelField("INSPECTOR PROPERTIES", EditorStyles.boldLabel);
            base.OnInspectorGUI(); // Draw the rest

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties(); // Save changes
        }

        /// <summary>
        /// This method creates and draw a line separator in inspector
        /// </summary>
        private void DrawSeparator(Color color)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, color);
        }

        /// <summary>
        /// This method returns the data values of a ScriptableObject
        /// </summary>
        private SerializedProperty GetConfigProperty(SerializedProperty configProp, string propName)
        {
            if (configProp.objectReferenceValue == null)
            {
                return null;
            }

            // Create a temporal SerializedObject of the ScriptableObject to get its values
            SerializedObject configObject = new SerializedObject(configProp.objectReferenceValue);
            return configObject.FindProperty(propName);
        }

        /// <summary>
        /// This method creates a 2D texture
        /// </summary>
        private Texture2D CreateTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = color;
            }

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
