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
    }

    public static class AssetApi
    {
        public static string Refresh()
        {
            AssetDatabase.Refresh();
            return "{\"success\":true}";
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
            System.IO.File.WriteAllText(path, content);
            AssetDatabase.Refresh();
            return $"{{\"success\":true,\"path\":\"{path}\"}}";
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
    }

    // Minimal JSON parser for simple flat key-value objects
    public static class SimpleJson
    {
        public static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return result;
            json = json.Trim().Trim('{', '}');
            // Very basic: split by "key":"value" pairs
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
