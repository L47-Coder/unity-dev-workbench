using System;

namespace DevWorkbench
{
    /// <summary>
    /// Annotates a field of a data class so the DevWorkbench
    /// <c>TableView</c> can customise its column header, visibility and
    /// editability. Declared in the runtime assembly so runtime types (such as
    /// <see cref="UnityEngine.ScriptableObject"/> payloads) can use it; the
    /// <c>TableView</c> control itself lives in the editor assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableColumnAttribute : Attribute
    {
        /// <summary>
        /// Column header text. When <c>null</c> or empty, the table falls back
        /// to a Nicify-style name derived from the field name.
        /// </summary>
        public string Header { get; set; }

        /// <summary>Whether the column is visible. Defaults to <c>true</c>.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Whether the column cells are user-editable. Defaults to <c>true</c>.</summary>
        public bool Editable { get; set; } = true;
    }
}
