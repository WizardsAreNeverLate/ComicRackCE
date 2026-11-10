using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cYo.Common.IO;
using cYo.Common.Windows.Forms.Theme;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ComicRack.Plugins.Tests;

internal sealed class ScriptsFolder : IDisposable
{
	private static readonly MetadataReference[] defaultReferences = new[]
	{
		typeof(object).Assembly,
		typeof(Uri).Assembly,
		typeof(Enumerable).Assembly,
		typeof(System.Drawing.Image).Assembly,
		typeof(System.Windows.Forms.Control).Assembly,
		typeof(FileUtility).Assembly,
		typeof(Themes).Assembly,
		typeof(ComicBook).Assembly,
		typeof(Command).Assembly
	}.Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location)).ToArray();

	public ScriptsFolder()
	{
		Root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ComicRack.Plugins.Tests", Guid.NewGuid().ToString("N"))).FullName;
		Scripts = Directory.CreateDirectory(Path.Combine(Root, "Scripts")).FullName;
	}

	public string Root { get; }

	public string Scripts { get; }

	public string PathTo(string pathUnderRoot)
	{
		string path = Path.GetFullPath(Path.Combine(Root, pathUnderRoot));
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		return path;
	}

	public string Write(string pathUnderRoot, string content)
	{
		string path = PathTo(pathUnderRoot);
		File.WriteAllText(path, content);
		return path;
	}

	// Assembly loads are permanent for the process, so every fixture assembly gets its own identity.
	public string Compile(string folderUnderRoot, string source, params string[] references)
	{
		string name = "Fixture" + Guid.NewGuid().ToString("N");
		return CompileTo(Path.Combine(folderUnderRoot, name + ".dll"), name, source, references: references);
	}

	// Only assemblies the source really uses end up as references of the DLL.
	public string CompileTo(string dllUnderRoot, string assemblyName, string source, string embeddedResourceFile = null, string version = null, params string[] references)
	{
		string dll = PathTo(dllUnderRoot);
		if (version != null)
		{
			source = "[assembly: System.Reflection.AssemblyVersion(\"" + version + "\")]\n" + source;
		}
		var compilation = CSharpCompilation.Create(
			assemblyName,
			new[] { CSharpSyntaxTree.ParseText(source) },
			defaultReferences.Concat(references.Select(r => (MetadataReference)MetadataReference.CreateFromFile(r))),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		var resources = embeddedResourceFile == null
			? Enumerable.Empty<ResourceDescription>()
			: new[] { new ResourceDescription(assemblyName + "." + Path.GetFileName(embeddedResourceFile), () => File.OpenRead(embeddedResourceFile), isPublic: true) };
		using (FileStream stream = File.Create(dll))
		{
			var result = compilation.Emit(stream, manifestResources: resources);
			if (!result.Success)
			{
				throw new InvalidOperationException("Fixture source did not compile:" + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics) + Environment.NewLine + source);
			}
		}
		return dll;
	}

	public LoadedPlugins Load()
	{
		PluginEngine engine = new PluginEngine();
		engine.Initialize(new StubEnvironment(), Scripts);
		return new LoadedPlugins(engine);
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(Root, recursive: true);
		}
		catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
		{
			// Loaded plugin DLLs stay locked until the process exits.
		}
	}
}

internal sealed class LoadedPlugins
{
	public LoadedPlugins(PluginEngine engine)
	{
		Engine = engine;
	}

	public PluginEngine Engine { get; }

	public IEnumerable<string> Hooks(string key) => Engine.GetAllCommands().Where(c => c.Key == key).Select(c => c.Hook);

	public Command Command(string key, string hook) => Engine.GetAllCommands().SingleOrDefault(c => c.Key == key && c.Hook == hook);
}

internal static class Source
{
	private const string Usings = "using System; using System.Collections.Generic; using System.Drawing; using System.Windows.Forms; using cYo.Projects.ComicRack.Engine; using cYo.Projects.ComicRack.Plugins;\n";

	public static string Types(string types) => Usings + "namespace Fixture {\n" + types + "\n}";

	public static string Plugin(string key, string hooks, string members, string className = "Plugin")
	{
		return Types($"public class {className} : DotNetPlugin, {hooks} {{\npublic override string Key {{ get; set; }} = \"{key}\";\n{members}\n}}");
	}
}
