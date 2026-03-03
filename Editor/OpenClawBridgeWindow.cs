using UnityEditor;
using UnityEngine;

namespace OpenClaw.UnityBridge.Editor
{
    public class OpenClawBridgeSettings : ScriptableObject
    {
        private const string PrefsKeyAutoStart = "OpenClaw_AutoStart";
        private const string PrefsKeyToken = "OpenClaw_Token";

        public static bool AutoStart
        {
            get => EditorPrefs.GetBool(PrefsKeyAutoStart, true);
            set => EditorPrefs.SetBool(PrefsKeyAutoStart, value);
        }

        public static string Token
        {
            get => EditorPrefs.GetString(PrefsKeyToken, "");
            set => EditorPrefs.SetString(PrefsKeyToken, value);
        }
    }

    public class OpenClawBridgeWindow : EditorWindow
    {
        [MenuItem("Tools/OpenClaw Bridge")]
        public static void ShowWindow()
        {
            GetWindow<OpenClawBridgeWindow>("OpenClaw Bridge");
        }

        private void OnGUI()
        {
            GUILayout.Label("OpenClaw Unity Bridge", EditorStyles.boldLabel);
            GUILayout.Space(8);

            // Status
            bool running = OpenClawBridgeServer.IsRunning;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Status:", GUILayout.Width(60));
            GUI.color = running ? Color.green : Color.red;
            GUILayout.Label(running ? "● Running (port 18790)" : "● Stopped");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Auto Start
            bool autoStart = OpenClawBridgeSettings.AutoStart;
            bool newAutoStart = EditorGUILayout.Toggle("Auto Start on Load", autoStart);
            if (newAutoStart != autoStart)
                OpenClawBridgeSettings.AutoStart = newAutoStart;

            // Token
            GUILayout.Label("Auth Token (optional):");
            string token = EditorGUILayout.TextField(OpenClawBridgeSettings.Token);
            if (token != OpenClawBridgeSettings.Token)
                OpenClawBridgeSettings.Token = token;

            GUILayout.Space(12);

            // Buttons
            GUILayout.BeginHorizontal();
            if (!running && GUILayout.Button("Start"))
                OpenClawBridgeServer.Start();
            if (running && GUILayout.Button("Stop"))
                OpenClawBridgeServer.Stop();
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("API Endpoints:", EditorStyles.boldLabel);
            GUILayout.Label("GET  /status\nGET  /scene\nGET  /hierarchy\nPOST /gameobject/create\nPOST /gameobject/destroy\nPOST /gameobject/component/add\nPOST /asset/refresh\nPOST /script/create\nPOST /editor/play\nPOST /editor/stop\nPOST /editor/pause", EditorStyles.helpBox);
        }
    }
}
