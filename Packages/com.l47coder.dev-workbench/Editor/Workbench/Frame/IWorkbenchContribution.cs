namespace DevWorkbench.Editor
{
    /// <summary>
    /// Contract for project-level bootstrap work that must run once per Editor
    /// session, independently of which <see cref="IPage"/> the user opens.
    /// <para>
    /// The framework guard discovers every non-abstract implementation via
    /// <see cref="UnityEditor.TypeCache"/> and invokes <see cref="Contribute"/>
    /// exactly once during <c>Ensure()</c>, after the project scaffolding has
    /// been verified and before any page is rendered.
    /// </para>
    /// <para>
    /// Implementations must have a public parameterless constructor. They are
    /// expected to be idempotent: <c>Contribute</c> may be re-entered after
    /// domain reloads, so it should only perform work when the target state is
    /// not already satisfied (e.g. "ensure every config is registered in
    /// Addressables", "ensure the order asset is in sync"). Typical owners are
    /// subsystem adapters such as Manager / Component installers.
    /// </para>
    /// <para>
    /// This interface intentionally carries no UI responsibilities. It was
    /// extracted from <see cref="IPage"/> so that adding new pages does not
    /// implicitly subscribe them to the global first-open fan-out.
    /// </para>
    /// </summary>
    public interface IWorkbenchContribution
    {
        /// <summary>
        /// Perform the one-shot ensure work owned by this contribution.
        /// Must be idempotent and throw only on unrecoverable errors.
        /// </summary>
        void Contribute();
    }
}
