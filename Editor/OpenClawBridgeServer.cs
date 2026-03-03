using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
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

        // Queue for main-thread execution
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

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
            Debug.Log("[OpenClaw Bridge] Stopped.");
        }

        private static void ProcessMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogError("[OpenClaw Bridge] MainThread error: " + e.Message); }
            }
        }

        private static void Listen()
        {
            while (_running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    HandleRequest(ctx);
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

            // Routes that need main thread
            bool needsMainThread = method == "POST" || path == "/scene" || path == "/hierarchy";

            if (!needsMainThread)
            {
                // Handle directly (GET /status)
                try
                {
                    var result = Route(method, path, body);
                    SendJson(res, 200, result);
                }
                catch (NotSupportedException)
                {
                    SendJson(res, 404, "{\"error\":\"not found\"}");
                }
                catch (Exception e)
                {
                    SendJson(res, 500, $"{{\"error\":\"{Escape(e.Message)}\"}}");
                }
                return;
            }

            // Dispatch to main thread, block until done
            var mre = new ManualResetEventSlim(false);
            string responseBody = "";
            int statusCode = 200;

            _mainThreadQueue.Enqueue(() =>
            {
                try
                {
                    responseBody = Route(method, path, body);
                    statusCode = 200;
                }
                catch (NotSupportedException)
                {
                    responseBody = "{\"error\":\"not found\"}";
                    statusCode = 404;
                }
                catch (Exception e)
                {
                    responseBody = $"{{\"error\":\"{Escape(e.Message)}\"}}";
                    statusCode = 500;
                }
                finally
                {
                    mre.Set();
                }
            });

            mre.Wait(5000);
            SendJson(res, statusCode, responseBody);
        }

        private static string Route(string method, string path, string body)
        {
            if (method == "GET" && path == "/status")
                return "{\"status\":\"ok\",\"version\":\"0.1.0\"}";
            if (method == "GET" && path == "/scene")
                return SceneApi.GetSceneInfo();
            if (method == "GET" && path == "/hierarchy")
                return SceneApi.GetHierarchy();
            if (method == "POST" && path == "/gameobject/create")
                return GameObjectApi.Create(body);
            if (method == "POST" && path == "/gameobject/destroy")
                return GameObjectApi.Destroy(body);
            if (method == "POST" && path == "/gameobject/component/add")
                return GameObjectApi.AddComponent(body);
            if (method == "POST" && path == "/asset/refresh")
                return AssetApi.Refresh();
            if (method == "POST" && path == "/editor/play")
                return EditorApi.Play();
            if (method == "POST" && path == "/editor/stop")
                return EditorApi.Stop();
            if (method == "POST" && path == "/editor/pause")
                return EditorApi.Pause();
            if (method == "POST" && path == "/script/create")
                return AssetApi.CreateScript(body);

            throw new NotSupportedException();
        }

        internal static void SendJson(HttpListenerResponse res, int code, string json)
        {
            try
            {
                res.StatusCode = code;
                res.ContentType = "application/json";
                var buf = Encoding.UTF8.GetBytes(json);
                res.ContentLength64 = buf.Length;
                res.OutputStream.Write(buf, 0, buf.Length);
                res.OutputStream.Close();
            }
            catch { /* client disconnected */ }
        }

        internal static string Escape(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
    }
}
