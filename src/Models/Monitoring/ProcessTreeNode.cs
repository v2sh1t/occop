using System;
using System.Collections.Generic;
using System.Linq;

namespace Occop.Models.Monitoring
{
    /// <summary>
    /// 进程树节点模型
    /// 表示进程树中的一个节点，包含进程信息和父子关系
    /// </summary>
    public class ProcessTreeNode
    {
        #region 基本属性

        /// <summary>
        /// 进程信息
        /// </summary>
        public ProcessInfo ProcessInfo { get; set; }

        /// <summary>
        /// 父节点引用
        /// </summary>
        public ProcessTreeNode Parent { get; set; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<ProcessTreeNode> Children { get; set; }

        /// <summary>
        /// 节点在树中的深度级别（根节点为0）
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 是否为根节点
        /// </summary>
        public bool IsRoot => Parent == null;

        /// <summary>
        /// 是否为叶子节点
        /// </summary>
        public bool IsLeaf => Children.Count == 0;

        /// <summary>
        /// 子节点数量
        /// </summary>
        public int ChildCount => Children.Count;

        /// <summary>
        /// 子树中所有节点数量（包括自身）
        /// </summary>
        public int SubtreeSize => 1 + Children.Sum(child => child.SubtreeSize);

        #endregion

        #region 元数据属性

        /// <summary>
        /// 节点创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdatedTime { get; set; }

        /// <summary>
        /// 节点状态
        /// </summary>
        public ProcessTreeNodeState State { get; set; }

        /// <summary>
        /// 是否为AI工具进程
        /// </summary>
        public bool IsAIToolProcess => ProcessInfo?.IsAIToolProcess ?? false;

        /// <summary>
        /// 监控权重（用于优先级排序）
        /// </summary>
        public int MonitoringWeight { get; set; }

        /// <summary>
        /// 节点标签
        /// </summary>
        public HashSet<string> Tags { get; set; }

        /// <summary>
        /// 节点元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        #endregion

        #region 进程树统计信息

        /// <summary>
        /// 子树中AI工具进程数量
        /// </summary>
        public int AIToolProcessCount => (IsAIToolProcess ? 1 : 0) + Children.Sum(child => child.AIToolProcessCount);

        /// <summary>
        /// 子树中活跃进程数量
        /// </summary>
        public int ActiveProcessCount => (IsProcessActive ? 1 : 0) + Children.Sum(child => child.ActiveProcessCount);

        /// <summary>
        /// 子树总内存使用量
        /// </summary>
        public long TotalMemoryUsage => (ProcessInfo?.WorkingSetSize ?? 0) + Children.Sum(child => child.TotalMemoryUsage);

        /// <summary>
        /// 进程是否活跃
        /// </summary>
        public bool IsProcessActive => ProcessInfo?.State == ProcessState.Running && !(ProcessInfo?.HasExited ?? true);

        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ProcessTreeNode()
        {
            Children = new List<ProcessTreeNode>();
            Tags = new HashSet<string>();
            Metadata = new Dictionary<string, object>();
            CreatedTime = DateTime.UtcNow;
            LastUpdatedTime = DateTime.UtcNow;
            State = ProcessTreeNodeState.Active;
            MonitoringWeight = 1;
        }

        /// <summary>
        /// 基于进程信息构造
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        public ProcessTreeNode(ProcessInfo processInfo) : this()
        {
            ProcessInfo = processInfo ?? throw new ArgumentNullException(nameof(processInfo));

            // 从进程信息复制标签
            if (processInfo.Tags != null)
            {
                foreach (var tag in processInfo.Tags)
                {
                    Tags.Add(tag);
                }
            }

            // 设置AI工具权重
            if (processInfo.IsAIToolProcess)
            {
                MonitoringWeight = 10;
                Tags.Add("AI_TOOL");
            }
        }

        #endregion

        #region 树操作方法

        /// <summary>
        /// 添加子节点
        /// </summary>
        /// <param name="child">子节点</param>
        public void AddChild(ProcessTreeNode child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child.Parent != null)
                child.Parent.RemoveChild(child);

            child.Parent = this;
            child.Level = Level + 1;
            Children.Add(child);

            UpdateLastModifiedTime();
            UpdateChildLevels(child);
        }

        /// <summary>
        /// 移除子节点
        /// </summary>
        /// <param name="child">子节点</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveChild(ProcessTreeNode child)
        {
            if (child == null || !Children.Contains(child))
                return false;

            child.Parent = null;
            child.Level = 0;
            var removed = Children.Remove(child);

            if (removed)
                UpdateLastModifiedTime();

            return removed;
        }

        /// <summary>
        /// 移除所有子节点
        /// </summary>
        public void ClearChildren()
        {
            foreach (var child in Children)
            {
                child.Parent = null;
                child.Level = 0;
            }

            Children.Clear();
            UpdateLastModifiedTime();
        }

