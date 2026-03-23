using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenClaw.UnityBridge.Editor
{
    public static class SceneApi
    {
        public static string GetSceneInfo()
        {
            var scene = SceneManager.GetActiveScene();
            return $"{{\"name\":\"{scene.name}\",\"path\":\"{scene.path}\",\"isDirty\":{scene.isDirty.ToString().ToLower()},\"rootCount\":{scene.rootCount}}}";
        }

        public static string GetHierarchy()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            for (int i = 0; i < roots.Length; i++)
            {
                if (i > 0) sb.Append(",");
                AppendGO(sb, roots[i], 0);
            }
            sb.Append("]");
            return sb.ToString();
        }

        // POST /scene/save  { "path": "Assets/Scenes/Test.unity" }
        public static string Save(string body)
        {
            var data = SimpleJson.Parse(body);
            var scene = SceneManager.GetActiveScene();
            string path = data.GetValueOrDefault("path", "");

            if (!string.IsNullOrEmpty(path))
            {
                // Ensure folder exists
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                EditorSceneManager.SaveScene(scene, path);
            }
            else
            {
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.Refresh();
            var saved = SceneManager.GetActiveScene();
            return $"{{\"success\":true,\"name\":\"{saved.name}\",\"path\":\"{saved.path}\"}}";
        }

        // POST /scene/new  { "path": "Assets/Scenes/MyScene.unity" }
        public static string New(string body)
        {
            var data = SimpleJson.Parse(body);
            string path = data.GetValueOrDefault("path", "");
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(path))
            {
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                EditorSceneManager.SaveScene(newScene, path);
            }
            AssetDatabase.Refresh();
            return $"{{\"success\":true,\"name\":\"{newScene.name}\",\"path\":\"{newScene.path}\"}}";
        }

        // POST /scene/open  { "path": "Assets/Scenes/Test.unity" }
        public static string Open(string body)
        {
            var data = SimpleJson.Parse(body);
            string path = data.GetValueOrDefault("path", "");
            if (string.IsNullOrEmpty(path)) return "{\"success\":false,\"error\":\"path required\"}";
            EditorSceneManager.OpenScene(path);
            var scene = SceneManager.GetActiveScene();
            return $"{{\"success\":true,\"name\":\"{scene.name}\",\"path\":\"{scene.path}\"}}";
        }

        private static void AppendGO(System.Text.StringBuilder sb, GameObject go, int depth)
        {
            sb.Append("{");
            sb.Append($"\"name\":\"{go.name}\",\"active\":{go.activeSelf.ToString().ToLower()},\"depth\":{depth}");
            if (go.transform.childCount > 0)
            {
                sb.Append(",\"children\":[");
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    if (i > 0) sb.Append(",");
                    AppendGO(sb, go.transform.GetChild(i).gameObject, depth + 1);
                }
                sb.Append("]");
            }
            sb.Append("}");
        }
    }

    public static class GameObjectApi
    {
        public static string Create(string body)
        {
            var data = SimpleJson.Parse(body);
            string name = data.GetValueOrDefault("name", "NewGameObject");
            string primitive = data.GetValueOrDefault("primitive", "");

            GameObject go;
            if (!string.IsNullOrEmpty(primitive) &&
                System.Enum.TryParse<PrimitiveType>(primitive, true, out var pt))
            {
                go = GameObject.CreatePrimitive(pt);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
            }

            if (data.TryGetValue("parent", out string parentName))
            {
                var parent = GameObject.Find(parentName);
                if (parent != null) go.transform.SetParent(parent.transform);
            }

            // Position
            if (data.TryGetValue("position", out string posStr))
            {
                var pos = ParseVector3(posStr);
                go.transform.position = pos;
            }

            EditorUtility.SetDirty(go);
            return $"{{\"success\":true,\"name\":\"{go.name}\",\"instanceId\":{go.GetInstanceID()}}}";
        }

        public static string Destroy(string body)
        {
            var data = SimpleJson.Parse(body);
            string name = data.GetValueOrDefault("name", "");
            var go = GameObject.Find(name);
            if (go == null) return "{\"success\":false,\"error\":\"not found\"}";
            GameObject.DestroyImmediate(go);
            return "{\"success\":true}";
        }

        public static string AddComponent(string body)
        {
            var data = SimpleJson.Parse(body);
            string goName = data.GetValueOrDefault("gameObject", "");
            string componentName = data.GetValueOrDefault("component", "");
            var go = GameObject.Find(goName);
            if (go == null) return "{\"success\":false,\"error\":\"gameObject not found\"}";
            var type = System.Type.GetType(componentName) ??
                       System.Type.GetType("UnityEngine." + componentName + ", UnityEngine");
            if (type == null) return $"{{\"success\":false,\"error\":\"component type '{componentName}' not found\"}}";
            go.AddComponent(type);
            return "{\"success\":true}";
        }

        // POST /gameobject/rename  { "name": "OldName", "newName": "NewName" }
        public static string Rename(string body)
        {
            var data = SimpleJson.Parse(body);
            string name = data.GetValueOrDefault("name", "");
            string newName = data.GetValueOrDefault("newName", "");
            var go = GameObject.Find(name);
            if (go == null) return "{\"success\":false,\"error\":\"not found\"}";
            go.name = newName;
            EditorUtility.SetDirty(go);
            return "{\"success\":true}";
        }

        // POST /gameobject/setactive  { "name": "Player", "active": "false" }
        public static string SetActive(string body)
        {
            var data = SimpleJson.Parse(body);
            string name = data.GetValueOrDefault("name", "");
            bool active = data.GetValueOrDefault("active", "true").ToLower() != "false";
            var go = GameObject.Find(name);
            if (go == null) return "{\"success\":false,\"error\":\"not found\"}";
            go.SetActive(active);
            return $"{{\"success\":true,\"active\":{active.ToString().ToLower()}}}";
        }

        // POST /gameobject/transform  { "name": "Player", "position": "0,1,0", "rotation": "0,45,0", "scale": "1,1,1" }
        public static string SetTransform(string body)
        {
            var data = SimpleJson.Parse(body);
            string name = data.GetValueOrDefault("name", "");
            var go = GameObject.Find(name);
            if (go == null) return "{\"success\":false,\"error\":\"not found\"}";

            if (data.TryGetValue("position", out string pos))
                go.transform.position = ParseVector3(pos);
            if (data.TryGetValue("rotation", out string rot))
                go.transform.eulerAngles = ParseVector3(rot);
            if (data.TryGetValue("scale", out string scale))
                go.transform.localScale = ParseVector3(scale);

            EditorUtility.SetDirty(go);
            return "{\"success\":true}";
        }

        private static Vector3 ParseVector3(string s)
        {
            var parts = s.Split(',');
            if (parts.Length >= 3 &&
                float.TryParse(parts[0].Trim(), out float x) &&
                float.TryParse(parts[1].Trim(), out float y) &&
                float.TryParse(parts[2].Trim(), out float z))
                return new Vector3(x, y, z);
            return Vector3.zero;
        }
    }

    public static class AssetApi
    {
        public static string Refresh()
        {
            AssetDatabase.Refresh();
            return "{\"success\":true}";
        }

        // POST /asset/create-folder  { "path": "Assets/Scenes" }
        public static string CreateFolder(string body)
        {
            var data = SimpleJson.Parse(body);
            string path = data.GetValueOrDefault("path", "");
            if (string.IsNullOrEmpty(path)) return "{\"success\":false,\"error\":\"path required\"}";
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            return $"{{\"success\":true,\"path\":\"{path}\"}}";
        }

        public static string CreateScript(string body)
        {
            var data = SimpleJson.Parse(body);
            string fileName = data.GetValueOrDefault("fileName", "NewScript");
            string folder = data.GetValueOrDefault("folder", "Assets/Scripts");
            string content = data.GetValueOrDefault("content", $"using UnityEngine;\n\npublic class {fileName} : MonoBehaviour\n{{\n}}\n");

            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);

            string path = $"{folder}/{fileName}.cs";
            System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh();
            return $"{{\"success\":true,\"path\":\"{path}\"}}";
        }

        // POST /asset/list  { "path": "Assets" }
        public static string List(string body)
        {
            var data = SimpleJson.Parse(body);
            string path = data.GetValueOrDefault("path", "Assets");
            var guids = AssetDatabase.FindAssets("", new[] { path });
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            for (int i = 0; i < guids.Length; i++)
            {
                if (i > 0) sb.Append(",");
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                sb.Append($"\"{p}\"");
            }
            sb.Append("]");
            return sb.ToString();
        }
    }

    public static class EditorApi
    {
        public static string Play()
        {
            EditorApplication.isPlaying = true;
            return "{\"success\":true}";
        }

        public static string Stop()
        {
            EditorApplication.isPlaying = false;
            return "{\"success\":true}";
        }

        public static string Pause()
        {
            EditorApplication.isPaused = !EditorApplication.isPaused;
            return $"{{\"success\":true,\"paused\":{EditorApplication.isPaused.ToString().ToLower()}}}";
        }

        // GET /editor/status
        public static string Status()
        {
            return $"{{\"isPlaying\":{EditorApplication.isPlaying.ToString().ToLower()},\"isPaused\":{EditorApplication.isPaused.ToString().ToLower()},\"isCompiling\":{EditorApplication.isCompiling.ToString().ToLower()}}}";
        }
    }

    public static class ProjectApi
    {
        // GET /project
        public static string GetProjectInfo()
        {
            string projectPath = System.IO.Path.GetFullPath(Application.dataPath + "/..");
            string projectName = System.IO.Path.GetFileName(projectPath);
            string unityVersion = Application.unityVersion;
            return $"{{\"name\":\"{Escape(projectName)}\",\"path\":\"{Escape(projectPath)}\",\"unityVersion\":\"{unityVersion}\"}}";
        }

        private static string Escape(string s) =>
            s?.Replace("\\", "/").Replace("\"", "\\\"") ?? "";
    }

    public static class SimpleJson
    {
        public static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return result;
            json = json.Trim().Trim('{', '}');
            var pattern = new System.Text.RegularExpressions.Regex(
                "\"([^\"]+)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}]+))");
            foreach (System.Text.RegularExpressions.Match m in pattern.Matches(json))
            {
                string key = m.Groups[1].Value;
                string val = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value.Trim();
                result[key] = val;
            }
            return result;
        }
    }
}
