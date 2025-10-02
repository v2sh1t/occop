using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Occop.Core.Common;

namespace Occop.UI.ViewModels
{
    /// <summary>
    /// ViewModel的基类，提供通用的MVVM基础设施
    /// </summary>
    public abstract class BaseViewModel : NotifyPropertyChangedBase, IDisposable
    {
        private bool _isBusy;
        private string? _title;
        private string? _errorMessage;
        private readonly Dictionary<string, object?> _propertyStorage = new();
        private readonly SynchronizationContext? _synchronizationContext;
        private bool _disposed;

        /// <summary>
        /// 初始化BaseViewModel
        /// </summary>
        protected BaseViewModel()
        {
            _synchronizationContext = SynchronizationContext.Current;
        }

        #region 公共属性

        /// <summary>
        /// 是否正在忙碌（用于显示加载状态）
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// ViewModel标题
        /// </summary>
        public string? Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        #endregion

        #region 属性存储机制

        /// <summary>
        /// 获取属性值（带默认值）
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="defaultValue">默认值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性值</returns>
        protected T GetProperty<T>(T defaultValue = default!, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (propertyName == null) return defaultValue;

            return _propertyStorage.TryGetValue(propertyName, out var value) && value is T typedValue
                ? typedValue
                : defaultValue;
        }

        /// <summary>
        /// 设置属性值（使用内部存储）
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>是否发生了变更</returns>
        protected bool SetProperty<T>(T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (propertyName == null) return false;

            var oldValue = GetProperty<T>(propertyName: propertyName);
            if (EqualityComparer<T>.Default.Equals(oldValue, value))
                return false;

            _propertyStorage[propertyName] = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region 错误处理

        /// <summary>
        /// 清除错误信息
        /// </summary>
        public virtual void ClearError()
        {
            ErrorMessage = null;
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        /// <param name="message">错误信息</param>
        protected virtual void SetError(string message)
        {
            ErrorMessage = message;
        }

        /// <summary>
        /// 设置异常信息
        /// </summary>
        /// <param name="exception">异常对象</param>
        protected virtual void SetError(Exception exception)
        {
            ErrorMessage = exception.Message;
        }

        #endregion

        #region 异步操作支持

        /// <summary>
        /// 执行异步操作，自动管理IsBusy状态和错误处理
        /// </summary>
        /// <param name="asyncAction">异步操作</param>
        /// <param name="onError">错误处理回调</param>
        /// <returns>执行任务</returns>
        protected virtual async Task ExecuteAsync(Func<Task> asyncAction, Action<Exception>? onError = null)
        {
            try
            {
                IsBusy = true;
                ClearError();
                await asyncAction();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                SetError(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 执行异步操作并返回结果，自动管理IsBusy状态和错误处理
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="asyncFunc">异步函数</param>
        /// <param name="onError">错误处理回调</param>
        /// <returns>执行结果</returns>
        protected virtual async Task<T?> ExecuteAsync<T>(Func<Task<T>> asyncFunc, Action<Exception>? onError = null)
        {
            try
            {
                IsBusy = true;
                ClearError();
                return await asyncFunc();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                SetError(ex);
                return default;
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region UI线程调度

        /// <summary>
        /// 在UI线程上执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        protected virtual void InvokeOnUIThread(Action action)
        {
            if (_synchronizationContext != null)
            {
                _synchronizationContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// 在UI线程上异步执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <returns>执行任务</returns>
        protected virtual Task InvokeOnUIThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            InvokeOnUIThread(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        #endregion

        #region 生命周期管理

        /// <summary>
        /// 初始化ViewModel（在构造后调用）
        /// </summary>
        public virtual void Initialize()
        {
            // 子类可以重写此方法进行初始化
        }

        /// <summary>
        /// 异步初始化ViewModel
        /// </summary>
        /// <returns>初始化任务</returns>
        public virtual Task InitializeAsync()
        {
            // 子类可以重写此方法进行异步初始化
            return Task.CompletedTask;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        protected virtual void OnDispose()
        {
            // 子类可以重写此方法进行资源清理
        }

        #endregion

        #region IDisposable实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的实际实现
        /// </summary>
        /// <param name="disposing">是否为显式释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                OnDispose();
                _propertyStorage.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// 检查是否已被释放
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        #endregion
    }
}