        /// <summary>
        /// 查找子节点（按进程ID）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>找到的子节点，未找到返回null</returns>
        public ProcessTreeNode FindChild(int processId)
        {
            return Children.FirstOrDefault(child => child.ProcessInfo?.ProcessId == processId);
        }

        /// <summary>
        /// 递归查找节点（按进程ID）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>找到的节点，未找到返回null</returns>
        public ProcessTreeNode FindDescendant(int processId)
        {
            if (ProcessInfo?.ProcessId == processId)
                return this;

            foreach (var child in Children)
            {
                var found = child.FindDescendant(processId);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 获取所有祖先节点
        /// </summary>
        /// <returns>祖先节点列表（从父节点到根节点）</returns>
        public List<ProcessTreeNode> GetAncestors()
        {
            var ancestors = new List<ProcessTreeNode>();
            var current = Parent;

            while (current != null)
            {
                ancestors.Add(current);
                current = current.Parent;
            }

            return ancestors;
        }

        /// <summary>
        /// 获取所有后代节点
        /// </summary>
        /// <returns>后代节点列表</returns>
        public List<ProcessTreeNode> GetDescendants()
        {
            var descendants = new List<ProcessTreeNode>();

            foreach (var child in Children)
            {
                descendants.Add(child);
                descendants.AddRange(child.GetDescendants());
            }

            return descendants;
        }

        /// <summary>
        /// 获取根节点
        /// </summary>
        /// <returns>根节点</returns>
        public ProcessTreeNode GetRoot()
        {
            var current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
            }
            return current;
        }

        /// <summary>
        /// 获取到根节点的路径
        /// </summary>
        /// <returns>路径节点列表（从当前节点到根节点）</returns>
        public List<ProcessTreeNode> GetPathToRoot()
        {
            var path = new List<ProcessTreeNode> { this };
            path.AddRange(GetAncestors());
            return path;
        }

        #endregion

        #region 查询和过滤方法

        /// <summary>
        /// 获取子树中的所有AI工具节点
        /// </summary>
        /// <returns>AI工具节点列表</returns>
        public List<ProcessTreeNode> GetAIToolNodes()
        {
            var aiToolNodes = new List<ProcessTreeNode>();

            if (IsAIToolProcess)
                aiToolNodes.Add(this);

            foreach (var child in Children)
            {
                aiToolNodes.AddRange(child.GetAIToolNodes());
            }

            return aiToolNodes;
        }

        /// <summary>
        /// 获取子树中的所有活跃节点
        /// </summary>
        /// <returns>活跃节点列表</returns>
        public List<ProcessTreeNode> GetActiveNodes()
        {
            var activeNodes = new List<ProcessTreeNode>();

            if (IsProcessActive)
                activeNodes.Add(this);

            foreach (var child in Children)
            {
                activeNodes.AddRange(child.GetActiveNodes());
            }

            return activeNodes;
        }

        /// <summary>
        /// 按标签过滤子树节点
        /// </summary>
        /// <param name="tag">标签</param>
        /// <returns>匹配的节点列表</returns>
        public List<ProcessTreeNode> GetNodesByTag(string tag)
        {
            var matchingNodes = new List<ProcessTreeNode>();

            if (HasTag(tag))
                matchingNodes.Add(this);

            foreach (var child in Children)
            {
                matchingNodes.AddRange(child.GetNodesByTag(tag));
            }

            return matchingNodes;
        }

        /// <summary>
        /// 按进程名称过滤子树节点
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <param name="ignoreCase">是否忽略大小写</param>
        /// <returns>匹配的节点列表</returns>
        public List<ProcessTreeNode> GetNodesByProcessName(string processName, bool ignoreCase = true)
        {
            var matchingNodes = new List<ProcessTreeNode>();
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            if (!string.IsNullOrEmpty(ProcessInfo?.ProcessName) &&
                ProcessInfo.ProcessName.Equals(processName, comparison))
            {
                matchingNodes.Add(this);
            }

            foreach (var child in Children)
            {
                matchingNodes.AddRange(child.GetNodesByProcessName(processName, ignoreCase));
            }

            return matchingNodes;
        }

        #endregion

        #region 状态管理方法

        /// <summary>
        /// 更新节点状态
        /// </summary>
        /// <param name="newState">新状态</param>
        public void UpdateState(ProcessTreeNodeState newState)
        {
            if (State != newState)
            {
                State = newState;
                UpdateLastModifiedTime();

                Tags.Add($"STATE_{newState}".ToUpperInvariant());
            }
        }

        /// <summary>
        /// 标记节点为已退出
        /// </summary>
        public void MarkAsExited()
        {
            UpdateState(ProcessTreeNodeState.Exited);
            Tags.Add("EXITED");
        }

        /// <summary>
        /// 标记节点为孤儿节点
        /// </summary>
        public void MarkAsOrphaned()
        {
            UpdateState(ProcessTreeNodeState.Orphaned);
            Tags.Add("ORPHANED");
        }

        /// <summary>
        /// 更新进程信息
        /// </summary>
        /// <param name="newProcessInfo">新的进程信息</param>
        public void UpdateProcessInfo(ProcessInfo newProcessInfo)
        {
            if (newProcessInfo != null && ProcessInfo?.ProcessId == newProcessInfo.ProcessId)
            {
                ProcessInfo = newProcessInfo;
                UpdateLastModifiedTime();

                // 更新AI工具权重
                if (newProcessInfo.IsAIToolProcess && MonitoringWeight < 10)
                {
                    MonitoringWeight = 10;
                    Tags.Add("AI_TOOL");
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 更新最后修改时间
        /// </summary>
        private void UpdateLastModifiedTime()
        {
            LastUpdatedTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 递归更新子节点的级别
        /// </summary>
        /// <param name="node">节点</param>
        private void UpdateChildLevels(ProcessTreeNode node)
        {
            foreach (var child in node.Children)
            {
                child.Level = node.Level + 1;
                UpdateChildLevels(child);
            }
        }

        /// <summary>
        /// 检查是否有指定标签
        /// </summary>
        /// <param name="tag">标签</param>
        /// <returns>是否存在</returns>
        public bool HasTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && Tags.Contains(tag.ToUpperInvariant());
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        /// <param name="tag">标签</param>
        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                Tags.Add(tag.ToUpperInvariant());
                UpdateLastModifiedTime();
            }
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        /// <param name="tag">标签</param>
        public void RemoveTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && Tags.Remove(tag.ToUpperInvariant()))
            {
                UpdateLastModifiedTime();
            }
        }

        /// <summary>
        /// 设置元数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void SetMetadata(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Metadata[key] = value;
                UpdateLastModifiedTime();
            }
        }

