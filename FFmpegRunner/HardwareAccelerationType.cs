namespace FFmpegRunner
{
    /// <summary>
    /// FFmpeg 硬件加速方式枚举。
    /// 用于 <see cref="InputOptions.WithHardwareAcceleration(HardwareAccelerationType)"/>。
    /// </summary>
    public enum HardwareAccelerationType
    {
        /// <summary>自动选择（-hwaccel auto）</summary>
        Auto,

        /// <summary>VDPAU（Linux/NVIDIA）</summary>
        Vdpau,

        /// <summary>DXVA2（Windows 旧式）</summary>
        Dxva2,

        /// <summary>D3D11VA（Windows Direct3D 11）</summary>
        D3d11va,

        /// <summary>VAAPI（Linux/Intel/AMD）</summary>
        Vaapi,

        /// <summary>QSV（Intel Quick Sync Video）</summary>
        Qsv,

        /// <summary>AMF（AMD）</summary>
        Amf,
    }
}