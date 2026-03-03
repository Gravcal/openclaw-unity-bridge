# Changelog

## [0.1.0] - 2026-03-03

### Added
- Initial release
- HTTP API server (port 18790) with auto-start on Editor load
- Editor window: `Tools > OpenClaw Bridge`
- API: GET /status, /scene, /hierarchy
- API: POST /gameobject/create, /gameobject/destroy, /gameobject/component/add
- API: POST /script/create, /asset/refresh
- API: POST /editor/play, /editor/stop, /editor/pause
- Optional Bearer token authentication
