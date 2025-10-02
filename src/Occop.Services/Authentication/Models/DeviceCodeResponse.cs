using System.Text.Json.Serialization;

namespace Occop.Services.Authentication.Models
{
    /// <summary>
    /// GitHub OAuth Device Flow device code response model
    /// 表示GitHub OAuth Device Flow设备码响应
    /// </summary>
    public class DeviceCodeResponse
    {
        /// <summary>
        /// The device verification code (device_code)
        /// 设备验证码
        /// </summary>
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// The user verification code to be entered by the user
        /// 用户需要输入的验证码
        /// </summary>
        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = string.Empty;

        /// <summary>
        /// The verification URI where users should navigate to authorize
        /// 用户授权的验证URI
        /// </summary>
        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = string.Empty;

        /// <summary>
        /// Complete verification URI including the user code
        /// 包含用户码的完整验证URI
        /// </summary>
        [JsonPropertyName("verification_uri_complete")]
        public string? VerificationUriComplete { get; set; }

        /// <summary>
        /// The lifetime in seconds of the device_code and user_code
        /// 设备码和用户码的有效期（秒）
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// The minimum number of seconds that must pass before making a new access token request
        /// 轮询间隔的最小秒数
        /// </summary>
        [JsonPropertyName("interval")]
        public int Interval { get; set; }

        /// <summary>
        /// Calculates the expiration time based on current time and ExpiresIn
        /// 基于当前时间和ExpiresIn计算过期时间
        /// </summary>
        public DateTime ExpiresAt => DateTime.UtcNow.AddSeconds(ExpiresIn);

        /// <summary>
        /// Checks if the device code has expired
        /// 检查设备码是否已过期
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        /// <summary>
        /// Validates that all required fields are present and valid
        /// 验证所有必需字段是否存在且有效
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(DeviceCode) &&
            !string.IsNullOrWhiteSpace(UserCode) &&
            !string.IsNullOrWhiteSpace(VerificationUri) &&
            ExpiresIn > 0 &&
            Interval > 0;
    }
}