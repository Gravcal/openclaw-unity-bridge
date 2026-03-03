using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenClaw.UnityBridge.Editor
{
    [InitializeOnLoad]
    public static class OpenClawBridgeServer
    {
        private static HttpListener _listener;
        private static Thread _thread;
        private static bool _running;

        private const int PORT = 18790;

        static OpenClawBridgeServer()
        {
            if (OpenClawBridgeSettings.AutoStart)
                Start();
        }

        public static bool IsRunning => _running;

        public static void Start()
        {
            if (_running) return;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{PORT}/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Listen) { IsBackground = true };
            _thread.Start();
            Debug.Log($"[OpenClaw Bridge] Listening on http://127.0.0.1:{PORT}/");
        }

        public static void Stop()
        {
            if (!_running) return;
            _running = false;
            _listener?.Stop();
            _thread?.Abort();
            Debug.Log("[OpenClaw Bridge] Stopped.");
        }

        private static void Listen()
        {
            while (_running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch (Exception e)
                {
                    if (_running) Debug.LogError("[OpenClaw Bridge] " + e.Message);
                }
            }
        }

        private static void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;

            // Token auth
            var token = OpenClawBridgeSettings.Token;
            if (!string.IsNullOrEmpty(token))
            {
                var auth = req.Headers["Authorization"] ?? "";
                if (auth != "Bearer " + token)
                {
                    SendJson(res, 401, "{\"error\":\"unauthorized\"}");
                    return;
                }
            }

            string body = "";
            if (req.HasEntityBody)
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                body = reader.ReadToEnd();
            }

            string path = req.Url.AbsolutePath.TrimEnd('/');
            string method = req.HttpMethod.ToUpper();
            string response = "";

            try
            {
                response = Route(method, path, body);
                SendJson(res, 200, response);
            }
            catch (NotSupportedException)
            {
                SendJson(res, 404, "{\"error\":\"not found\"}");
            }
            catch (Exception e)
            {
                SendJson(res, 500, $"{{\"error\":\"{Escape(e.Message)}\"}}");
            }
        }

        private static string Route(string method, string path, string body)
        {
            // GET /status
            if (method == "GET" && path == "/status")
                return "{\"status\":\"ok\",\"version\":\"0.1.0\"}";

            // GET /scene
            if (method == "GET" && path == "/scene")
                return SceneApi.GetSceneInfo();

            // GET /hierarchy
            if (method == "GET" && path == "/hierarchy")
                return SceneApi.GetHierarchy();

            // POST /gameobject/create
            if (method == "POST" && path == "/gameobject/create")
                return GameObjectApi.Create(body);

            // POST /gameobject/destroy
            if (method == "POST" && path == "/gameobject/destroy")
                return GameObjectApi.Destroy(body);

            // POST /gameobject/component/add
            if (method == "POST" && path == "/gameobject/component/add")
                return GameObjectApi.AddComponent(body);

            // POST /asset/refresh
            if (method == "POST" && path == "/asset/refresh")
                return AssetApi.Refresh();

            // POST /editor/play
            if (method == "POST" && path == "/editor/play")
                return EditorApi.Play();

            // POST /editor/stop
            if (method == "POST" && path == "/editor/stop")
                return EditorApi.Stop();

            // POST /editor/pause
            if (method == "POST" && path == "/editor/pause")
                return EditorApi.Pause();

            // POST /script/create
            if (method == "POST" && path == "/script/create")
                return AssetApi.CreateScript(body);

            throw new NotSupportedException();
        }

        private static void SendJson(HttpListenerResponse res, int code, string json)
        {
            res.StatusCode = code;
            res.ContentType = "application/json";
            var buf = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = buf.Length;
            res.OutputStream.Write(buf, 0, buf.Length);
            res.OutputStream.Close();
        }

        private static string Escape(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
    }
}
