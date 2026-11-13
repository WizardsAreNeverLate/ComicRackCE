using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;

namespace ComicRack.Plugins.Tests;

public class SamplePackageTests
{
	private static readonly string SamplePackage = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "SamplePlugin.crplugin");

	[Test]
	public async Task Installed_sample_package_loads_its_four_plugins_from_its_package_folder()
	{
		using var scripts = new ScriptsFolder();
		string pending = scripts.PathTo("Pending");
		await Assert.That(new PackageManager(scripts.Scripts, pending, commit: false).Install(SamplePackage)).IsTrue();
		new PackageManager(scripts.Scripts, pending, commit: true);
		string installed = Path.Combine(scripts.Scripts, "Sample CSharp Plugin");

		var loaded = scripts.Load();

		await Assert.That(File.Exists(Path.Combine(installed, "SamplePlugin.dll"))).IsTrue();
		await Assert.That(loaded.Keys.Distinct()).IsEquivalentTo(new[]
		{
			"SamplePlugin.SamplePlugin.Startup",
			"SamplePlugin.SamplePlugin.StampNotes",
			"SamplePlugin.SamplePlugin.EditFieldDialog",
			"SamplePlugin.SamplePlugin.FieldEditorPanel"
		});
		await Assert.That(loaded.Engine.GetAllCommands().Select(c => c.Environment.CommandPath).Distinct()).IsEquivalentTo(new[] { installed });
		var book = new ComicBook();
		loaded.Engine.GetCommands("Books").Single(c => c.Key == "SamplePlugin.SamplePlugin.StampNotes").Invoke(new object[] { new[] { book } });
		await Assert.That(book.Notes).IsEqualTo("Stamped by Sample CSharp Plugin");
	}
}
