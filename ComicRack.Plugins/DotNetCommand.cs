using cYo.Common.Drawing;
using cYo.Common.Windows.Forms.Theme;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace cYo.Projects.ComicRack.Plugins
{
	internal interface IDotNetHook
	{
		string Hook { get; }
		Type HookType { get; }
	}

	internal sealed class DotNetHook<T> : IDotNetHook where T : IDotNetCommand
	{
		public string Hook { get; }
		public Type HookType { get; }

		public DotNetHook(string hook)
		{
			Hook = hook;
			HookType = typeof(T);
		}
	}

	internal sealed class DotNetCommand : Command
	{
		private const string Method = "Execute";

		private static readonly Dictionary<string, IDotNetHook> hookTypes = new()
		{
			[PluginEngine.ScriptTypeCreateBookList] = new DotNetHook<ICreateBookList>(PluginEngine.ScriptTypeCreateBookList),
			[PluginEngine.ScriptTypeParseComicPath] = new DotNetHook<IParseComicPath>(PluginEngine.ScriptTypeParseComicPath),
			[PluginEngine.ScriptTypeLibrary] = new DotNetHook<ILibrary>(PluginEngine.ScriptTypeLibrary),
			[PluginEngine.ScriptTypeEditor] = new DotNetHook<IEditor>(PluginEngine.ScriptTypeEditor),
			[PluginEngine.ScriptTypeBooks] = new DotNetHook<IBooks>(PluginEngine.ScriptTypeBooks),
			[PluginEngine.ScriptTypeNewBooks] = new DotNetHook<INewBooks>(PluginEngine.ScriptTypeNewBooks),
			[PluginEngine.ScriptTypeBookOpened] = new DotNetHook<IBookOpened>(PluginEngine.ScriptTypeBookOpened),
			[PluginEngine.ScriptTypeReaderResized] = new DotNetHook<IReaderResized>(PluginEngine.ScriptTypeReaderResized),
			[PluginEngine.ScriptTypeSearch] = new DotNetHook<ISearch>(PluginEngine.ScriptTypeSearch),
			[PluginEngine.ScriptTypeConfig] = new DotNetHook<IConfig>(PluginEngine.ScriptTypeConfig),
			[PluginEngine.ScriptTypeStartup] = new DotNetHook<IStartup>(PluginEngine.ScriptTypeStartup),
			[PluginEngine.ScriptTypeShutdown] = new DotNetHook<IShutdown>(PluginEngine.ScriptTypeShutdown),
			[PluginEngine.ScriptTypeComicInfoHtml] = new DotNetHook<IComicInfoHtml>(PluginEngine.ScriptTypeComicInfoHtml),
			[PluginEngine.ScriptTypeComicInfoUI] = new DotNetHook<IComicInfoUI>(PluginEngine.ScriptTypeComicInfoUI),
			[PluginEngine.ScriptTypeQuickOpenHtml] = new DotNetHook<IQuickOpenHtml>(PluginEngine.ScriptTypeQuickOpenHtml),
			[PluginEngine.ScriptTypeQuickOpenUI] = new DotNetHook<IQuickOpenUI>(PluginEngine.ScriptTypeQuickOpenUI),
			[PluginEngine.ScriptTypeDrawThumbnailOverlay] = new DotNetHook<IDrawThumbnailOverlay>(PluginEngine.ScriptTypeDrawThumbnailOverlay)
		};

		private readonly MethodInfo execute;

		private readonly string pluginFile;

		private DotNetCommand(DotNetPlugin plugin, IDotNetHook hook, string pluginFile)
		{
			Plugin = plugin;
			this.pluginFile = pluginFile;
			execute = hook.HookType.GetMethod(Method);
			Key = plugin.Key;
			Name = plugin.Name;
			Description = plugin.Description;
			Image = plugin.Image;
			Enabled = plugin.Enabled;
			PCount = hook.HookType.GetProperty("PCount")?.GetValue(plugin) as int? ?? 0;
			Hook = hook.Hook;
		}

		public DotNetPlugin Plugin { get; }

		private string ConfigFileName => pluginFile + "-" + Key + ".config";

		// pluginFile is the file that stands for the plugin on disk: the DLL of a compiled plugin, the .csplugin file of a source plugin.
		public static IEnumerable<DotNetCommand> CreateAll(DotNetPlugin plugin, string pluginFile)
		{
			return hookTypes.Values.Where(hook => hook.HookType.IsInstanceOfType(plugin)).Select(hook => new DotNetCommand(plugin, hook, pluginFile));
		}

		protected override void Log(string text, params object[] o)
		{
			LogDefault(text, o);
		}

		protected override object OnInvoke(object[] data)
		{
			Log($"Calling '{Key}.{Hook}'...");
			try
			{
				return execute.Invoke(Plugin, data);
			}
			catch (TargetInvocationException e) when (e.InnerException != null)
			{
				Log($"{Key}.{Hook} failed: {e.InnerException.Message}");
				ExceptionDispatchInfo.Capture(e.InnerException).Throw();
				throw;
			}
			catch (Exception e)
			{
				Log($"{Key}.{Hook} failed: {e.Message}");
				throw;
			}
		}

		protected override void OnInitialize(IPluginEnvironment env, string path)
		{
			base.OnInitialize(env, path);
			if (CommandImage == null && !string.IsNullOrEmpty(Plugin.Image))
			{
				CommandImage = LoadEmbeddedImage();
			}
			try
			{
				Plugin.Load(Environment, path);
			}
			catch (Exception e)
			{
				Log($"Skipped '{Key}.{Hook}' from '{pluginFile}': OnLoad failed: {e.Message}");
				throw;
			}
		}

		// The base class loads the image from the plugin folder; this fills in a missing file from the plugin assembly, so a single DLL can carry its image.
		private System.Drawing.Image LoadEmbeddedImage()
		{
			string file = Path.GetFileName(Plugin.Image);
			var candidates = new List<string>();
			if (Environment != null && Environment.Theme.CurrentTheme != Themes.Default)
			{
				candidates.Add(Environment.Theme.CurrentTheme + file);
			}
			candidates.Add(file);
			Assembly assembly = Plugin.GetType().Assembly;
			string[] resources = assembly.GetManifestResourceNames();
			foreach (string candidate in candidates)
			{
				string resource = resources.FirstOrDefault(r => r.Equals(candidate, StringComparison.OrdinalIgnoreCase) || r.EndsWith("." + candidate, StringComparison.OrdinalIgnoreCase));
				if (resource == null)
				{
					continue;
				}
				try
				{
					using (Stream stream = assembly.GetManifestResourceStream(resource))
					using (System.Drawing.Image source = System.Drawing.Image.FromStream(stream))
					{
						return source.CreateCopy(alwaysTrueCopy: true);
					}
				}
				catch
				{
					return null;
				}
			}
			return null;
		}

		public override string LoadConfig()
		{
			try
			{
				return File.ReadAllText(ConfigFileName);
			}
			catch
			{
				return string.Empty;
			}
		}

		public override bool SaveConfig(string config)
		{
			try
			{
				File.WriteAllText(ConfigFileName, config);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
