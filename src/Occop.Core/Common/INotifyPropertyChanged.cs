using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Occop.Core.Common
{
    /// <summary>
    /// 扩展的属性变更通知接口，提供便捷的属性变更通知方法
    /// </summary>
    public interface INotifyPropertyChangedEx : INotifyPropertyChanged
    {
        /// <summary>
        /// 触发属性变更事件的便捷方法
        /// </summary>
        /// <param name="propertyName">属性名称，自动推断调用者成员名</param>
        void OnPropertyChanged([CallerMemberName] string? propertyName = null);

        /// <summary>
        /// 设置属性值并在值变更时触发通知
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名称，自动推断调用者成员名</param>
        /// <returns>如果值发生了变更返回true，否则返回false</returns>
        bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null);

        /// <summary>
        /// 设置属性值并在值变更时触发通知，支持自定义变更后操作
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="onChanged">值变更后的回调操作</param>
        /// <param name="propertyName">属性名称，自动推断调用者成员名</param>
        /// <returns>如果值发生了变更返回true，否则返回false</returns>
        bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null);
    }

    /// <summary>
    /// 属性变更通知的基础实现类
    /// </summary>
    public abstract class NotifyPropertyChangedBase : INotifyPropertyChangedEx
    {
        /// <summary>
        /// 属性值变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 设置属性值并在值变更时触发通知
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果值发生了变更返回true，否则返回false</returns>
        public virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 设置属性值并在值变更时触发通知，支持自定义变更后操作
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="onChanged">值变更后的回调操作</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果值发生了变更返回true，否则返回false</returns>
        public virtual bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            onChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 批量触发多个属性变更通知
        /// </summary>
        /// <param name="propertyNames">属性名称数组</param>
        protected virtual void OnPropertiesChanged(params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }
        }
    }
}