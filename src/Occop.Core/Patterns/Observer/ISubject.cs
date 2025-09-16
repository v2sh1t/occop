using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Occop.Core.Patterns.Observer
{
    /// <summary>
    /// 主题接口的基础定义
    /// </summary>
    public interface ISubject
    {
        /// <summary>
        /// 注册观察者
        /// </summary>
        /// <param name="observer">要注册的观察者</param>
        void Attach(IObserver observer);

        /// <summary>
        /// 注销观察者
        /// </summary>
        /// <param name="observer">要注销的观察者</param>
        void Detach(IObserver observer);

        /// <summary>
        /// 通知所有观察者
        /// </summary>
        void Notify();

        /// <summary>
        /// 获取当前注册的观察者数量
        /// </summary>
        int ObserverCount { get; }
    }

    /// <summary>
    /// 泛型主题接口，提供强类型的数据传递
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public interface ISubject<T>
    {
        /// <summary>
        /// 注册泛型观察者
        /// </summary>
        /// <param name="observer">要注册的观察者</param>
        void Attach(IObserver<T> observer);

        /// <summary>
        /// 注销泛型观察者
        /// </summary>
        /// <param name="observer">要注销的观察者</param>
        void Detach(IObserver<T> observer);

        /// <summary>
        /// 通知所有观察者并传递数据
        /// </summary>
        /// <param name="data">要传递的数据</param>
        void Notify(T data);

        /// <summary>
        /// 获取当前注册的观察者数量
        /// </summary>
        int ObserverCount { get; }
    }

    /// <summary>
    /// 异步主题接口
    /// </summary>
    public interface IAsyncSubject
    {
        /// <summary>
        /// 注册异步观察者
        /// </summary>
        /// <param name="observer">要注册的异步观察者</param>
        void Attach(IAsyncObserver observer);

        /// <summary>
        /// 注销异步观察者
        /// </summary>
        /// <param name="observer">要注销的异步观察者</param>
        void Detach(IAsyncObserver observer);

        /// <summary>
        /// 异步通知所有观察者
        /// </summary>
        /// <returns>异步任务</returns>
        Task NotifyAsync();

        /// <summary>
        /// 获取当前注册的观察者数量
        /// </summary>
        int ObserverCount { get; }
    }

    /// <summary>
    /// 泛型异步主题接口
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public interface IAsyncSubject<T>
    {
        /// <summary>
        /// 注册泛型异步观察者
        /// </summary>
        /// <param name="observer">要注册的异步观察者</param>
        void Attach(IAsyncObserver<T> observer);

        /// <summary>
        /// 注销泛型异步观察者
        /// </summary>
        /// <param name="observer">要注销的异步观察者</param>
        void Detach(IAsyncObserver<T> observer);

        /// <summary>
        /// 异步通知所有观察者并传递数据
        /// </summary>
        /// <param name="data">要传递的数据</param>
        /// <returns>异步任务</returns>
        Task NotifyAsync(T data);

        /// <summary>
        /// 获取当前注册的观察者数量
        /// </summary>
        int ObserverCount { get; }
    }

    /// <summary>
    /// 主题的抽象基类，提供线程安全的观察者管理
    /// </summary>
    public abstract class SubjectBase : ISubject
    {
        // 使用线程安全的集合存储观察者
        private readonly ConcurrentBag<IObserver> _observers = new ConcurrentBag<IObserver>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// 获取当前注册的观察者数量
        /// </summary>
        public int ObserverCount => _observers.Count;

        /// <summary>
        /// 注册观察者
        /// </summary>
        /// <param name="observer">要注册的观察者</param>
        public virtual void Attach(IObserver observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            lock (_lockObject)
            {
                // 检查是否已存在相同的观察者（基于引用相等性）
                if (!_observers.Contains(observer))
                {
                    _observers.Add(observer);
                    OnObserverAttached(observer);
                }
            }
        }

        /// <summary>
        /// 注销观察者
        /// </summary>
        /// <param name="observer">要注销的观察者</param>
        public virtual void Detach(IObserver observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            lock (_lockObject)
            {
                // 重新构建不包含要移除观察者的集合
                var remainingObservers = _observers.Where(o => !ReferenceEquals(o, observer)).ToList();

                if (remainingObservers.Count < _observers.Count)
                {
                    // 清空并重新添加剩余的观察者
                    while (_observers.TryTake(out _)) { }
                    foreach (var remainingObserver in remainingObservers)
                    {
                        _observers.Add(remainingObserver);
                    }

                    OnObserverDetached(observer);
                }
            }
        }

        /// <summary>
        /// 通知所有观察者
        /// </summary>
        public virtual void Notify()
        {
            var observersSnapshot = _observers.ToArray();

            // 根据优先级排序（如果观察者实现了IPriorityObserver）
            var sortedObservers = observersSnapshot
                .OrderByDescending(o => o is IPriorityObserver priority ? priority.Priority : 0)
                .ToArray();

            foreach (var observer in sortedObservers)
            {
                try
                {
                    // 如果是条件观察者，检查是否应该更新
                    if (observer is IConditionalObserver conditional)
                    {
                        if (conditional.ShouldUpdate(this))
                        {
                            observer.Update(this);
                        }
                    }
                    else
                    {
                        observer.Update(this);
                    }
                }
                catch (Exception ex)
                {
                    OnNotificationError(observer, ex);
                }
            }
        }

        /// <summary>
        /// 清除所有观察者
        /// </summary>
        public virtual void ClearObservers()
        {
            lock (_lockObject)
            {
                var observersToRemove = _observers.ToArray();
                while (_observers.TryTake(out _)) { }

                foreach (var observer in observersToRemove)
                {
                    OnObserverDetached(observer);
                }
            }
        }

        /// <summary>
        /// 观察者注册时的回调
        /// </summary>
        /// <param name="observer">被注册的观察者</param>
        protected virtual void OnObserverAttached(IObserver observer)
        {
            // 子类可以重写此方法来处理观察者注册事件
        }

        /// <summary>
        /// 观察者注销时的回调
        /// </summary>
        /// <param name="observer">被注销的观察者</param>
        protected virtual void OnObserverDetached(IObserver observer)
        {
            // 子类可以重写此方法来处理观察者注销事件
        }

        /// <summary>
        /// 通知过程中出现错误时的回调
        /// </summary>
        /// <param name="observer">出错的观察者</param>
        /// <param name="exception">发生的异常</param>
        protected virtual void OnNotificationError(IObserver observer, Exception exception)
        {
            // 默认实现：可以记录日志或执行其他错误处理逻辑
            // 在实际项目中，这里可以集成日志系统
        }
    }

    /// <summary>
    /// 泛型主题的抽象基类，提供强类型的观察者管理
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public abstract class SubjectBase<T> : ISubject<T>
    {
        // 使用线程安全的集合存储观察者
        private readonly ConcurrentBag<IObserver<T>> _observers = new ConcurrentBag<IObserver<T>>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// 获取当前注册的观察者数量
        /// </summary>
        public int ObserverCount => _observers.Count;

        /// <summary>
        /// 注册泛型观察者
        /// </summary>
        /// <param name="observer">要注册的观察者</param>
        public virtual void Attach(IObserver<T> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            lock (_lockObject)
            {
                if (!_observers.Contains(observer))
                {
                    _observers.Add(observer);
                    OnObserverAttached(observer);
                }
            }
        }

        /// <summary>
        /// 注销泛型观察者
        /// </summary>
        /// <param name="observer">要注销的观察者</param>
        public virtual void Detach(IObserver<T> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            lock (_lockObject)
            {
                var remainingObservers = _observers.Where(o => !ReferenceEquals(o, observer)).ToList();

                if (remainingObservers.Count < _observers.Count)
                {
                    while (_observers.TryTake(out _)) { }
                    foreach (var remainingObserver in remainingObservers)
                    {
                        _observers.Add(remainingObserver);
                    }

                    OnObserverDetached(observer);
                }
            }
        }

        /// <summary>
        /// 通知所有观察者并传递数据
        /// </summary>
        /// <param name="data">要传递的数据</param>
        public virtual void Notify(T data)
        {
            var observersSnapshot = _observers.ToArray();

            // 根据优先级排序
            var sortedObservers = observersSnapshot
                .OrderByDescending(o => o is IPriorityObserver<T> priority ? priority.Priority : 0)
                .ToArray();

            foreach (var observer in sortedObservers)
            {
                try
                {
                    // 如果是条件观察者，检查是否应该更新
                    if (observer is IConditionalObserver<T> conditional)
                    {
                        if (conditional.ShouldUpdate(this, data))
                        {
                            observer.Update(this, data);
                        }
                    }
                    else
                    {
                        observer.Update(this, data);
                    }
                }
                catch (Exception ex)
                {
                    OnNotificationError(observer, data, ex);
                }
            }
        }

        /// <summary>
        /// 清除所有观察者
        /// </summary>
        public virtual void ClearObservers()
        {
            lock (_lockObject)
            {
                var observersToRemove = _observers.ToArray();
                while (_observers.TryTake(out _)) { }

                foreach (var observer in observersToRemove)
                {
                    OnObserverDetached(observer);
                }
            }
        }

        /// <summary>
        /// 观察者注册时的回调
        /// </summary>
        /// <param name="observer">被注册的观察者</param>
        protected virtual void OnObserverAttached(IObserver<T> observer)
        {
            // 子类可以重写此方法来处理观察者注册事件
        }

        /// <summary>
        /// 观察者注销时的回调
        /// </summary>
        /// <param name="observer">被注销的观察者</param>
        protected virtual void OnObserverDetached(IObserver<T> observer)
        {
            // 子类可以重写此方法来处理观察者注销事件
        }

        /// <summary>
        /// 通知过程中出现错误时的回调
        /// </summary>
        /// <param name="observer">出错的观察者</param>
        /// <param name="data">传递的数据</param>
        /// <param name="exception">发生的异常</param>
        protected virtual void OnNotificationError(IObserver<T> observer, T data, Exception exception)
        {
            // 默认实现：可以记录日志或执行其他错误处理逻辑
        }
    }
}