        /// <summary>
        /// 获取元数据
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>元数据值</returns>
        public T GetMetadata<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key) || !Metadata.ContainsKey(key))
                return defaultValue;

            try
            {
                return (T)Metadata[key];
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 生成树结构字符串表示
        /// </summary>
        /// <param name="indent">缩进字符串</param>
        /// <returns>树结构字符串</returns>
        public string ToTreeString(string indent = "")
        {
            var processName = ProcessInfo?.ProcessName ?? "Unknown";
            var processId = ProcessInfo?.ProcessId ?? 0;
            var state = ProcessInfo?.State ?? ProcessState.Unknown;
            var aiToolIndicator = IsAIToolProcess ? " [AI]" : "";

            var result = $"{indent}├─ {processName} [{processId}] ({state}){aiToolIndicator}\n";

            for (int i = 0; i < Children.Count; i++)
            {
                var childIndent = indent + (i == Children.Count - 1 ? "   " : "│  ");
                result += Children[i].ToTreeString(childIndent);
            }

            return result;
        }

        /// <summary>
        /// 获取节点摘要信息
        /// </summary>
        /// <returns>摘要字符串</returns>
        public string GetSummary()
        {
            var processName = ProcessInfo?.ProcessName ?? "Unknown";
            var processId = ProcessInfo?.ProcessId ?? 0;
            var childInfo = ChildCount > 0 ? $", {ChildCount}个子进程" : "";
            var aiToolIndicator = IsAIToolProcess ? " [AI工具]" : "";

            return $"{processName} [PID:{processId}] (Level:{Level}{childInfo}){aiToolIndicator}";
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 获取对象字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return GetSummary();
        }

        /// <summary>
        /// 计算哈希码
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return ProcessInfo?.ProcessId.GetHashCode() ?? 0;
        }

        /// <summary>
        /// 比较对象相等性
        /// </summary>
        /// <param name="obj">比较对象</param>
        /// <returns>是否相等</returns>
        public override bool Equals(object obj)
        {
            return obj is ProcessTreeNode other &&
                   ProcessInfo?.ProcessId == other.ProcessInfo?.ProcessId;
        }

        #endregion
    }

    /// <summary>
    /// 进程树节点状态
    /// </summary>
    public enum ProcessTreeNodeState
    {
        /// <summary>
        /// 活跃状态
        /// </summary>
        Active,

        /// <summary>
        /// 已退出
        /// </summary>
        Exited,

        /// <summary>
        /// 孤儿节点（父进程已退出）
        /// </summary>
        Orphaned,

        /// <summary>
        /// 暂停状态
        /// </summary>
        Suspended,

        /// <summary>
        /// 错误状态
        /// </summary>
        Error
    }
}