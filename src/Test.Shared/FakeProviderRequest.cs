namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A request recorded by <see cref="FakeProviderServer"/>.
    /// </summary>
    internal sealed class FakeProviderRequest
    {
        internal string Method { get; set; } = String.Empty;

        internal string Path { get; set; } = String.Empty;

        internal string Body { get; set; } = String.Empty;

        internal Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
