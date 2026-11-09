using System.Drawing;
using System.Threading.Tasks;

namespace ComicRack.Plugins.Tests;

public class IconTests
{
	private const string PluginWithIcon = "[PluginCommand(PluginHook.Books, Key = \"Tool\", Image = \"icon.png\")] public void Run(ComicBook[] books) { }";

	[Test]
	public async Task Icon_missing_from_the_plugin_folder_is_taken_from_the_embedded_resources_of_the_plugin_dll()
	{
		using var scripts = new ScriptsFolder();
		string png = scripts.PathTo(@"Resources\icon.png");
		using (var bitmap = new Bitmap(5, 3))
		{
			bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
		}
		scripts.CompileWithEmbeddedResource(@"Scripts\Mine", Source.Plugin(PluginWithIcon), png);

		var tool = scripts.Load().Command("Tool");

		await Assert.That(tool.CommandImage).IsNotNull();
		await Assert.That(tool.CommandImage.Size).IsEqualTo(new Size(5, 3));
	}

	[Test]
	public async Task Command_with_an_icon_that_exists_nowhere_still_loads_without_an_image()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(PluginWithIcon));

		var tool = scripts.Load().Command("Tool");

		await Assert.That(tool).IsNotNull();
		await Assert.That(tool.CommandImage).IsNull();
	}
}
