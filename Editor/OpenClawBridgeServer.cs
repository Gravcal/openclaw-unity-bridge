using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace OpenClaw.UnityBridge.Editor
{
    [InitializeOnLoad]
    public static class OpenClawBridgeServer
    {
        private static TcpListener _listener;
        private static Thread _thread;
        private static bool _running;
        private static string _cachedToken = "";

        private static readonly ConcurrentQueue<(string method, string path, string body, Action<int,string> reply)>
            _mainThreadQueue = new ConcurrentQueue<(string, string, string, Action<int,string>)>();

        private const int PORT = 18790;

        static OpenClawBridgeServer()
        {
            EditorApplication.update += ProcessMainThreadQueue;
            if (OpenClawBridgeSettings.AutoStart)
                Start();
        }

        public static bool IsRunning => _running;

        public static void Start()
        {
            if (_running) return;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, PORT);
                _listener.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenClaw Bridge] Failed to start: {e.Message}");
                return;
            }
            _running = true;
            _cachedToken = OpenClawBridgeSettings.Token;
            _thread = new Thread(Listen) { IsBackground = true, Name = "OpenClawBridge" };
            _thread.Start();
            Debug.Log($"[OpenClaw Bridge] Listening on http://127.0.0.1:{PORT}/");
        }

        public static void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener?.Stop(); } catch { }
            _listener = null;
            Debug.Log("[OpenClaw Bridge] Stopped.");
        }

        private static void Listen()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (Exception e)
                {
                    if (_running) Debug.LogError("[OpenClaw Bridge] Accept error: " + e.Message);
                }            }
        }

        private static void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = false };

                    // Read request line
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine)) return;

                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;
                    string method = parts[0].ToUpper();
                    string path = parts[1].Split('?')[0].TrimEnd('/');
                    if (string.IsNullOrEmpty(path)) path = "/";

                    // Read headers
                    int contentLength = 0;
                    string authHeader = "";
                    string line;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(line.Substring(15).Trim(), out contentLength);
                        if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                            authHeader = line.Substring(14).Trim();
                    }

                    // Auth check
                    var token = _cachedToken;
                    if (!string.IsNullOrEmpty(token) && authHeader != "Bearer " + token)
                    {
                        SendResponse(writer, 401, "{\"error\":\"unauthorized\"}");
                        return;
                    }

                    // Read body
                    string body = "";
                    if (contentLength > 0)
                    {
                        var buf = new char[contentLength];
                        reader.Read(buf, 0, contentLength);
                        body = new string(buf);
                    }

                    // /status — no main thread needed
                    if (method == "GET" && path == "/status")
                    {
                        SendResponse(writer, 200, "{\"status\":\"ok\",\"version\":\"0.1.1\"}");
                        return;
                    }

                    // Dispatch to main thread
                    var mre = new ManualResetEventSlim(false);
                    int statusCode = 200;
                    string responseBody = "";

                    _mainThreadQueue.Enqueue((method, path, body, (code, resp) =>
                    {
                        statusCode = code;
                        responseBody = resp;
                        mre.Set();
                    }));

                    if (mre.Wait(8000))
                        SendResponse(writer, statusCode, responseBody);
                    else
                        SendResponse(writer, 504, "{\"error\":\"main thread timeout\"}");
                }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogError("[OpenClaw Bridge] HandleClient error: " + e.Message);
            }
        }

        private static void ProcessMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var item))
            {
                var (method, path, body, reply) = item;
                try
                {
                    var result = Route(method, path, body);
                    reply(200, result);
                }
                catch (NotSupportedException)
                {
                    reply(404, "{\"error\":\"not found\"}");
                }
                catch (Exception e)
                {
                    reply(500, $"{{\"error\":\"{Escape(e.Message)}\"}}");
                }
            }
        }

        private static string Route(string method, string path, string body)
        {
            if (method == "GET"  && path == "/project")                    return ProjectApi.GetProjectInfo();
            if (method == "GET"  && path == "/scene")                      return SceneApi.GetSceneInfo();
            if (method == "GET"  && path == "/hierarchy")                  return SceneApi.GetHierarchy();
            if (method == "POST" && path == "/scene/save")                 return SceneApi.Save(body);
            if (method == "POST" && path == "/scene/new")                  return SceneApi.New(body);
            if (method == "POST" && path == "/scene/open")                 return SceneApi.Open(body);
            if (method == "GET"  && path == "/editor/status")              return EditorApi.Status();
            if (method == "POST" && path == "/gameobject/create")          return GameObjectApi.Create(body);
            if (method == "POST" && path == "/gameobject/destroy")         return GameObjectApi.Destroy(body);
            if (method == "POST" && path == "/gameobject/component/add")   return GameObjectApi.AddComponent(body);
            if (method == "POST" && path == "/gameobject/rename")          return GameObjectApi.Rename(body);
            if (method == "POST" && path == "/gameobject/setactive")       return GameObjectApi.SetActive(body);
            if (method == "POST" && path == "/gameobject/transform")       return GameObjectApi.SetTransform(body);
            if (method == "POST" && path == "/asset/refresh")              return AssetApi.Refresh();
            if (method == "POST" && path == "/asset/create-folder")        return AssetApi.CreateFolder(body);
            if (method == "POST" && path == "/asset/list")                 return AssetApi.List(body);
            if (method == "POST" && path == "/script/create")              return AssetApi.CreateScript(body);
            if (method == "POST" && path == "/editor/play")                return EditorApi.Play();
            if (method == "POST" && path == "/editor/stop")                return EditorApi.Stop();
            if (method == "POST" && path == "/editor/pause")               return EditorApi.Pause();
            throw new NotSupportedException();
        }

        private static void SendResponse(StreamWriter writer, int code, string json)
        {
            string status = code switch { 200 => "OK", 401 => "Unauthorized", 404 => "Not Found", 500 => "Internal Server Error", 504 => "Gateway Timeout", _ => "OK" };
            var bytes = Encoding.UTF8.GetBytes(json);
            writer.WriteLine($"HTTP/1.1 {code} {status}");
            writer.WriteLine("Content-Type: application/json; charset=utf-8");
            writer.WriteLine($"Content-Length: {bytes.Length}");
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Flush();
            writer.BaseStream.Write(bytes, 0, bytes.Length);
            writer.BaseStream.Flush();
        }

        internal static string Escape(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
    }
}
