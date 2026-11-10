using System.Collections.Generic;

namespace cYo.Projects.ComicRack.Plugins
{
	internal sealed class SourcePluginCompilation
	{
		public string AssemblyName { get; set; }

		// Null means the newest version the compiler knows.
		public string LanguageVersion { get; set; }

		public IReadOnlyList<string> SourceFiles { get; set; }

		public IReadOnlyList<string> References { get; set; }

		// The DLL to write. The PDB is written next to it with the same name.
		public string OutputFile { get; set; }
	}

	// Implemented in ComicRack.Plugins.Compiler, the only assembly that references Roslyn.
	internal interface ISourcePluginCompiler
	{
		// Returns the compile errors in the form "file(line,column): error CS0000: message". An empty list means OutputFile is valid.
		IReadOnlyList<string> Compile(SourcePluginCompilation compilation);
	}
}
