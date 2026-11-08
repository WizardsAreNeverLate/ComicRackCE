using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CSharp;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class SmokeTests
{
	[Test]
	public async Task Compiled_plugin_is_discovered()
	{
		string dir = Path.Combine(Path.GetTempPath(), "crtest-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		string name = "Smoke" + Guid.NewGuid().ToString("N");
		var options = new CompilerParameters { OutputAssembly = Path.Combine(dir, name + ".dll") };
		options.ReferencedAssemblies.Add(typeof(Command).Assembly.Location);
		CompilerResults results = new CSharpCodeProvider().CompileAssemblyFromSource(options,
			"public class P { [cYo.Projects.ComicRack.Plugins.PluginCommand(\"Startup\")] public static void Run() { } }");
		await Assert.That(results.Errors.HasErrors).IsFalse();
		var engine = new PluginEngine();
		engine.Initialize(new StubEnvironment(), dir);
		await Assert.That(engine.GetAllCommands().Select(c => c.Key)).Contains("P.Run");
	}
}
