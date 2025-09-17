using System.Text.Json.Serialization;

namespace Occop.Services.Authentication.Models
{
    /// <summary>
    /// GitHub OAuth access token response model
    /// 表示GitHub OAuth访问令牌响应
    /// </summary>
    public class AccessTokenResponse
    {
        /// <summary>
        /// The OAuth access token
        /// OAuth访问令牌
        /// </summary>
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// The type of token (usually "bearer")
        /// 令牌类型（通常是"bearer"）
        /// </summary>
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated list of scopes the access token is authorized for
        /// 访问令牌授权的范围列表（逗号分隔）
        /// </summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// Error code if the request failed
        /// 请求失败时的错误代码
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Human-readable error description
        /// 人类可读的错误描述
        /// </summary>
        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// Error URI with more information about the error
        /// 包含错误详细信息的URI
        /// </summary>
        [JsonPropertyName("error_uri")]
        public string? ErrorUri { get; set; }

        /// <summary>
        /// Indicates if the response represents a successful token grant
        /// 指示响应是否表示成功的令牌授权
        /// </summary>
        public bool IsSuccess => string.IsNullOrWhiteSpace(Error) && !string.IsNullOrWhiteSpace(AccessToken);

        /// <summary>
        /// Indicates if the authorization is still pending
        /// 指示授权是否仍在等待中
        /// </summary>
        public bool IsAuthorizationPending =>
            string.Equals(Error, "authorization_pending", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if the polling is too slow (need to reduce frequency)
        /// 指示轮询过慢（需要降低频率）
        /// </summary>
        public bool IsSlowDown =>
            string.Equals(Error, "slow_down", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if the device code has expired
        /// 指示设备码已过期
        /// </summary>
        public bool IsExpiredToken =>
            string.Equals(Error, "expired_token", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if the device code was not found, is unsupported, or malformed
        /// 指示设备码未找到、不受支持或格式错误
        /// </summary>
        public bool IsUnsupportedGrantType =>
            string.Equals(Error, "unsupported_grant_type", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates if the request was denied by the user
        /// 指示请求被用户拒绝
        /// </summary>
        public bool IsAccessDenied =>
            string.Equals(Error, "access_denied", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a user-friendly error message
        /// 获取用户友好的错误消息
        /// </summary>
        public string GetUserFriendlyErrorMessage()
        {
            if (IsSuccess) return string.Empty;

            return Error switch
            {
                "authorization_pending" => "等待用户授权中...",
                "slow_down" => "轮询频率过高，请降低频率",
                "expired_token" => "设备码已过期，请重新启动授权流程",
                "unsupported_grant_type" => "设备码无效或格式错误",
                "access_denied" => "用户拒绝了授权请求",
                _ => ErrorDescription ?? Error ?? "未知错误"
            };
        }

        /// <summary>
        /// Validates that the token response contains valid data
        /// 验证令牌响应包含有效数据
        /// </summary>
        public bool IsValid =>
            IsSuccess &&
            !string.IsNullOrWhiteSpace(AccessToken) &&
            !string.IsNullOrWhiteSpace(TokenType);

        /// <summary>
        /// Returns the scopes as an array
        /// 将范围作为数组返回
        /// </summary>
        public string[] GetScopes() =>
            string.IsNullOrWhiteSpace(Scope)
                ? Array.Empty<string>()
                : Scope.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .ToArray();
    }
}