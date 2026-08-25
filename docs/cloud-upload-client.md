# PvpStatsCN 云端上传客户端

## 当前实现

- 仅上传已完整结算的纷争前线对局。
- 每场正文采用 v1 JSON，并使用 gzip 压缩。
- `X-PvPLogs-Content-SHA256` 对实际传输的 gzip 字节计算。
- 请求使用每安装实例独立密钥执行 HMAC-SHA256 签名，并包含时间戳、Nonce 和确定性幂等键。
- 安装密钥通过 Windows DPAPI（Current User）加密后才写入插件配置。
- 上传在后台队列中执行，不阻塞游戏线程；临时失败最多重试 3 次。
- 上传状态保存在 LiteDB 的 `cloud_uploads` 集合；失败或中断任务会在下次启动时补传。
- 对局正文包含三队分数变化与本人的战意变化时间线，并过滤服务端允许窗口之外的准备阶段采样。
- 云端上传和身份采集均默认关闭。

## 隐私边界

- 用户未接受云端上传协议时，行为与原版一致，不把结果包中的 Account ID、Content ID 写入对局记录。
- 用户接受协议后，新对局会保存结果包自带的 Account ID、Content ID，并在上传正文中作为 `pvp_result` 身份观察发送。
- 不上传聊天、调试日志、内存转储或逐技能原始遥测。
- 服务地址只允许 HTTPS；为本机联调允许 loopback HTTP。

## UsedName 状态

截至当前读取的 UsedName `Memory` 分支源码没有提供 Dalamud IPC/CallGate。PvpStatsCN 不读取它的私有配置文件，也不引用它的内部类型。曾用名适配继续保持可选；UsedName 后续提供稳定 IPC 后，再把数据转换为 `IdentityObservationV1`。

## 尚未开放给普通用户的部分

- UsedName 曾用名补充。
- 网站用户登录后生成绑定码的页面与设备撤销页面。

在绑定接口和用户确认界面完成前，不应手工开启 `CloudUploadEnabled`。

## 验证

```powershell
dotnet run --project tests/UploadProtocolSelfTest/UploadProtocolSelfTest.csproj -c Release

$env:DALAMUD_HOME='C:\Users\Tony\AppData\Roaming\XIVLauncherCN\addon\Hooks\dev'
dotnet build PvpStats.sln -c Release --no-restore
```

服务端 `internal/hmacauth` 同时保存相同固定测试向量，防止 C# 与 Go 的签名规范发生漂移。
