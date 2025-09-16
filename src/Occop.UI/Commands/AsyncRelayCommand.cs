using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Occop.UI.Commands
{
    /// <summary>
    /// 异步命令的基础接口
    /// </summary>
    public interface IAsyncCommand : ICommand, INotifyPropertyChanged
    {
        /// <summary>
        /// 异步执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>执行任务</returns>
        Task ExecuteAsync(object? parameter);

        /// <summary>
        /// 取消当前执行
        /// </summary>
        void Cancel();

        /// <summary>
        /// 是否正在执行
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// 支持取消标记
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// 触发CanExecute变更通知
        /// </summary>
        void RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 异步命令的实现类
    /// </summary>
    public class AsyncRelayCommand : IAsyncCommand
    {
        private readonly Func<object?, CancellationToken, Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isExecuting;

        /// <summary>
        /// 初始化AsyncRelayCommand（无参数版本）
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = (_, ct) => execute();
            _canExecute = canExecute != null ? _ => canExecute() : null;
        }

        /// <summary>
        /// 初始化AsyncRelayCommand（带CancellationToken版本）
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public AsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
        {
            _execute = (_, ct) => execute(ct);
            _canExecute = canExecute != null ? _ => canExecute() : null;
        }

        /// <summary>
        /// 初始化AsyncRelayCommand（带参数版本）
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = (param, _) => execute(param);
            _canExecute = canExecute;
        }

        /// <summary>
        /// 初始化AsyncRelayCommand（完整版本）
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public AsyncRelayCommand(Func<object?, CancellationToken, Task> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #region 属性

        /// <summary>
        /// 是否正在执行
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    OnPropertyChanged(nameof(IsExecuting));
                    RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 当前取消标记
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        #endregion

        #region 事件

        /// <summary>
        /// 执行状态变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region ICommand实现

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            return !IsExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        /// <summary>
        /// 同步执行命令（内部调用异步方法）
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public async void Execute(object? parameter)
        {
            await ExecuteAsync(parameter);
        }

        #endregion

        #region IAsyncCommand实现

        /// <summary>
        /// 异步执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>执行任务</returns>
        public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            IsExecuting = true;

            try
            {
                await _execute(parameter, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，正常情况，不需要处理
            }
            finally
            {
                IsExecuting = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 取消当前执行
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// 手动触发CanExecute变更通知
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region 事件触发

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// 泛型版本的AsyncRelayCommand
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public class AsyncRelayCommand<T> : IAsyncCommand
    {
        private readonly Func<T?, CancellationToken, Task> _execute;
        private readonly Func<T?, bool>? _canExecute;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isExecuting;

        /// <summary>
        /// 初始化强类型AsyncRelayCommand
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = (param, _) => execute(param);
            _canExecute = canExecute;
        }

        /// <summary>
        /// 初始化强类型AsyncRelayCommand（支持取消）
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public AsyncRelayCommand(Func<T?, CancellationToken, Task> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #region 属性

        /// <summary>
        /// 是否正在执行
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    OnPropertyChanged(nameof(IsExecuting));
                    RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 当前取消标记
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        #endregion

        #region 事件

        /// <summary>
        /// 执行状态变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region ICommand实现

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            if (IsExecuting)
                return false;

            // 处理类型转换
            if (parameter is T typedParameter)
                return _canExecute?.Invoke(typedParameter) ?? true;

            if (parameter == null && !typeof(T).IsValueType)
                return _canExecute?.Invoke(default(T)) ?? true;

            return false;
        }

        /// <summary>
        /// 同步执行命令（内部调用异步方法）
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public async void Execute(object? parameter)
        {
            await ExecuteAsync(parameter);
        }

        #endregion

        #region IAsyncCommand实现

        /// <summary>
        /// 异步执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>执行任务</returns>
        public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            T? typedParameter = default;

            if (parameter is T convertedParameter)
                typedParameter = convertedParameter;
            else if (parameter == null && !typeof(T).IsValueType)
                typedParameter = default(T);

            _cancellationTokenSource = new CancellationTokenSource();
            IsExecuting = true;

            try
            {
                await _execute(typedParameter, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，正常情况，不需要处理
            }
            finally
            {
                IsExecuting = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 取消当前执行
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// 手动触发CanExecute变更通知
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region 事件触发

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// AsyncRelayCommand的静态工厂方法
    /// </summary>
    public static class AsyncRelayCommandFactory
    {
        /// <summary>
        /// 创建无参数的AsyncRelayCommand
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>AsyncRelayCommand实例</returns>
        public static AsyncRelayCommand Create(Func<Task> execute, Func<bool>? canExecute = null)
        {
            return new AsyncRelayCommand(execute, canExecute);
        }

        /// <summary>
        /// 创建支持取消的AsyncRelayCommand
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>AsyncRelayCommand实例</returns>
        public static AsyncRelayCommand Create(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
        {
            return new AsyncRelayCommand(execute, canExecute);
        }

        /// <summary>
        /// 创建带参数的AsyncRelayCommand
        /// </summary>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>AsyncRelayCommand实例</returns>
        public static AsyncRelayCommand Create(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        {
            return new AsyncRelayCommand(execute, canExecute);
        }

        /// <summary>
        /// 创建强类型的AsyncRelayCommand
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>AsyncRelayCommand实例</returns>
        public static AsyncRelayCommand<T> Create<T>(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
        {
            return new AsyncRelayCommand<T>(execute, canExecute);
        }

        /// <summary>
        /// 创建支持取消的强类型AsyncRelayCommand
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="execute">异步执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>AsyncRelayCommand实例</returns>
        public static AsyncRelayCommand<T> Create<T>(Func<T?, CancellationToken, Task> execute, Func<T?, bool>? canExecute = null)
        {
            return new AsyncRelayCommand<T>(execute, canExecute);
        }
    }
}