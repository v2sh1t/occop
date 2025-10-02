using System;

namespace Occop.Core.Patterns.Observer
{
    /// <summary>
    /// 观察者接口的基础定义
    /// </summary>
    public interface IObserver
    {
        /// <summary>
        /// 接收来自主题的更新通知
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        void Update(ISubject subject);
    }

    /// <summary>
    /// 泛型观察者接口，提供强类型的数据传递
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public interface IObserver<in T>
    {
        /// <summary>
        /// 接收来自主题的强类型更新通知
        /// </summary>
        /// <param name="data">更新的数据</param>
        void Update(T data);

        /// <summary>
        /// 接收来自主题的更新通知（带主题引用）
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <param name="data">更新的数据</param>
        void Update(ISubject<T> subject, T data);
    }

    /// <summary>
    /// 异步观察者接口
    /// </summary>
    public interface IAsyncObserver
    {
        /// <summary>
        /// 异步接收来自主题的更新通知
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <returns>异步任务</returns>
        System.Threading.Tasks.Task UpdateAsync(ISubject subject);
    }

    /// <summary>
    /// 泛型异步观察者接口
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public interface IAsyncObserver<in T>
    {
        /// <summary>
        /// 异步接收来自主题的强类型更新通知
        /// </summary>
        /// <param name="data">更新的数据</param>
        /// <returns>异步任务</returns>
        System.Threading.Tasks.Task UpdateAsync(T data);

        /// <summary>
        /// 异步接收来自主题的更新通知（带主题引用）
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <param name="data">更新的数据</param>
        /// <returns>异步任务</returns>
        System.Threading.Tasks.Task UpdateAsync(ISubject<T> subject, T data);
    }

    /// <summary>
    /// 带优先级的观察者接口
    /// </summary>
    public interface IPriorityObserver : IObserver
    {
        /// <summary>
        /// 观察者的优先级（数值越大优先级越高）
        /// </summary>
        int Priority { get; }
    }

    /// <summary>
    /// 带优先级的泛型观察者接口
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public interface IPriorityObserver<in T> : IObserver<T>
    {
        /// <summary>
        /// 观察者的优先级（数值越大优先级越高）
        /// </summary>
        int Priority { get; }
    }

    /// <summary>
    /// 条件观察者接口，支持条件性的通知接收
    /// </summary>
    public interface IConditionalObserver : IObserver
    {
        /// <summary>
        /// 检查是否应该接收此次更新
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <returns>如果应该接收更新返回true</returns>
        bool ShouldUpdate(ISubject subject);
    }

    /// <summary>
    /// 泛型条件观察者接口
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public interface IConditionalObserver<in T> : IObserver<T>
    {
        /// <summary>
        /// 检查是否应该接收此次更新
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <param name="data">更新的数据</param>
        /// <returns>如果应该接收更新返回true</returns>
        bool ShouldUpdate(ISubject<T> subject, T data);
    }

    /// <summary>
    /// 观察者的抽象基类，提供通用的观察者实现
    /// </summary>
    public abstract class ObserverBase : IObserver
    {
        /// <summary>
        /// 观察者是否处于活动状态
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 观察者的唯一标识
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 初始化观察者基类
        /// </summary>
        /// <param name="id">观察者标识，如果为null则自动生成</param>
        protected ObserverBase(string? id = null)
        {
            Id = id ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 接收更新通知的具体实现
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        public virtual void Update(ISubject subject)
        {
            if (!IsActive) return;

            try
            {
                OnUpdate(subject);
            }
            catch (Exception ex)
            {
                OnUpdateError(subject, ex);
            }
        }

        /// <summary>
        /// 处理更新的抽象方法，由子类实现
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        protected abstract void OnUpdate(ISubject subject);

        /// <summary>
        /// 处理更新过程中的错误
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <param name="exception">发生的异常</param>
        protected virtual void OnUpdateError(ISubject subject, Exception exception)
        {
            // 默认实现：可以记录日志或执行其他错误处理逻辑
            // 在实际项目中，这里可以集成日志系统
        }
    }

    /// <summary>
    /// 泛型观察者的抽象基类
    /// </summary>
    /// <typeparam name="T">传递的数据类型</typeparam>
    public abstract class ObserverBase<T> : IObserver<T>
    {
        /// <summary>
        /// 观察者是否处于活动状态
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 观察者的唯一标识
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 初始化泛型观察者基类
        /// </summary>
        /// <param name="id">观察者标识，如果为null则自动生成</param>
        protected ObserverBase(string? id = null)
        {
            Id = id ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 接收强类型更新通知
        /// </summary>
        /// <param name="data">更新的数据</param>
        public virtual void Update(T data)
        {
            if (!IsActive) return;

            try
            {
                OnUpdate(data);
            }
            catch (Exception ex)
            {
                OnUpdateError(data, ex);
            }
        }

        /// <summary>
        /// 接收带主题引用的强类型更新通知
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <param name="data">更新的数据</param>
        public virtual void Update(ISubject<T> subject, T data)
        {
            if (!IsActive) return;

            try
            {
                OnUpdate(subject, data);
            }
            catch (Exception ex)
            {
                OnUpdateError(subject, data, ex);
            }
        }

        /// <summary>
        /// 处理强类型更新的抽象方法
        /// </summary>
        /// <param name="data">更新的数据</param>
        protected abstract void OnUpdate(T data);

        /// <summary>
        /// 处理带主题引用的强类型更新（默认委托给OnUpdate(T)）
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <param name="data">更新的数据</param>
        protected virtual void OnUpdate(ISubject<T> subject, T data)
        {
            OnUpdate(data);
        }

        /// <summary>
        /// 处理更新过程中的错误
        /// </summary>
        /// <param name="data">更新的数据</param>
        /// <param name="exception">发生的异常</param>
        protected virtual void OnUpdateError(T data, Exception exception)
        {
            // 默认实现：可以记录日志或执行其他错误处理逻辑
        }

        /// <summary>
        /// 处理带主题引用的更新过程中的错误
        /// </summary>
        /// <param name="subject">发出通知的主题</param>
        /// <param name="data">更新的数据</param>
        /// <param name="exception">发生的异常</param>
        protected virtual void OnUpdateError(ISubject<T> subject, T data, Exception exception)
        {
            OnUpdateError(data, exception);
        }
    }
}