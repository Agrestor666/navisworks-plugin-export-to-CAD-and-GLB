namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill for net48 — compiler emits module initializers when LangVersion supports them.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
