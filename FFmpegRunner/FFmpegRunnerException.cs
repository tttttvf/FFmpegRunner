using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FFmpegRunner
{
    /// <summary>
    /// FFmpegRunner 业务逻辑异常基类。用于统一封装和传递当前 NuGet 包中的所有业务异常。
    /// </summary>
    /// <remarks>
    /// <para>设计遵循 .NET 异常处理最佳实践：</para>
    /// <list type="bullet">
    ///   <item>继承自 <see cref="Exception"/>，兼容现有异常处理基础架构</item>
    ///   <item>提供 <see cref="ErrorCode"/> 用于程序化识别异常类型</item>
    ///   <item>提供 <see cref="ContextData"/> 字典传递附加上下文信息，便于问题定位</item>
    ///   <item>支持序列化（<see cref="SerializableAttribute"/>），确保跨 AppDomain 可用</item>
    ///   <item>提供标准构造函数重载，与 <see cref="Exception"/> 使用模式一致</item>
    /// </list>
    /// </remarks>
    [Serializable]
    public class FFmpegRunnerException : Exception
    {
        private const string ErrorCodeKey = "FFmpegRunner.ErrorCode";
        private const string ContextDataKey = "FFmpegRunner.ContextData";

        /// <summary>
        /// 获取异常错误代码，用于程序化识别异常类型。
        /// </summary>
        /// <example>
        /// <code>
        /// try { ... }
        /// catch (FFmpegRunnerException ex) when (ex.ErrorCode == "FFMPEG_NOT_FOUND") { ... }
        /// </code>
        /// </example>
        public string ErrorCode { get; }

        /// <summary>
        /// 获取异常附加上下文数据，用于问题诊断和日志记录。
        /// </summary>
        /// <remarks>
        /// 建议包含的关键信息：源路径、目标路径、FFmpeg 参数、进程退出码等。
        /// </remarks>
        public IReadOnlyDictionary<string, object?> ContextData { get; }

        /// <summary>
        /// 初始化 <see cref="FFmpegRunnerException"/> 类的新实例。
        /// </summary>
        public FFmpegRunnerException()
            : this("FFmpegRunner 发生了一个错误。")
        {
        }

        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="FFmpegRunnerException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public FFmpegRunnerException(string? message)
            : this(message, null, null)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和错误代码初始化 <see cref="FFmpegRunnerException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="errorCode">错误代码，用于程序化识别异常类型。</param>
        public FFmpegRunnerException(string? message, string? errorCode)
            : this(message, errorCode, null, null)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="FFmpegRunnerException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的原始异常。</param>
        public FFmpegRunnerException(string? message, Exception? innerException)
            : this(message, null, null, innerException)
        {
        }

        /// <summary>
        /// 使用指定的错误消息、错误代码和上下文数据初始化 <see cref="FFmpegRunnerException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="errorCode">错误代码，用于程序化识别异常类型。</param>
        /// <param name="contextData">附加上下文数据字典，用于问题诊断。数据会被复制为只读副本。</param>
        public FFmpegRunnerException(string? message, string? errorCode, IDictionary<string, object?>? contextData)
            : this(message, errorCode, contextData, null)
        {
        }

        /// <summary>
        /// 使用全部参数初始化 <see cref="FFmpegRunnerException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="errorCode">错误代码，用于程序化识别异常类型。</param>
        /// <param name="contextData">附加上下文数据字典，用于问题诊断。数据会被复制为只读副本。</param>
        /// <param name="innerException">导致当前异常的原始异常。</param>
        public FFmpegRunnerException(
            string? message,
            string? errorCode,
            IDictionary<string, object?>? contextData,
            Exception? innerException)
            : base(message ?? "FFmpegRunner 发生了一个错误。", innerException)
        {
            ErrorCode = errorCode ?? "UNKNOWN";

            if (contextData != null && contextData.Count > 0)
            {
                var copy = new Dictionary<string, object?>(contextData, StringComparer.OrdinalIgnoreCase);
                ContextData = copy;
            }
            else
            {
                ContextData = new Dictionary<string, object?>();
            }
        }

        /// <summary>
        /// 使用序列化数据初始化 <see cref="FFmpegRunnerException"/> 类的新实例。
        /// </summary>
        /// <param name="info">保存序列化对象数据的 <see cref="SerializationInfo"/>。</param>
        /// <param name="context">有关源或目标的上下文信息。</param>
#pragma warning disable SYSLIB0051, CS0672
        protected FFmpegRunnerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorCode = info.GetString(ErrorCodeKey) ?? "UNKNOWN";

            var contextData = (Dictionary<string, object?>)info.GetValue(
                ContextDataKey, typeof(Dictionary<string, object?>))!;

            ContextData = contextData ?? new Dictionary<string, object?>();
        }

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            base.GetObjectData(info, context);

            info.AddValue(ErrorCodeKey, ErrorCode);

            var dict = new Dictionary<string, object?>();
            foreach (var kvp in ContextData)
                dict[kvp.Key] = kvp.Value;
            info.AddValue(ContextDataKey, dict);
        }
#pragma warning restore SYSLIB0051, CS0672

        /// <inheritdoc />
        public override string ToString()
        {
            var baseStr = base.ToString();
            var extra = $"[ErrorCode: {ErrorCode}, ContextData entries: {ContextData.Count}]";
            return $"{baseStr}{Environment.NewLine}{extra}";
        }
    }
}