using System;
using System.Windows.Input;

namespace Occop.UI.Commands
{
    /// <summary>
    /// 命令的基础接口，扩展了ICommand
    /// </summary>
    public interface IRelayCommand : ICommand
    {
        /// <summary>
        /// 触发CanExecute变更通知
        /// </summary>
        void RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 同步命令的实现，支持委托模式
    /// </summary>
    public class RelayCommand : IRelayCommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>
        /// 初始化RelayCommand（无参数版本）
        /// </summary>
        /// <param name="execute">执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = _ => execute();
            _canExecute = canExecute != null ? _ => canExecute() : null;
        }

        /// <summary>
        /// 初始化RelayCommand（带参数版本）
        /// </summary>
        /// <param name="execute">执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 执行状态变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                _execute(parameter);
            }
        }

        /// <summary>
        /// 手动触发CanExecute变更通知
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 泛型版本的RelayCommand，提供强类型支持
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public class RelayCommand<T> : IRelayCommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        /// <summary>
        /// 初始化强类型RelayCommand
        /// </summary>
        /// <param name="execute">执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 执行状态变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            // 处理类型转换
            if (parameter is T typedParameter)
                return _canExecute?.Invoke(typedParameter) ?? true;

            if (parameter == null && !typeof(T).IsValueType)
                return _canExecute?.Invoke(default(T)) ?? true;

            return false;
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                T? typedParameter = default;

                if (parameter is T convertedParameter)
                    typedParameter = convertedParameter;
                else if (parameter == null && !typeof(T).IsValueType)
                    typedParameter = default(T);

                _execute(typedParameter);
            }
        }

        /// <summary>
        /// 手动触发CanExecute变更通知
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// RelayCommand的静态工厂方法
    /// </summary>
    public static class RelayCommandFactory
    {
        /// <summary>
        /// 创建无参数的RelayCommand
        /// </summary>
        /// <param name="execute">执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>RelayCommand实例</returns>
        public static RelayCommand Create(Action execute, Func<bool>? canExecute = null)
        {
            return new RelayCommand(execute, canExecute);
        }

        /// <summary>
        /// 创建带参数的RelayCommand
        /// </summary>
        /// <param name="execute">执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>RelayCommand实例</returns>
        public static RelayCommand Create(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            return new RelayCommand(execute, canExecute);
        }

        /// <summary>
        /// 创建强类型的RelayCommand
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="execute">执行操作</param>
        /// <param name="canExecute">可执行判断</param>
        /// <returns>RelayCommand实例</returns>
        public static RelayCommand<T> Create<T>(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            return new RelayCommand<T>(execute, canExecute);
        }
    }
}