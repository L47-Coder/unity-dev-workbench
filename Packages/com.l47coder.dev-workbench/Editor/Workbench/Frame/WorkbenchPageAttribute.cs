using System;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// Marks a class as a Dev Workbench page and declares its display metadata.
    /// <para>
    /// When present, <c>DevWindow</c> reads group, tab, and order directly from this
    /// attribute — no instantiation is required at discovery time, and the values
    /// take precedence over <see cref="IPage.GroupTitle"/> / <see cref="IPage.TabTitle"/>.
    /// </para>
    /// <para>
    /// The page class must still implement <see cref="IPage"/> and provide a public
    /// parameterless constructor so it can be instantiated when activated.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [WorkbenchPage("Manager", "Viewer", order: 0)]
    /// internal sealed class ManagerViewerPage : IPage { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WorkbenchPageAttribute : Attribute
    {
        /// <summary>Left-sidebar group name, e.g. <c>"Manager"</c>.</summary>
        public string Group { get; }

        /// <summary>Tab title within the group, e.g. <c>"Viewer"</c>.</summary>
        public string Tab { get; }

        /// <summary>
        /// Default order hint used the first time this page is discovered.
        /// Lower values appear earlier in the tab bar.
        /// Pages already stored in <c>PageOrder.asset</c> keep their persisted order.
        /// </summary>
        public int Order { get; }

        /// <param name="group">Left-sidebar group label.</param>
        /// <param name="tab">Tab label within the group.</param>
        /// <param name="order">First-discovery order hint (default 0).</param>
        public WorkbenchPageAttribute(string group, string tab, int order = 0)
        {
            Group = group;
            Tab = tab;
            Order = order;
        }
    }
}
