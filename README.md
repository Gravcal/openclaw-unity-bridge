# OpenClaw Unity Bridge

Unity Editor 鎻掍欢锛屼负 [OpenClaw](https://openclaw.ai) AI 鍔╂墜鎻愪緵鏈湴 HTTP API锛屽疄鐜?AI 鐩存帴鎺у埗 Unity 缂栬緫鍣ㄣ€?
## 鍔熻兘

- 鏌ヨ鍦烘櫙淇℃伅 & Hierarchy
- 鍒涘缓 / 鍒犻櫎 GameObject
- 娣诲姞缁勪欢
- 鍒涘缓 C# 鑴氭湰
- 鍒锋柊 AssetDatabase
- 鎺у埗鎾斁 / 鏆傚仠 / 鍋滄

## 瀹夎

### 鏂瑰紡涓€锛歅ackage Manager锛堟帹鑽愶級

鍦?Unity `Package Manager` 涓€夋嫨 **Add package from git URL**锛?
```
https://github.com/Gravcal/openclaw-unity-bridge.git
```

### 鏂瑰紡浜岋細鏈湴纾佺洏

涓嬭浇鍚庡湪 Package Manager 涓€夋嫨 **Add package from disk**锛岄€変腑 `package.json`銆?
## 浣跨敤

1. 鎵撳紑 `Tools > OpenClaw Bridge`
2. 鍕鹃€?**Auto Start on Load**锛堟帹鑽愶級
3. 鍙€夊～鍐?**Auth Token** 鐢ㄤ簬閴存潈
4. 鐐瑰嚮 **Start** 鍚姩鏈嶅姟锛堥粯璁ょ洃鍚?`http://127.0.0.1:18790/`锛?
## API 鏂囨。

鎵€鏈夎姹傚潎涓?JSON 鏍煎紡锛孒eader 闇€鍔?`Authorization: Bearer <token>`锛堝閰嶇疆浜?token锛夈€?
| Method | Path | 璇存槑 |
|--------|------|------|
| GET | `/status` | 妫€鏌ユ湇鍔＄姸鎬?|
| GET | `/scene` | 鑾峰彇褰撳墠鍦烘櫙淇℃伅 |
| GET | `/hierarchy` | 鑾峰彇 Hierarchy 鏍?|
| POST | `/gameobject/create` | 鍒涘缓 GameObject |
| POST | `/gameobject/destroy` | 鍒犻櫎 GameObject |
| POST | `/gameobject/component/add` | 娣诲姞缁勪欢 |
| POST | `/script/create` | 鍒涘缓 C# 鑴氭湰 |
| POST | `/asset/refresh` | 鍒锋柊 AssetDatabase |
| POST | `/editor/play` | 杩涘叆 Play 妯″紡 |
| POST | `/editor/stop` | 閫€鍑?Play 妯″紡 |
| POST | `/editor/pause` | 鏆傚仠 / 缁х画 |

### 绀轰緥

```json
// POST /gameobject/create
{ "name": "Player", "primitive": "Capsule" }

// POST /script/create
{ "fileName": "PlayerController", "folder": "Assets/Scripts" }

// POST /gameobject/component/add
{ "gameObject": "Player", "component": "Rigidbody" }
```

## 鍏煎鎬?
- Unity 2022.3+锛堝惈鍥㈢粨寮曟搸 1.x锛?- Editor Only锛屼笉褰卞搷杩愯鏃舵瀯寤?
## License

MIT

