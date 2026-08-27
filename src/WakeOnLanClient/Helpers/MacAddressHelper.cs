// AI-code-start lines:71 tool:cursor ai生成
using System;
using System.Globalization;

namespace WakeOnLanClient.Helpers
{
    /// <summary>
    /// MAC 地址解析与校验工具。
    /// </summary>
    public static class MacAddressHelper
    {
        /// <summary>
        /// 将常见格式的 MAC 地址解析为 6 字节。
        /// 支持 AA:BB:CC:DD:EE:FF、AA-BB-CC-DD-EE-FF、AABBCCDDEEFF。
        /// </summary>
        /// <param name="macAddress">原始 MAC 字符串。</param>
        /// <returns>6 字节 MAC。</returns>
        /// <exception cref="ArgumentException">格式非法时抛出。</exception>
        public static byte[] Parse(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
            {
                throw new ArgumentException("MAC 地址不能为空。", nameof(macAddress));
            }

            var normalized = macAddress.Trim()
                .Replace(":", string.Empty)
                .Replace("-", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", string.Empty);

            if (normalized.Length != 12)
            {
                throw new ArgumentException($"MAC 地址格式无效: {macAddress}", nameof(macAddress));
            }

            for (var index = 0; index < normalized.Length; index++)
            {
                if (!Uri.IsHexDigit(normalized[index]))
                {
                    throw new ArgumentException($"MAC 地址包含非法字符: {macAddress}", nameof(macAddress));
                }
            }

            var bytes = new byte[6];
            for (var index = 0; index < 6; index++)
            {
                bytes[index] = byte.Parse(normalized.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        /// <summary>
        /// 简单校验 MAC 字符串是否可解析。
        /// </summary>
        public static bool IsValid(string macAddress)
        {
            try
            {
                Parse(macAddress);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
// AI-code-end