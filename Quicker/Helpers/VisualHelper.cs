using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;

namespace Quicker.Helpers
{
    /// <summary>
    /// WPF视觉树辅助类，提供视觉树遍历和查找功能
    /// </summary>
    public static class VisualHelper
    {
        #region 子元素查找

        /// <summary>
        /// 查找指定类型的所有子元素
        /// </summary>
        /// <typeparam name="T">要查找的元素类型</typeparam>
        /// <param name="depObj">起始依赖对象</param>
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child == null) continue;

                if (child is T targetChild)
                    yield return targetChild;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        /// <summary>
        /// 查找指定类型的第一个子元素
        /// </summary>
        /// <typeparam name="T">要查找的元素类型</typeparam>
        /// <param name="depObj">起始依赖对象</param>
        public static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child == null) continue;

                if (child is T targetChild)
                    return targetChild;

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        /// <summary>
        /// 查找指定类型和名称的子元素
        /// </summary>
        /// <typeparam name="T">要查找的元素类型</typeparam>
        /// <param name="depObj">起始依赖对象</param>
        /// <param name="name">元素名称</param>
        public static T FindVisualChildByName<T>(DependencyObject depObj, string name) where T : DependencyObject
        {
            if (depObj == null || string.IsNullOrEmpty(name)) return null;

            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child == null) continue;

                if (child is T targetChild && IsElementWithName(targetChild, name))
                    return targetChild;

                T childOfChild = FindVisualChildByName<T>(child, name);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        #endregion

        #region 父元素查找

        /// <summary>
        /// 查找指定类型的父元素（递归版本）
        /// </summary>
        /// <typeparam name="T">要查找的父元素类型</typeparam>
        /// <param name="depObj">起始依赖对象</param>
        public static T FindVisualParent<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(depObj);
            if (parent == null) return null;

            if (parent is T targetParent)
                return targetParent;

            return FindVisualParent<T>(parent);
        }

        /// <summary>
        /// 查找指定类型的父元素（非递归版本）
        /// </summary>
        /// <typeparam name="T">要查找的父元素类型</typeparam>
        /// <param name="child">起始依赖对象</param>
        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
                if (child is T targetParent)
                    return targetParent;
            }
            return null;
        }

        #endregion

        #region 特定元素查找

        /// <summary>
        /// 递归查找名为指定名称的 Grid
        /// </summary>
        /// <param name="parent">父级元素</param>
        /// <param name="name">Grid 名称</param>
        public static Grid FindGridByName(DependencyObject parent, string name)
        {
            return FindVisualChildByName<Grid>(parent, name);
        }

        /// <summary>
        /// 递归查找名为指定名称的 Button
        /// </summary>
        /// <param name="parent">父级元素</param>
        /// <param name="name">Button 名称</param>
        public static Button FindButtonByName(DependencyObject parent, string name)
        {
            return FindVisualChildByName<Button>(parent, name);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查元素是否具有指定名称
        /// </summary>
        /// <param name="element">要检查的元素</param>
        /// <param name="name">要匹配的名称</param>
        private static bool IsElementWithName(DependencyObject element, string name)
        {
            if (element is FrameworkElement fe)
                return fe.Name == name;
            return false;
        }

        #endregion
    }
} 