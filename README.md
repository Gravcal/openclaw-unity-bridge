# OpenClaw Unity Bridge

Unity Editor 插件，为 [OpenClaw](https://openclaw.ai) AI 助手提供本地 HTTP API，实现 AI 直接控制 Unity 编辑器。

## 功能

- 查询场景信息 & Hierarchy
- 创建 / 删除 GameObject
- 添加组件
- 创建 C# 脚本
- 刷新 AssetDatabase
- 控制播放 / 暂停 / 停止

## 安装

### 方式一：Package Manager（推荐）

在 Unity `Package Manager` 中选择 **Add package from git URL**：

```
https://github.com/Gravex/openclaw-unity-bridge.git
```

### 方式二：本地磁盘

下载后在 Package Manager 中选择 **Add package from disk**，选中 `package.json`。

## 使用

1. 打开 `Tools > OpenClaw Bridge`
2. 勾选 **Auto Start on Load**（推荐）
3. 可选填写 **Auth Token** 用于鉴权
4. 点击 **Start** 启动服务（默认监听 `http://127.0.0.1:18790/`）

## API 文档

所有请求均为 JSON 格式，Header 需加 `Authorization: Bearer <token>`（如配置了 token）。

| Method | Path | 说明 |
|--------|------|------|
| GET | `/status` | 检查服务状态 |
| GET | `/scene` | 获取当前场景信息 |
| GET | `/hierarchy` | 获取 Hierarchy 树 |
| POST | `/gameobject/create` | 创建 GameObject |
| POST | `/gameobject/destroy` | 删除 GameObject |
| POST | `/gameobject/component/add` | 添加组件 |
| POST | `/script/create` | 创建 C# 脚本 |
| POST | `/asset/refresh` | 刷新 AssetDatabase |
| POST | `/editor/play` | 进入 Play 模式 |
| POST | `/editor/stop` | 退出 Play 模式 |
| POST | `/editor/pause` | 暂停 / 继续 |

### 示例

```json
// POST /gameobject/create
{ "name": "Player", "primitive": "Capsule" }

// POST /script/create
{ "fileName": "PlayerController", "folder": "Assets/Scripts" }

// POST /gameobject/component/add
{ "gameObject": "Player", "component": "Rigidbody" }
```

## 兼容性

- Unity 2022.3+（含团结引擎 1.x）
- Editor Only，不影响运行时构建

## License

MIT
