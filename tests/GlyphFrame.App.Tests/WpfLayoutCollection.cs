namespace GlyphFrame.App.Tests;

// These suites open native windows/popups. Running them concurrently changes
// keyboard focus and popup lifetime, and races shared WPF resource-package reads.
// Keep their real template/binding assertions; isolate their desktop ownership.
[CollectionDefinition("WPF layout", DisableParallelization = true)]
public sealed class WpfLayoutCollection;
