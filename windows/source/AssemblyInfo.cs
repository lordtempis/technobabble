using System.Reflection;
using System.Runtime.CompilerServices;

#if TERMINAL
[assembly: AssemblyTitle("Technobabble Terminal for Windows")]
[assembly: AssemblyVersion("1.13.0.0")]
[assembly: AssemblyFileVersion("1.13.0.0")]
#else
[assembly: AssemblyTitle("Technobabble")]
[assembly: AssemblyVersion("1.13.0.0")]
[assembly: AssemblyFileVersion("1.13.0.0")]
#endif
[assembly: AssemblyDescription("A native Windows recreation of Dustin Kirkland's Hollywood")]
[assembly: AssemblyProduct("Technobabble")]
[assembly: InternalsVisibleTo("Technobabble.Tests")]


