using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace OpenClaw.UnityBridge.Editor
{
    [InitializeOnLoad]
    public static class OpenClawBridgeServer
    {
        private static HttpListener _listener;
        private static bool _running;

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
            try
            {
                _listener.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenClaw Bridge] Failed to start: {e.Message}");
                return;
            }
            _running = true;
            BeginAccept();
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

        private static void BeginAccept()
        {
            if (!_running || _listener == null) return;
            try
            {
                _listener.BeginGetContext(OnContext, null);
            }
            catch (Exception e)
            {
                if (_running) Debug.LogError("[OpenClaw Bridge] BeginGetContext error: " + e.Message);
            }
        }

        private static void OnContext(IAsyncResult ar)
        {
            // Accept next connection immediately
            BeginAccept();

            HttpListenerContext ctx;
            try
            {
                ctx = _listener.EndGetContext(ar);
            }
            catch { return; }

            // Read body on this thread pool thread
            string body = "";
            if (ctx.Request.HasEntityBody)
            {
                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                body = reader.ReadToEnd();
            }

            string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
            string method = ctx.Request.HttpMethod.ToUpper();

            // Auth check
            var token = OpenClawBridgeSettings.Token;
            if (!string.IsNullOrEmpty(token))
            {
                var auth = ctx.Request.Headers["Authorization"] ?? "";
                if (auth != "Bearer " + token)
                {
                    SendJson(ctx.Response, 401, "{\"error\":\"unauthorized\"}");
                    return;
                }
            }

            // /status can respond immediately without main thread
            if (method == "GET" && path == "/status")
            {
                SendJson(ctx.Response, 200, "{\"status\":\"ok\",\"version\":\"0.1.0\"}");
                return;
            }

            // Everything else needs main thread
            var mre = new ManualResetEventSlim(false);
            string responseBody = "";
            int statusCode = 200;

            _mainThreadQueue.Enqueue(() =>
            {
                try
                {
                    responseBody = Route(method, path, body);
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
                finally { mre.Set(); }
            });

            mre.Wait(8000);
            SendJson(ctx.Response, statusCode, responseBody);
        }

        private static void ProcessMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogError("[OpenClaw Bridge] Main thread error: " + e); }
            }
        }

        private static string Route(string method, string path, string body)
        {
            if (method == "GET" && path == "/scene")    return SceneApi.GetSceneInfo();
            if (method == "GET" && path == "/hierarchy") return SceneApi.GetHierarchy();
            if (method == "POST" && path == "/gameobject/create")        return GameObjectApi.Create(body);
            if (method == "POST" && path == "/gameobject/destroy")       return GameObjectApi.Destroy(body);
            if (method == "POST" && path == "/gameobject/component/add") return GameObjectApi.AddComponent(body);
            if (method == "POST" && path == "/asset/refresh")            return AssetApi.Refresh();
            if (method == "POST" && path == "/editor/play")              return EditorApi.Play();
            if (method == "POST" && path == "/editor/stop")              return EditorApi.Stop();
            if (method == "POST" && path == "/editor/pause")             return EditorApi.Pause();
            if (method == "POST" && path == "/script/create")            return AssetApi.CreateScript(body);
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
            catch { }
        }

        internal static string Escape(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
    }
}
