using System.Text.RegularExpressions;

namespace Occop.Core.Logging
{
    /// <summary>
    /// 敏感数据过滤器，用于在日志中过滤敏感信息
    /// Sensitive data filter for filtering sensitive information in logs
    /// </summary>
    public class SensitiveDataFilter
    {
        private readonly List<SensitivePattern> _patterns;
        private readonly string _replacementMask;
        private readonly bool _enabled;

        /// <summary>
        /// 获取过滤器是否启用
        /// Gets whether the filter is enabled
        /// </summary>
        public bool IsEnabled => _enabled;

        /// <summary>
        /// 初始化敏感数据过滤器
        /// Initializes sensitive data filter
        /// </summary>
        /// <param name="enabled">是否启用 Whether enabled</param>
        /// <param name="replacementMask">替换掩码 Replacement mask</param>
        public SensitiveDataFilter(bool enabled = true, string replacementMask = "***REDACTED***")
        {
            _enabled = enabled;
            _replacementMask = replacementMask;
            _patterns = new List<SensitivePattern>();

            InitializeDefaultPatterns();
        }

        /// <summary>
        /// 初始化默认的敏感数据模式
        /// Initializes default sensitive data patterns
        /// </summary>
        private void InitializeDefaultPatterns()
        {
            // API密钥和令牌
            // API keys and tokens
            AddPattern("ApiKey", @"(api[_-]?key|apikey|api[_-]?token)[""'\s:=]+([a-zA-Z0-9\-_]{16,})", RegexOptions.IgnoreCase);
            AddPattern("BearerToken", @"Bearer\s+([a-zA-Z0-9\-_\.]+)", RegexOptions.IgnoreCase);
            AddPattern("AccessToken", @"(access[_-]?token|accesstoken)[""'\s:=]+([a-zA-Z0-9\-_\.]{16,})", RegexOptions.IgnoreCase);

            // 密码
            // Passwords
            AddPattern("Password", @"(password|passwd|pwd)[""'\s:=]+([^\s,""'}{]{4,})", RegexOptions.IgnoreCase);
            AddPattern("Secret", @"(secret|client[_-]?secret)[""'\s:=]+([a-zA-Z0-9\-_]{16,})", RegexOptions.IgnoreCase);

            // 加密密钥
            // Encryption keys
            AddPattern("EncryptionKey", @"(encryption[_-]?key|aes[_-]?key|cipher[_-]?key)[""'\s:=]+([a-zA-Z0-9+/=]{16,})", RegexOptions.IgnoreCase);
            AddPattern("PrivateKey", @"-----BEGIN\s+(?:RSA\s+)?PRIVATE\s+KEY-----[\s\S]*?-----END\s+(?:RSA\s+)?PRIVATE\s+KEY-----", RegexOptions.IgnoreCase);

            // 信用卡号
            // Credit card numbers
            AddPattern("CreditCard", @"\b(?:\d{4}[\s\-]?){3}\d{4}\b", RegexOptions.None);

            // 社会安全号码 (SSN)
            // Social Security Numbers
            AddPattern("SSN", @"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.None);

            // 电子邮件地址（部分遮蔽）
            // Email addresses (partial masking)
            AddPattern("Email", @"\b([a-zA-Z0-9._%+-]{2})[a-zA-Z0-9._%+-]*@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})", RegexOptions.IgnoreCase);

            // 电话号码
            // Phone numbers
            AddPattern("Phone", @"\b(?:\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})\b", RegexOptions.None);

            // IP地址（可选）
            // IP addresses (optional)
            // AddPattern("IPv4", @"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.None);

            // JWT令牌
            // JWT tokens
            AddPattern("JWT", @"eyJ[a-zA-Z0-9_-]*\.eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*", RegexOptions.None);

            // 数据库连接字符串
            // Database connection strings
            AddPattern("ConnectionString", @"(Server|Data Source|Host)=([^;]+);.*?(Password|Pwd)=([^;]+)", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 添加自定义敏感数据模式
        /// Adds custom sensitive data pattern
        /// </summary>
        /// <param name="name">模式名称 Pattern name</param>
        /// <param name="pattern">正则表达式模式 Regex pattern</param>
        /// <param name="options">正则表达式选项 Regex options</param>
        public void AddPattern(string name, string pattern, RegexOptions options = RegexOptions.None)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentNullException(nameof(pattern));

            _patterns.Add(new SensitivePattern
            {
                Name = name,
                Pattern = new Regex(pattern, options | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100))
            });
        }

        /// <summary>
        /// 移除自定义模式
        /// Removes custom pattern
        /// </summary>
        /// <param name="name">模式名称 Pattern name</param>
        /// <returns>是否成功移除 Whether successfully removed</returns>
        public bool RemovePattern(string name)
        {
            return _patterns.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        /// <summary>
        /// 过滤消息中的敏感数据
        /// Filters sensitive data from message
        /// </summary>
        /// <param name="message">原始消息 Original message</param>
        /// <returns>过滤后的消息 Filtered message</returns>
        public string FilterSensitiveData(string message)
        {
            if (!_enabled || string.IsNullOrEmpty(message))
                return message;

            var filteredMessage = message;

            foreach (var pattern in _patterns)
            {
                try
                {
                    if (pattern.Pattern.IsMatch(filteredMessage))
                    {
                        // 特殊处理：保留邮箱的部分信息
                        // Special handling: preserve partial email information
                        if (pattern.Name == "Email")
                        {
                            filteredMessage = pattern.Pattern.Replace(filteredMessage, m =>
                            {
                                var firstPart = m.Groups[1].Value;
                                var domain = m.Groups[2].Value;
                                return $"{firstPart}***@{domain}";
                            });
                        }
                        // 特殊处理：保留信用卡号的最后4位
                        // Special handling: preserve last 4 digits of credit card
                        else if (pattern.Name == "CreditCard")
                        {
                            filteredMessage = pattern.Pattern.Replace(filteredMessage, m =>
                            {
                                var digits = m.Value.Replace(" ", "").Replace("-", "");
                                return $"****-****-****-{digits.Substring(Math.Max(0, digits.Length - 4))}";
                            });
                        }
                        // 特殊处理：连接字符串只遮蔽密码
                        // Special handling: mask only password in connection string
                        else if (pattern.Name == "ConnectionString")
                        {
                            filteredMessage = pattern.Pattern.Replace(filteredMessage, m =>
                            {
                                var beforePassword = m.Value.Substring(0, m.Groups[3].Index - m.Index);
                                var afterPassword = m.Value.Substring(m.Groups[4].Index + m.Groups[4].Length - m.Index);
                                return $"{beforePassword}{m.Groups[3].Value}={_replacementMask}{afterPassword}";
                            });
                        }
                        else
                        {
                            filteredMessage = pattern.Pattern.Replace(filteredMessage, _replacementMask);
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // 正则表达式超时，跳过此模式
                    // Regex timeout, skip this pattern
                    continue;
                }
                catch (Exception)
                {
                    // 忽略其他异常，继续处理
                    // Ignore other exceptions, continue processing
                    continue;
                }
            }

            return filteredMessage;
        }

        /// <summary>
        /// 过滤字典中的敏感数据
        /// Filters sensitive data from dictionary
        /// </summary>
        /// <param name="properties">属性字典 Properties dictionary</param>
        /// <returns>过滤后的字典 Filtered dictionary</returns>
        public Dictionary<string, object> FilterSensitiveData(Dictionary<string, object> properties)
        {
            if (!_enabled || properties == null || properties.Count == 0)
                return properties;

            var filtered = new Dictionary<string, object>(properties.Count);

            foreach (var kvp in properties)
            {
                // 检查键名是否为敏感字段
                // Check if key name is a sensitive field
                if (IsSensitiveKey(kvp.Key))
                {
                    filtered[kvp.Key] = _replacementMask;
                }
                else if (kvp.Value is string stringValue)
                {
                    filtered[kvp.Key] = FilterSensitiveData(stringValue);
                }
                else if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    filtered[kvp.Key] = FilterSensitiveData(nestedDict);
                }
                else
                {
                    filtered[kvp.Key] = kvp.Value;
                }
            }

            return filtered;
        }

        /// <summary>
        /// 检查键名是否为敏感字段
        /// Checks if key name is a sensitive field
        /// </summary>
        /// <param name="key">键名 Key name</param>
        /// <returns>是否敏感 Whether sensitive</returns>
        private bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var lowerKey = key.ToLowerInvariant();

            return lowerKey.Contains("password") ||
                   lowerKey.Contains("secret") ||
                   lowerKey.Contains("token") ||
                   lowerKey.Contains("apikey") ||
                   lowerKey.Contains("api_key") ||
                   lowerKey.Contains("privatekey") ||
                   lowerKey.Contains("private_key") ||
                   lowerKey.Contains("connectionstring") ||
                   lowerKey.Contains("connection_string") ||
                   lowerKey.Contains("credential") ||
                   lowerKey.Contains("auth") && (lowerKey.Contains("key") || lowerKey.Contains("token"));
        }

        /// <summary>
        /// 检测消息中是否包含敏感数据
        /// Detects if message contains sensitive data
        /// </summary>
        /// <param name="message">消息 Message</param>
        /// <returns>检测结果 Detection result</returns>
        public SensitiveDataDetectionResult DetectSensitiveData(string message)
        {
            var result = new SensitiveDataDetectionResult
            {
                ContainsSensitiveData = false,
                DetectedPatterns = new List<string>()
            };

            if (string.IsNullOrEmpty(message))
                return result;

            foreach (var pattern in _patterns)
            {
                try
                {
                    if (pattern.Pattern.IsMatch(message))
                    {
                        result.ContainsSensitiveData = true;
                        result.DetectedPatterns.Add(pattern.Name);
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    continue;
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return result;
        }

        /// <summary>
        /// 敏感数据模式
        /// Sensitive data pattern
        /// </summary>
        private class SensitivePattern
        {
            public string Name { get; set; } = string.Empty;
            public Regex Pattern { get; set; } = null!;
        }
    }

    /// <summary>
    /// 敏感数据检测结果
    /// Sensitive data detection result
    /// </summary>
    public class SensitiveDataDetectionResult
    {
        /// <summary>
        /// 是否包含敏感数据
        /// Whether contains sensitive data
        /// </summary>
        public bool ContainsSensitiveData { get; set; }

        /// <summary>
        /// 检测到的模式列表
        /// List of detected patterns
        /// </summary>
        public List<string> DetectedPatterns { get; set; } = new List<string>();

        /// <summary>
        /// 检测到的模式数量
        /// Number of detected patterns
        /// </summary>
        public int PatternCount => DetectedPatterns.Count;
    }
}
