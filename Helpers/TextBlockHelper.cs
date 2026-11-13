using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows;

namespace Quicker.Helpers
{
    /// <summary>
    /// TextBlock 辅助类，实现高亮显示功能（加粗匹配关键字）。
    /// </summary>
    public static class TextBlockHelper
    {
        /// <summary>
        /// 附加属性：Highlight。用于绑定高亮数据（HighlightTextData）。
        /// </summary>
        public static readonly DependencyProperty HighlightProperty =
            DependencyProperty.RegisterAttached(
                "Highlight", typeof(object), typeof(TextBlockHelper),
                new PropertyMetadata(null, OnHighlightChanged));

        /// <summary>
        /// 设置 Highlight 附加属性。
        /// </summary>
        /// <param name="element">目标依赖对象</param>
        /// <param name="value">高亮数据</param>
        public static void SetHighlight(DependencyObject element, object value) => element.SetValue(HighlightProperty, value);

        /// <summary>
        /// 当 Highlight 属性变化时，动态生成 TextBlock 的 Inlines，实现关键字高亮。
        /// </summary>
        /// <param name="d">目标对象</param>
        /// <param name="e">事件参数</param>
        private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock tb && e.NewValue is HighlightTextData data)
            {
                tb.Inlines.Clear(); // 先清空原有内容
                if (string.IsNullOrEmpty(data.Keyword))
                {
                    // 没有关键字，直接显示原文本
                    tb.Inlines.Add(new Run(data.Text ?? ""));
                    return;
                }
                int index = 0;
                int kwLen = data.Keyword.Length;
                string text = data.Text ?? "";
                string lowerText = text.ToLower();
                string lowerKeyword = data.Keyword.ToLower();
                // 循环查找并加粗所有匹配关键字
                while (index < text.Length)
                {
                    int found = lowerText.IndexOf(lowerKeyword, index, StringComparison.Ordinal);
                    if (found < 0)
                    {
                        tb.Inlines.Add(new Run(text.Substring(index)));
                        break;
                    }
                    if (found > index)
                        tb.Inlines.Add(new Run(text.Substring(index, found - index)));
                    tb.Inlines.Add(new Run(text.Substring(found, kwLen)) { FontWeight = FontWeights.Bold });
                    index = found + kwLen;
                }
            }
        }
    }

    /// <summary>
    /// 高亮数据结构，包含原始文本和关键字。
    /// </summary>
    public class HighlightTextData
    {
        /// <summary>
        /// 原始文本
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// 需要高亮的关键字
        /// </summary>
        public string Keyword { get; set; }
    }
}
