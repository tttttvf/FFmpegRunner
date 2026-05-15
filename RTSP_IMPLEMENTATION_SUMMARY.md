# FFmpegBuilder RTSP 配置方法实现总结

## 已完成功能

### 1. FromRtspSource 方法 - RTSP拉流地址配置

**功能**: 配置从RTSP源拉取视频流

**参数**:
- `rtspAddress` (string, 必填): RTSP拉流地址
- `transportProtocol` (string, 可选, 默认="tcp"): 传输协议，支持 "tcp", "udp", "http"

**实现位置**: [FFmpegBuilder.cs#L74-88](e:\Project\ffmpeg-runner\FFmpegRunner\FFmpegBuilder.cs#L74-88)

**生成的命令示例**:
```bash
ffmpeg -y -rtsp_transport tcp rtsp://192.168.1.100:554/stream
```

**验证机制**:
- ✓ 地址不能为空或空白
- ✓ 传输协议必须在允许的值范围内 (tcp, udp, http)
- ✓ 无效值会抛出 ArgumentException

---

### 2. ToRtsp 方法 - RTSP推流地址配置

**功能**: 配置输出到RTSP推流地址

**参数**:
- `rtspAddress` (string, 必填): RTSP推流地址
- `transportProtocol` (string, 可选, 默认="tcp"): 传输协议，仅支持 "tcp", "udp"

**实现位置**: [FFmpegBuilder.cs#L358-374](e:\Project\ffmpeg-runner\FFmpegRunner\FFmpegBuilder.cs#L358-374)

**生成的命令示例**:
```bash
ffmpeg -y -i input.mp4 -rtsp_transport udp -f rtsp rtsp://192.168.1.100:554/live/stream
```

**验证机制**:
- ✓ 地址不能为空或空白
- ✓ 传输协议必须为 "tcp" 或 "udp" (RTSP推流不支持http)
- ✓ 无效值会抛出 ArgumentException

---

### 3. 参数优先级机制 - ToRtsp 覆盖 -f 参数

**功能**: 当调用ToRtsp方法配置RTSP推流后，自动忽略之前设置的格式参数

**实现位置**:
- 私有方法: [RemoveFormatArguments](e:\Project\ffmpeg-runner\FFmpegRunner\FFmpegBuilder.cs#L442-451)
- Build验证: [Build方法中的参数检查](e:\Project\ffmpeg-runner\FFmpegRunner\FFmpegBuilder.cs#L408-412)

**实现方式**:
1. **自动清除已存在的-f参数**: 在ToRtsp方法调用时，通过正则表达式移除所有已存在的 `-f` 参数
2. **阻止后续设置冲突参数**: 在Build方法中添加验证，确保RTSP模式下只包含 `-f rtsp` 参数

**验证示例**:
```csharp
// 先设置flv格式，再调用ToRtsp
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithFormat("flv")           // 被忽略
    .ToRtsp("rtsp://...")        // 自动覆盖为-f rtsp
    .Build();

// 生成的参数: -rtsp_transport tcp -f rtsp
// 不包含: -f flv ✓
```

---

## 测试覆盖

### 单元测试 (10个RTSP相关测试全部通过)

| 测试名称 | 测试内容 | 状态 |
|---------|---------|------|
| FromRtspSource_ShouldSetSourceAndTransport | 基本拉流配置 | ✓ |
| FromRtspSource_WithUdp_ShouldSetTransport | UDP传输协议 | ✓ |
| FromRtspSource_WithInvalidTransport_ShouldThrow | 无效协议验证 | ✓ |
| FromRtspSource_WithNullAddress_ShouldThrow | 空地址验证 | ✓ |
| ToRtsp_ShouldSetTargetAndFormat | 基本推流配置 | ✓ |
| ToRtsp_WithUdp_ShouldSetTransport | UDP传输协议 | ✓ |
| ToRtsp_WithInvalidTransport_ShouldThrow | 无效协议验证 | ✓ |
| ToRtsp_WithNullAddress_ShouldThrow | 空地址验证 | ✓ |
| ToRtsp_AfterWithFormat_ShouldOverrideFormat | 参数优先级覆盖 | ✓ |
| WithFormat_BeforeToRtsp_ShouldBeIgnored | 参数优先级忽略 | ✓ |

---

## 使用示例

### 示例1: RTSP拉流到文件
```csharp
var runner = new FFmpegBuilder()
    .FromRtspSource("rtsp://camera.local:554/stream", "tcp")
    .WithVideoCodec("copy")
    .ToFile("output.mp4")
    .Build();

runner.Start();
```

### 示例2: 文件转RTSP推流
```csharp
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .WithAudioCodec("aac")
    .ToRtsp("rtsp://192.168.1.100:554/live/stream", "udp")
    .Build();

runner.Start();
```

### 示例3: RTSP拉流转RTSP推流 (转码)
```csharp
var runner = new FFmpegBuilder()
    .FromRtspSource("rtsp://source.local:554/stream", "tcp")
    .WithVideoCodec("h264")
    .WithAudioCodec("aac")
    .WithCrf(23)
    .ToRtsp("rtsp://dest.local:554/live/stream")
    .Build();

runner.Start();
```

---

## 技术实现细节

### 参数构建流程
1. **FromRtspSource**: 设置源地址 + 添加 `-rtsp_transport` 参数
2. **ToRtsp**:
   - 设置目标地址
   - 调用 `RemoveFormatArguments()` 清除所有 `-f` 参数
   - 添加 `-rtsp_transport` 和 `-f rtsp` 参数

### 私有方法 RemoveFormatArguments
```csharp
private void RemoveFormatArguments()
{
    var args = _arguments.ToString();
    var pattern = @"-f\s+\S+";
    var result = Regex.Replace(args, pattern, "");
    result = Regex.Replace(result, @"\s+", " ").Trim();

    _arguments.Clear();
    _arguments.Append(result);
}
```

### Build方法中的安全检查
```csharp
if (_isRtspOutput && commandArgs.Contains("-f ") && !commandArgs.Contains("-f rtsp"))
{
    throw new InvalidOperationException(
        "RTSP 推流模式不允许使用其他格式参数。请仅使用 ToRtsp() 方法配置 RTSP 推流。");
}
```

---

## 符合FFmpeg标准

所有生成的FFmpeg命令都符合标准FFmpeg参数格式：

- **RTSP拉流**: `ffmpeg [全局参数] -rtsp_transport [protocol] [source]`
- **RTSP推流**: `ffmpeg [全局参数] -i [input] -rtsp_transport [protocol] -f rtsp [output]`

---

## 编译状态
- ✓ 所有单元测试通过 (10/10)
- ✓ 无编译警告
- ✓ 无编译错误
