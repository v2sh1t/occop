using System.Text;
using System.Text.RegularExpressions;
using Occop.Core.Logging;

namespace Occop.Core.Security
{
    /// <summary>
    /// 敏感数据扫描器，用于检测日志、内存、文件中的敏感信息
    /// Sensitive data scanner for detecting sensitive information in logs, memory, and files
    /// </summary>
    public class SensitiveDataScanner
    {
        private readonly SensitiveDataFilter _filter;
        private readonly List<SensitivePattern> _patterns;

        /// <summary>
        /// 获取扫描器配置
        /// Gets scanner configuration
        /// </summary>
        public ScannerConfiguration Configuration { get; }

        /// <summary>
        /// 初始化敏感数据扫描器
        /// Initializes sensitive data scanner
        /// </summary>
        /// <param name="configuration">扫描器配置 Scanner configuration</param>
        public SensitiveDataScanner(ScannerConfiguration? configuration = null)
        {
            Configuration = configuration ?? new ScannerConfiguration();
            _filter = new SensitiveDataFilter(true);
            _patterns = new List<SensitivePattern>();

            InitializePatterns();
        }

        /// <summary>
        /// 初始化扫描模式
        /// Initializes scan patterns
        /// </summary>
        private void InitializePatterns()
        {
            // API密钥和令牌
            AddPattern("ApiKey", @"(api[_-]?key|apikey|api[_-]?token)[""'\s:=]+([a-zA-Z0-9\-_]{16,})",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            AddPattern("BearerToken", @"Bearer\s+([a-zA-Z0-9\-_\.]+)",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            AddPattern("AccessToken", @"(access[_-]?token|accesstoken)[""'\s:=]+([a-zA-Z0-9\-_\.]{16,})",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            // 密码和密钥
            AddPattern("Password", @"(password|passwd|pwd)[""'\s:=]+([^\s,""'}{]{4,})",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            AddPattern("Secret", @"(secret|client[_-]?secret)[""'\s:=]+([a-zA-Z0-9\-_]{16,})",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            // 加密密钥
            AddPattern("EncryptionKey", @"(encryption[_-]?key|aes[_-]?key|cipher[_-]?key)[""'\s:=]+([a-zA-Z0-9+/=]{16,})",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            AddPattern("PrivateKey", @"-----BEGIN\s+(?:RSA\s+)?PRIVATE\s+KEY-----[\s\S]*?-----END\s+(?:RSA\s+)?PRIVATE\s+KEY-----",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            // 信用卡和金融信息
            AddPattern("CreditCard", @"\b(?:\d{4}[\s\-]?){3}\d{4}\b",
                SensitivityLevel.High, RegexOptions.None);

            AddPattern("SSN", @"\b\d{3}-\d{2}-\d{4}\b",
                SensitivityLevel.High, RegexOptions.None);

            // 个人信息
            AddPattern("Email", @"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b",
                SensitivityLevel.Medium, RegexOptions.IgnoreCase);

            AddPattern("Phone", @"\b(?:\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})\b",
                SensitivityLevel.Medium, RegexOptions.None);

            // JWT和Session令牌
            AddPattern("JWT", @"eyJ[a-zA-Z0-9_-]*\.eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*",
                SensitivityLevel.Critical, RegexOptions.None);

            AddPattern("SessionToken", @"(session[_-]?token|session[_-]?id)[""'\s:=]+([a-zA-Z0-9\-_]{16,})",
                SensitivityLevel.High, RegexOptions.IgnoreCase);

            // 数据库连接信息
            AddPattern("ConnectionString", @"(Server|Data Source|Host)=([^;]+);.*?(Password|Pwd)=([^;]+)",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            AddPattern("DatabasePassword", @"(database[_-]?password|db[_-]?pwd)[""'\s:=]+([^\s,""'}{]+)",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);

            // GitHub和OAuth令牌
            AddPattern("GitHubToken", @"gh[pousr]_[a-zA-Z0-9]{36}",
                SensitivityLevel.Critical, RegexOptions.None);

            AddPattern("OAuthSecret", @"(oauth[_-]?secret|client[_-]?secret)[""'\s:=]+([a-zA-Z0-9\-_]{16,})",
                SensitivityLevel.Critical, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 添加自定义扫描模式
        /// Adds custom scan pattern
        /// </summary>
        public void AddPattern(string name, string pattern, SensitivityLevel level, RegexOptions options = RegexOptions.None)
        {
            _patterns.Add(new SensitivePattern
            {
                Name = name,
                Pattern = new Regex(pattern, options | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100)),
                Level = level
            });
        }

        /// <summary>
        /// 扫描文本中的敏感数据
        /// Scans text for sensitive data
        /// </summary>
        public ScanResult ScanText(string text, string source = "unknown")
        {
            var result = new ScanResult
            {
                Source = source,
                ScanTime = DateTime.UtcNow,
                Findings = new List<SensitiveFinding>()
            };

            if (string.IsNullOrEmpty(text))
                return result;

            foreach (var pattern in _patterns)
            {
                try
                {
                    var matches = pattern.Pattern.Matches(text);
                    foreach (Match match in matches)
                    {
                        result.Findings.Add(new SensitiveFinding
                        {
                            Type = pattern.Name,
                            Level = pattern.Level,
                            Position = match.Index,
                            Length = match.Length,
                            Context = GetContext(text, match.Index, match.Length),
                            MaskedValue = MaskValue(match.Value, pattern.Name)
                        });
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    continue;
                }
            }

            result.ContainsSensitiveData = result.Findings.Any();
            result.CriticalCount = result.Findings.Count(f => f.Level == SensitivityLevel.Critical);
            result.HighCount = result.Findings.Count(f => f.Level == SensitivityLevel.High);
            result.MediumCount = result.Findings.Count(f => f.Level == SensitivityLevel.Medium);

            return result;
        }

        /// <summary>
        /// 扫描文件中的敏感数据
        /// Scans file for sensitive data
        /// </summary>
        public async Task<ScanResult> ScanFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > Configuration.MaxFileSizeBytes)
            {
                return new ScanResult
                {
                    Source = filePath,
                    ScanTime = DateTime.UtcNow,
                    Error = $"File size ({fileInfo.Length} bytes) exceeds maximum ({Configuration.MaxFileSizeBytes} bytes)"
                };
            }

            var content = await File.ReadAllTextAsync(filePath);
            return ScanText(content, filePath);
        }

        /// <summary>
        /// 扫描目录中所有文件的敏感数据
        /// Scans all files in directory for sensitive data
        /// </summary>
        public async Task<List<ScanResult>> ScanDirectoryAsync(string directoryPath, string searchPattern = "*.*")
        {
            var results = new List<ScanResult>();

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (Configuration.ExcludePatterns.Any(p => file.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    var result = await ScanFileAsync(file);
                    if (result.ContainsSensitiveData)
                        results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new ScanResult
                    {
                        Source = file,
                        ScanTime = DateTime.UtcNow,
                        Error = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// 扫描内存对象的敏感数据
        /// Scans memory object for sensitive data
        /// </summary>
        public ScanResult ScanObject(object obj, string source = "object")
        {
            if (obj == null)
                return new ScanResult { Source = source, ScanTime = DateTime.UtcNow };

            var serialized = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            return ScanText(serialized, source);
        }

        /// <summary>
        /// 批量扫描多个文本
        /// Batch scans multiple texts
        /// </summary>
        public List<ScanResult> ScanBatch(Dictionary<string, string> textsWithSources)
        {
            return textsWithSources.Select(kvp => ScanText(kvp.Value, kvp.Key)).ToList();
        }

        /// <summary>
        /// 生成扫描报告
        /// Generates scan report
        /// </summary>
        public ScanReport GenerateReport(List<ScanResult> results)
        {
            return new ScanReport
            {
                TotalScans = results.Count,
                FilesWithSensitiveData = results.Count(r => r.ContainsSensitiveData),
                TotalFindings = results.Sum(r => r.Findings.Count),
                CriticalFindings = results.Sum(r => r.CriticalCount),
                HighFindings = results.Sum(r => r.HighCount),
                MediumFindings = results.Sum(r => r.MediumCount),
                Results = results,
                GeneratedAt = DateTime.UtcNow,
                IsClean = !results.Any(r => r.ContainsSensitiveData)
            };
        }

        /// <summary>
        /// 获取上下文信息
        /// Gets context information
        /// </summary>
        private string GetContext(string text, int position, int length, int contextSize = 20)
        {
            var start = Math.Max(0, position - contextSize);
            var end = Math.Min(text.Length, position + length + contextSize);

            var context = text.Substring(start, end - start);
            return context.Replace("\n", " ").Replace("\r", "");
        }

        /// <summary>
        /// 遮蔽敏感值
        /// Masks sensitive value
        /// </summary>
        private string MaskValue(string value, string patternName)
        {
            return _filter.FilterSensitiveData(value);
        }

        /// <summary>
        /// 敏感模式
        /// </summary>
        private class SensitivePattern
        {
            public string Name { get; set; } = string.Empty;
            public Regex Pattern { get; set; } = null!;
            public SensitivityLevel Level { get; set; }
        }
    }

    /// <summary>
    /// 扫描器配置
    /// Scanner configuration
    /// </summary>
    public class ScannerConfiguration
    {
        /// <summary>
        /// 最大文件大小（字节）
        /// Maximum file size in bytes
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// 排除模式列表
        /// List of exclude patterns
        /// </summary>
        public List<string> ExcludePatterns { get; set; } = new()
        {
            ".git",
            "node_modules",
            "bin",
            "obj",
            ".vs",
            "packages"
        };

        /// <summary>
        /// 是否扫描二进制文件
        /// Whether to scan binary files
        /// </summary>
        public bool ScanBinaryFiles { get; set; } = false;
    }

    /// <summary>
    /// 扫描结果
    /// Scan result
    /// </summary>
    public class ScanResult
    {
        /// <summary>
        /// 来源
        /// Source
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 扫描时间
        /// Scan time
        /// </summary>
        public DateTime ScanTime { get; set; }

        /// <summary>
        /// 是否包含敏感数据
        /// Whether contains sensitive data
        /// </summary>
        public bool ContainsSensitiveData { get; set; }

        /// <summary>
        /// 发现的敏感数据列表
        /// List of sensitive findings
        /// </summary>
        public List<SensitiveFinding> Findings { get; set; } = new();

        /// <summary>
        /// 严重级别计数
        /// Critical level count
        /// </summary>
        public int CriticalCount { get; set; }

        /// <summary>
        /// 高级别计数
        /// High level count
        /// </summary>
        public int HighCount { get; set; }

        /// <summary>
        /// 中级别计数
        /// Medium level count
        /// </summary>
        public int MediumCount { get; set; }

        /// <summary>
        /// 错误信息
        /// Error message
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// 敏感发现
    /// Sensitive finding
    /// </summary>
    public class SensitiveFinding
    {
        /// <summary>
        /// 类型
        /// Type
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 敏感级别
        /// Sensitivity level
        /// </summary>
        public SensitivityLevel Level { get; set; }

        /// <summary>
        /// 位置
        /// Position
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// 长度
        /// Length
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 上下文
        /// Context
        /// </summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// 遮蔽后的值
        /// Masked value
        /// </summary>
        public string MaskedValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// 扫描报告
    /// Scan report
    /// </summary>
    public class ScanReport
    {
        /// <summary>
        /// 总扫描数
        /// Total scans
        /// </summary>
        public int TotalScans { get; set; }

        /// <summary>
        /// 包含敏感数据的文件数
        /// Files with sensitive data
        /// </summary>
        public int FilesWithSensitiveData { get; set; }

        /// <summary>
        /// 总发现数
        /// Total findings
        /// </summary>
        public int TotalFindings { get; set; }

        /// <summary>
        /// 严重级别发现数
        /// Critical findings
        /// </summary>
        public int CriticalFindings { get; set; }

        /// <summary>
        /// 高级别发现数
        /// High findings
        /// </summary>
        public int HighFindings { get; set; }

        /// <summary>
        /// 中级别发现数
        /// Medium findings
        /// </summary>
        public int MediumFindings { get; set; }

        /// <summary>
        /// 扫描结果列表
        /// List of scan results
        /// </summary>
        public List<ScanResult> Results { get; set; } = new();

        /// <summary>
        /// 生成时间
        /// Generated at
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// 是否干净（无敏感数据）
        /// Whether clean (no sensitive data)
        /// </summary>
        public bool IsClean { get; set; }
    }

    /// <summary>
    /// 敏感级别
    /// Sensitivity level
    /// </summary>
    public enum SensitivityLevel
    {
        /// <summary>
        /// 低级别
        /// Low level
        /// </summary>
        Low = 0,

        /// <summary>
        /// 中级别
        /// Medium level
        /// </summary>
        Medium = 1,

        /// <summary>
        /// 高级别
        /// High level
        /// </summary>
        High = 2,

        /// <summary>
        /// 严重级别
        /// Critical level
        /// </summary>
        Critical = 3
    }
}
