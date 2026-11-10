using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace cYo.Projects.ComicRack.Plugins
{
	// The one place that turns a plugin assembly into commands. Compiled plugins and source plugins both end here.
	internal static class DotNetPluginLoader
	{
		// pluginFile is the file that stands for the plugin on disk: the DLL of a compiled plugin, the .csplugin file of a source plugin.
		public static IEnumerable<Command> CreateCommands(Assembly assembly, string pluginFile)
		{
			var commands = new List<Command>();
			foreach (Type type in GetLoadableTypes(assembly, pluginFile).Where(type => type.IsClass))
			{
				try
				{
					DotNetPluginClass plugin = ReadPluginClass(type, pluginFile, out string problem);
					if (problem != null)
					{
						Command.LogDefault("Skipped .NET plugin class '{0}' in '{1}': {2}", type.FullName, pluginFile, problem);
					}
					if (plugin != null)
					{
						commands.AddRange(DotNetCommand.CreateAll(plugin));
					}
				}
				catch (Exception e)
				{
					Command.LogDefault("Skipped .NET plugin class '{0}' in '{1}': {2}", type.FullName, pluginFile, e.Message);
				}
			}
			return commands;
		}

		// Reads the attribute only. The class is not created here, so a scan runs no plugin code.
		private static DotNetPluginClass ReadPluginClass(Type type, string pluginFile, out string problem)
		{
			problem = null;
			bool hasHooks = DotNetCommand.ImplementsHook(type);
			PluginAttribute attribute = type.GetCustomAttribute<PluginAttribute>(inherit: false);
			if (attribute == null)
			{
				// Abstract and open generic classes are base classes for plugins and need no attribute.
				if (hasHooks && !type.IsAbstract && !type.ContainsGenericParameters)
				{
					problem = "it implements a hook interface but has no [Plugin] attribute";
				}
				return null;
			}
			if (!type.IsVisible || type.IsAbstract || type.ContainsGenericParameters)
			{
				problem = "a plugin class must be public, not abstract and not generic";
				return null;
			}
			if (!hasHooks)
			{
				problem = "it implements no hook interface";
				return null;
			}
			if (string.IsNullOrWhiteSpace(attribute.Key))
			{
				problem = "the Key is empty";
				return null;
			}
			DotNetPluginClass plugin = DotNetPluginClass.Create(type, attribute, pluginFile);
			if (plugin == null)
			{
				problem = "it needs a public constructor that takes an IPluginContext or no parameters";
			}
			return plugin;
		}

		private static IEnumerable<Type> GetLoadableTypes(Assembly assembly, string pluginFile)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				foreach (string message in e.LoaderExceptions.Select(le => le.Message).Distinct())
				{
					Command.LogDefault("Some types in .NET plugin '{0}' could not be loaded: {1}", pluginFile, message);
				}
				return e.Types.Where(type => type != null);
			}
		}
	}
}
