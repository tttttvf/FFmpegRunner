using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FFmpegRunner
{
    /// <summary>
    /// 全局静态配置类，用于管理 FFmpeg 执行文件路径及相关设置。
    /// </summary>
    public static class FFmpegConfig
    {
        private static string? _ffmpegPath;

        /// <summary>
        /// 获取或设置 FFmpeg 可执行文件的完整路径。
        /// 设置为 <c>null</c> 时将回退到自动探测机制。
        /// </summary>
        public static string? FFmpegPath
        {
            get => _ffmpegPath;
            set => _ffmpegPath = value;
        }

        /// <summary>
        /// 设置 FFmpeg 可执行文件的路径。
        /// </summary>
        /// <param name="path">FFmpeg 可执行文件的完整路径，或 <c>null</c> 以使用自动探测。</param>
        public static void SetFFmpegPath(string? path)
        {
            _ffmpegPath = path;
        }

        /// <summary>
        /// 获取 FFmpeg 可执行文件的路径。如果未显式设置，则尝试自动探测。
        /// 探测顺序：已设置的路径 → PATH 环境变量 → 常见安装路径。
        /// </summary>
        /// <returns>解析后的 FFmpeg 可执行文件路径。</returns>
        /// <exception cref="FileNotFoundException">当无法找到 FFmpeg 可执行文件时抛出。</exception>
        public static string GetFFmpegPath()
        {
            if (!string.IsNullOrEmpty(_ffmpegPath))
            {
                if (File.Exists(_ffmpegPath))
                    return _ffmpegPath!;

                if (TryResolveFromPath(_ffmpegPath!, out var resolved))
                    return resolved;

                throw new FileNotFoundException(
                    $"指定的 FFmpeg 路径不存在: {_ffmpegPath}");
            }

            return ResolveFFmpegPath();
        }

        /// <summary>
        /// 尝试自动探测 FFmpeg 可执行文件路径。
        /// </summary>
        private static string ResolveFFmpegPath()
        {
            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ffmpeg.exe"
                : "ffmpeg";

            if (TryResolveFromPath(executableName, out var fromPath))
                return fromPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var commonPaths = new[]
                {
                    @"C:\ffmpeg\bin\ffmpeg.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin", "ffmpeg.exe"),
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                        return path;
                }
            }
            else
            {
                var commonPaths = new[]
                {
                    "/usr/bin/ffmpeg",
                    "/usr/local/bin/ffmpeg",
                    "/opt/ffmpeg/bin/ffmpeg",
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                        return path;
                }
            }

            throw new FileNotFoundException(
                "无法找到 FFmpeg 可执行文件。请确保 FFmpeg 已安装并在 PATH 环境变量中，或通过 FFmpegConfig.SetFFmpegPath() 显式设置路径。");
        }

        /// <summary>
        /// 尝试在 PATH 环境变量中解析可执行文件。
        /// </summary>
        private static bool TryResolveFromPath(string executableName, out string resolvedPath)
        {
            resolvedPath = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT";
                var ext = Path.GetExtension(executableName);

                foreach (var dir in pathVariable.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrEmpty(dir))
                        continue;

                    if (!string.IsNullOrEmpty(ext))
                    {
                        var fullPath = Path.Combine(dir, executableName);
                        if (File.Exists(fullPath))
                        {
                            resolvedPath = fullPath;
                            return true;
                        }
                    }
                    else
                    {
                        foreach (var e in pathExt.Split(';'))
                        {
                            var fullPath = Path.Combine(dir, executableName + e.ToLowerInvariant());
                            if (File.Exists(fullPath))
                            {
                                resolvedPath = fullPath;
                                return true;
                            }
                        }
                    }
                }
            }
            else
            {
                var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

                foreach (var dir in pathVariable.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrEmpty(dir))
                        continue;

                    var fullPath = Path.Combine(dir, executableName);
                    if (File.Exists(fullPath))
                    {
                        resolvedPath = fullPath;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}