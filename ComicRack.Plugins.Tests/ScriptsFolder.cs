using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using cYo.Common.IO;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;
using Microsoft.CSharp;

namespace ComicRack.Plugins.Tests;

internal sealed class ScriptsFolder : IDisposable
{
	private static readonly string[] defaultReferences =
	{
		"System.dll",
		"System.Core.dll",
		"System.Drawing.dll",
		"System.Windows.Forms.dll",
		typeof(FileUtility).Assembly.Location,
		typeof(ComicBook).Assembly.Location,
		typeof(Command).Assembly.Location
	};

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

	// The plugin API is always passed to the compiler, but a DLL references it only when its source uses it.
	public string Compile(string folderUnderRoot, string source, params string[] references)
	{
		return Compile(folderUnderRoot, source, null, references);
	}

	public string CompileWithEmbeddedResource(string folderUnderRoot, string source, string resourceFile)
	{
		return Compile(folderUnderRoot, source, resourceFile, new string[0]);
	}

	private string Compile(string folderUnderRoot, string source, string resourceFile, string[] references)
	{
		// Assembly loads are permanent for the process, so every fixture assembly needs its own identity.
		string dll = PathTo(Path.Combine(folderUnderRoot, "Fixture" + Guid.NewGuid().ToString("N") + ".dll"));
		CompilerParameters parameters = new CompilerParameters(defaultReferences.Concat(references).ToArray(), dll);
		if (resourceFile != null)
		{
			parameters.EmbeddedResources.Add(resourceFile);
		}
		CompilerResults results = new CSharpCodeProvider().CompileAssemblyFromSource(parameters, source);
		if (results.Errors.HasErrors)
		{
			throw new InvalidOperationException("Fixture source did not compile:" + Environment.NewLine + string.Join(Environment.NewLine, results.Errors.Cast<CompilerError>()) + Environment.NewLine + source);
		}
		return dll;
	}

	public LoadedPlugins Load()
	{
		PluginConsole.Install();
		PluginEngine engine = new PluginEngine();
		engine.Initialize(new StubEnvironment(), Scripts);
		return new LoadedPlugins(engine, Root);
	}

	public static bool IsExecutionLoaded(string dll)
	{
		return AppDomain.CurrentDomain.GetAssemblies().Any(a => !a.IsDynamic && string.Equals(a.Location, dll, StringComparison.OrdinalIgnoreCase));
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
	private readonly string root;

	public LoadedPlugins(PluginEngine engine, string root)
	{
		Engine = engine;
		this.root = root;
	}

	public PluginEngine Engine { get; }

	public IEnumerable<string> Keys => Engine.GetAllCommands().Select(c => c.Key);

	public string ConsoleLinesAboutThisFixture => PluginConsole.LinesMentioning(root);

	public Command Command(string key) => Engine.GetAllCommands().SingleOrDefault(c => c.Key == key);
}

internal static class PluginConsole
{
	private static readonly CaptureStream capture = new CaptureStream();

	public static void Install()
	{
		Command.SetLogOutput(capture);
		Command.EnableLog = true;
	}

	public static string LinesMentioning(string text)
	{
		return string.Join("\n", capture.Text.Split('\n').Where(line => line.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).Select(line => line.TrimEnd('\r')));
	}

	private sealed class CaptureStream : Stream
	{
		private readonly StringBuilder text = new StringBuilder();

		public string Text
		{
			get
			{
				lock (text)
				{
					return text.ToString();
				}
			}
		}

		public override bool CanRead => false;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			lock (text)
			{
				text.Append(Encoding.UTF8.GetString(buffer, offset, count));
			}
		}

		public override void Flush()
		{
		}

		protected override void Dispose(bool disposing)
		{
		}

		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();
	}
}

internal static class Source
{
	private const string Usings = "using System; using System.Collections.Generic; using cYo.Projects.ComicRack.Engine; using cYo.Projects.ComicRack.Plugins;\n";

	// A DotNetPlugin class named Plugin. The fixture compiler is C# 5, so the Key is a property with a body and not an initializer.
	public static string Plugin(string key, string hooks, string members)
	{
		return Types("public class Plugin : DotNetPlugin, " + hooks + " {\n" + PluginKey(key) + "\n" + members + "\n}");
	}

	public static string PluginKey(string key) => "public override string Key { get { return \"" + key + "\"; } set { } }";

	public static string Types(string types) => Usings + "namespace Fixture {\n" + types + "\n}";
}
