using System.Runtime.CompilerServices;

// The WPF host and the tests were part of the same assembly before the split.
// These keep the internal types visible to them.
[assembly: InternalsVisibleTo("Mapping Tools")]
[assembly: InternalsVisibleTo("Mapping_Tools_Tests")]
[assembly: InternalsVisibleTo("Mapping_Tools.Core.Tests")]
