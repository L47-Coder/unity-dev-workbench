using UnityEngine;

namespace DevWorkbench.Editor
{
    /// <summary>
    /// Contract for a page inside the Dev Workbench window. Any non-abstract
    /// class implementing this interface (in any Editor assembly that references
    /// <c>DevWorkbench.Editor</c>) is discovered automatically via
    /// <see cref="UnityEditor.TypeCache"/> and rendered as a tab under
    /// <see cref="GroupTitle"/> / <see cref="TabTitle"/>.
    /// <para>
    /// Implementers must provide a parameterless constructor. Apart from
    /// <see cref="GroupTitle"/> and <see cref="TabTitle"/>, every member has a
    /// no-op default implementation so pages only override what they need.
    /// </para>
    /// <para>
    /// Pages are pure UI: they must not assume they will be constructed during
    /// the framework-guard ensure pass. Project-level bootstrap work (asset
    /// registration, order-asset sync, ...) belongs in a dedicated
    /// <see cref="IWorkbenchContribution"/> implementation instead; that keeps
    /// page construction cheap and avoids firing every external page during
    /// the global first-open fan-out.
    /// </para>
    /// </summary>
    public interface IPage
    {
        /// <summary>Display name of the left-hand group this page belongs to
        /// (e.g. <c>"Manager"</c>, <c>"Component"</c>). Pages sharing the same
        /// value are grouped together in the navigation column.</summary>
        string GroupTitle { get; }

        /// <summary>Display name of the page's tab inside its group
        /// (e.g. <c>"Viewer"</c>, <c>"Creator"</c>).</summary>
        string TabTitle { get; }

        /// <summary>
        /// Invoked the first time the page becomes active within a given
        /// <c>DevWindow</c> instance. Use for one-shot UI initialisation.
        /// </summary>
        void OnFirstEnter() { }

        /// <summary>Invoked every time the page becomes the active tab.</summary>
        void OnEnter() { }

        /// <summary>Invoked every frame while the page is active, inside the
        /// content rect already reserved by the window chrome.</summary>
        void OnGUI(Rect rect) { }

        /// <summary>Invoked when the page is about to lose focus (either the
        /// user switched tabs or the window is being destroyed).</summary>
        void OnLeave() { }
    }
}
