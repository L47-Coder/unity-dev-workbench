using System;

namespace DevWorkbench
{
    /// <summary>
    /// 标注在 string 字段上，TableView 渲染时将其显示为下拉框。
    /// <paramref name="methodName"/> 必须是该字段所在类中的静态方法，
    /// 返回 string[] 或 List&lt;string&gt; 作为下拉选项。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DropdownAttribute : Attribute
    {
        public string MethodName { get; }
        public DropdownAttribute(string methodName) => MethodName = methodName;
    }
}
