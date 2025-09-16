using System;
using System.Threading;

namespace Occop.Core.Patterns
{
    /// <summary>
    /// 线程安全的单例模式基础类
    /// 使用双重检查锁定模式确保线程安全和性能
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public abstract class Singleton<T> where T : class, new()
    {
        // 使用volatile确保内存可见性
        private static volatile T? _instance;

        // 用于线程同步的锁对象
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                // 第一次检查（无锁，性能优化）
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        // 第二次检查（有锁，确保线程安全）
                        if (_instance == null)
                        {
                            _instance = new T();

                            // 如果实例实现了初始化接口，调用初始化方法
                            if (_instance is ISingletonInitializer initializer)
                            {
                                initializer.Initialize();
                            }
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 受保护的构造函数，防止外部直接实例化
        /// </summary>
        protected Singleton() { }

        /// <summary>
        /// 重置单例实例（主要用于测试）
        /// 注意：在生产环境中谨慎使用此方法
        /// </summary>
        public static void ResetInstance()
        {
            lock (_lock)
            {
                if (_instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _instance = null;
            }
        }

        /// <summary>
        /// 检查单例是否已初始化
        /// </summary>
        public static bool IsInitialized => _instance != null;
    }

    /// <summary>
    /// 单例初始化接口
    /// 单例类可以实现此接口来提供自定义初始化逻辑
    /// </summary>
    public interface ISingletonInitializer
    {
        /// <summary>
        /// 单例初始化方法
        /// 在单例首次创建时自动调用
        /// </summary>
        void Initialize();
    }

    /// <summary>
    /// 懒加载单例模式的替代实现
    /// 使用.NET的Lazy&lt;T&gt;类提供线程安全的懒加载
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public abstract class LazySingleton<T> where T : class, new()
    {
        // 使用Lazy<T>提供线程安全的懒加载
        private static readonly Lazy<T> _lazy = new Lazy<T>(() =>
        {
            var instance = new T();

            // 如果实例实现了初始化接口，调用初始化方法
            if (instance is ISingletonInitializer initializer)
            {
                initializer.Initialize();
            }

            return instance;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance => _lazy.Value;

        /// <summary>
        /// 受保护的构造函数，防止外部直接实例化
        /// </summary>
        protected LazySingleton() { }

        /// <summary>
        /// 检查单例是否已初始化
        /// </summary>
        public static bool IsInitialized => _lazy.IsValueCreated;
    }

    /// <summary>
    /// 单例模式的辅助类，提供常用的单例操作
    /// </summary>
    public static class SingletonHelper
    {
        /// <summary>
        /// 安全地重置单例实例
        /// </summary>
        /// <typeparam name="T">单例类型</typeparam>
        /// <param name="resetAction">重置前执行的操作</param>
        public static void SafeReset<T>(Action? resetAction = null) where T : Singleton<T>, new()
        {
            try
            {
                resetAction?.Invoke();
            }
            finally
            {
                Singleton<T>.ResetInstance();
            }
        }

        /// <summary>
        /// 检查单例是否为指定类型
        /// </summary>
        /// <typeparam name="TSingleton">单例基类型</typeparam>
        /// <typeparam name="TTarget">目标类型</typeparam>
        /// <returns>如果单例是目标类型返回true</returns>
        public static bool IsInstanceOfType<TSingleton, TTarget>()
            where TSingleton : Singleton<TSingleton>, new()
            where TTarget : class
        {
            return Singleton<TSingleton>.IsInitialized &&
                   Singleton<TSingleton>.Instance is TTarget;
        }

        /// <summary>
        /// 获取单例实例并转换为指定类型
        /// </summary>
        /// <typeparam name="TSingleton">单例基类型</typeparam>
        /// <typeparam name="TTarget">目标类型</typeparam>
        /// <returns>转换后的实例，如果转换失败返回null</returns>
        public static TTarget? GetInstanceAs<TSingleton, TTarget>()
            where TSingleton : Singleton<TSingleton>, new()
            where TTarget : class
        {
            return Singleton<TSingleton>.Instance as TTarget;
        }
    }
}