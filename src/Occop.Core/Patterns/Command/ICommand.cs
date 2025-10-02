using System;
using System.Threading;
using System.Threading.Tasks;

namespace Occop.Core.Patterns.Command
{
    /// <summary>
    /// 命令模式的核心接口，提供基础的命令操作
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        void Execute();

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <returns>如果可以执行返回true</returns>
        bool CanExecute();

        /// <summary>
        /// 命令的唯一标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 命令的显示名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 命令的描述信息
        /// </summary>
        string? Description { get; }
    }

    /// <summary>
    /// 带参数的命令接口
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public interface ICommand<in T> : ICommand
    {
        /// <summary>
        /// 带参数执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        void Execute(T parameter);

        /// <summary>
        /// 判断命令是否可以带参数执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>如果可以执行返回true</returns>
        bool CanExecute(T parameter);
    }

    /// <summary>
    /// 异步命令接口
    /// </summary>
    public interface IAsyncCommand : ICommand
    {
        /// <summary>
        /// 异步执行命令
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task ExecuteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 命令是否正在执行
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// 命令执行完成事件
        /// </summary>
        event EventHandler<CommandExecutedEventArgs>? ExecutionCompleted;
    }

    /// <summary>
    /// 带参数的异步命令接口
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public interface IAsyncCommand<in T> : ICommand<T>, IAsyncCommand
    {
        /// <summary>
        /// 带参数异步执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 可撤销的命令接口
    /// </summary>
    public interface IUndoableCommand : ICommand
    {
        /// <summary>
        /// 撤销命令
        /// </summary>
        void Undo();

        /// <summary>
        /// 判断命令是否可以撤销
        /// </summary>
        /// <returns>如果可以撤销返回true</returns>
        bool CanUndo();

        /// <summary>
        /// 命令是否已被执行
        /// </summary>
        bool IsExecuted { get; }
    }

    /// <summary>
    /// 可重做的命令接口
    /// </summary>
    public interface IRedoableCommand : IUndoableCommand
    {
        /// <summary>
        /// 重做命令
        /// </summary>
        void Redo();

        /// <summary>
        /// 判断命令是否可以重做
        /// </summary>
        /// <returns>如果可以重做返回true</returns>
        bool CanRedo();
    }

    /// <summary>
    /// 宏命令接口，支持组合多个命令
    /// </summary>
    public interface IMacroCommand : ICommand
    {
        /// <summary>
        /// 添加子命令
        /// </summary>
        /// <param name="command">要添加的命令</param>
        void AddCommand(ICommand command);

        /// <summary>
        /// 移除子命令
        /// </summary>
        /// <param name="command">要移除的命令</param>
        void RemoveCommand(ICommand command);

        /// <summary>
        /// 清除所有子命令
        /// </summary>
        void ClearCommands();

        /// <summary>
        /// 获取子命令数量
        /// </summary>
        int CommandCount { get; }
    }

    /// <summary>
    /// 命令执行结果事件参数
    /// </summary>
    public class CommandExecutedEventArgs : EventArgs
    {
        /// <summary>
        /// 执行的命令
        /// </summary>
        public ICommand Command { get; }

        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 执行过程中的异常（如果有）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 执行耗时
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// 初始化命令执行结果事件参数
        /// </summary>
        /// <param name="command">执行的命令</param>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="exception">异常信息</param>
        /// <param name="duration">执行耗时</param>
        public CommandExecutedEventArgs(ICommand command, bool isSuccess, Exception? exception = null, TimeSpan duration = default)
        {
            Command = command;
            IsSuccess = isSuccess;
            Exception = exception;
            Duration = duration;
        }
    }

    /// <summary>
    /// 命令的抽象基类，提供通用的命令实现
    /// </summary>
    public abstract class CommandBase : ICommand
    {
        /// <summary>
        /// 命令的唯一标识
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 命令的显示名称
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// 命令的描述信息
        /// </summary>
        public string? Description { get; protected set; }

        /// <summary>
        /// 初始化命令基类
        /// </summary>
        /// <param name="name">命令名称</param>
        /// <param name="description">命令描述</param>
        /// <param name="id">命令标识，如果为null则自动生成</param>
        protected CommandBase(string name, string? description = null, string? id = null)
        {
            Id = id ?? Guid.NewGuid().ToString();
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description;
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public virtual void Execute()
        {
            if (!CanExecute())
                throw new InvalidOperationException($"Command '{Name}' cannot be executed.");

            try
            {
                OnExecute();
            }
            catch (Exception ex)
            {
                OnExecutionError(ex);
                throw;
            }
        }

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <returns>如果可以执行返回true</returns>
        public virtual bool CanExecute()
        {
            return OnCanExecute();
        }

        /// <summary>
        /// 执行命令的具体实现，由子类重写
        /// </summary>
        protected abstract void OnExecute();

        /// <summary>
        /// 判断命令是否可以执行的具体实现，由子类重写
        /// </summary>
        /// <returns>如果可以执行返回true</returns>
        protected virtual bool OnCanExecute()
        {
            return true;
        }

        /// <summary>
        /// 处理执行过程中的错误
        /// </summary>
        /// <param name="exception">发生的异常</param>
        protected virtual void OnExecutionError(Exception exception)
        {
            // 默认实现：可以记录日志或执行其他错误处理逻辑
        }

        /// <summary>
        /// 重写ToString方法，返回命令的描述信息
        /// </summary>
        /// <returns>命令的字符串表示</returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Description) ? Name : $"{Name}: {Description}";
        }

        /// <summary>
        /// 重写Equals方法，基于Id进行比较
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回true</returns>
        public override bool Equals(object? obj)
        {
            if (obj is CommandBase other)
            {
                return Id.Equals(other.Id);
            }
            return false;
        }

        /// <summary>
        /// 重写GetHashCode方法
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    /// <summary>
    /// 简单的委托命令实现
    /// </summary>
    public class DelegateCommand : CommandBase
    {
        private readonly Action _executeAction;
        private readonly Func<bool>? _canExecuteFunc;

        /// <summary>
        /// 初始化委托命令
        /// </summary>
        /// <param name="executeAction">执行操作</param>
        /// <param name="canExecuteFunc">可执行判断</param>
        /// <param name="name">命令名称</param>
        /// <param name="description">命令描述</param>
        /// <param name="id">命令标识</param>
        public DelegateCommand(
            Action executeAction,
            Func<bool>? canExecuteFunc = null,
            string? name = null,
            string? description = null,
            string? id = null)
            : base(name ?? "DelegateCommand", description, id)
        {
            _executeAction = executeAction ?? throw new ArgumentNullException(nameof(executeAction));
            _canExecuteFunc = canExecuteFunc;
        }

        /// <summary>
        /// 执行委托操作
        /// </summary>
        protected override void OnExecute()
        {
            _executeAction();
        }

        /// <summary>
        /// 判断是否可以执行
        /// </summary>
        /// <returns>如果可以执行返回true</returns>
        protected override bool OnCanExecute()
        {
            return _canExecuteFunc?.Invoke() ?? true;
        }
    }

    /// <summary>
    /// 带参数的委托命令实现
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public class DelegateCommand<T> : CommandBase, ICommand<T>
    {
        private readonly Action<T> _executeAction;
        private readonly Func<T, bool>? _canExecuteFunc;

        /// <summary>
        /// 初始化带参数的委托命令
        /// </summary>
        /// <param name="executeAction">执行操作</param>
        /// <param name="canExecuteFunc">可执行判断</param>
        /// <param name="name">命令名称</param>
        /// <param name="description">命令描述</param>
        /// <param name="id">命令标识</param>
        public DelegateCommand(
            Action<T> executeAction,
            Func<T, bool>? canExecuteFunc = null,
            string? name = null,
            string? description = null,
            string? id = null)
            : base(name ?? "DelegateCommand<T>", description, id)
        {
            _executeAction = executeAction ?? throw new ArgumentNullException(nameof(executeAction));
            _canExecuteFunc = canExecuteFunc;
        }

        /// <summary>
        /// 带参数执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public virtual void Execute(T parameter)
        {
            if (!CanExecute(parameter))
                throw new InvalidOperationException($"Command '{Name}' cannot be executed with the given parameter.");

            try
            {
                _executeAction(parameter);
            }
            catch (Exception ex)
            {
                OnExecutionError(ex);
                throw;
            }
        }

        /// <summary>
        /// 判断命令是否可以带参数执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>如果可以执行返回true</returns>
        public virtual bool CanExecute(T parameter)
        {
            return _canExecuteFunc?.Invoke(parameter) ?? true;
        }

        /// <summary>
        /// 无参数执行（使用默认值）
        /// </summary>
        protected override void OnExecute()
        {
            Execute(default(T)!);
        }

        /// <summary>
        /// 无参数可执行判断（使用默认值）
        /// </summary>
        /// <returns>如果可以执行返回true</returns>
        protected override bool OnCanExecute()
        {
            return CanExecute(default(T)!);
        }
    }

    /// <summary>
    /// 命令工厂类，提供常用命令的创建方法
    /// </summary>
    public static class CommandFactory
    {
        /// <summary>
        /// 创建简单的委托命令
        /// </summary>
        /// <param name="executeAction">执行操作</param>
        /// <param name="canExecuteFunc">可执行判断</param>
        /// <param name="name">命令名称</param>
        /// <param name="description">命令描述</param>
        /// <returns>委托命令实例</returns>
        public static DelegateCommand Create(
            Action executeAction,
            Func<bool>? canExecuteFunc = null,
            string? name = null,
            string? description = null)
        {
            return new DelegateCommand(executeAction, canExecuteFunc, name, description);
        }

        /// <summary>
        /// 创建带参数的委托命令
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="executeAction">执行操作</param>
        /// <param name="canExecuteFunc">可执行判断</param>
        /// <param name="name">命令名称</param>
        /// <param name="description">命令描述</param>
        /// <returns>委托命令实例</returns>
        public static DelegateCommand<T> Create<T>(
            Action<T> executeAction,
            Func<T, bool>? canExecuteFunc = null,
            string? name = null,
            string? description = null)
        {
            return new DelegateCommand<T>(executeAction, canExecuteFunc, name, description);
        }

        /// <summary>
        /// 创建空操作命令（用于测试或占位）
        /// </summary>
        /// <param name="name">命令名称</param>
        /// <param name="description">命令描述</param>
        /// <returns>空操作命令</returns>
        public static DelegateCommand CreateNoOp(string? name = null, string? description = null)
        {
            return new DelegateCommand(() => { }, () => true, name ?? "NoOp", description ?? "No operation command");
        }
    }
}