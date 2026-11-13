using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace cYo.Projects.ComicRack.Plugins
{
	internal sealed class AttributePluginInitializer : PluginInitializer
	{
		private static readonly string pluginApiName = typeof(Command).Assembly.GetName().Name;

		public override IEnumerable<Command> GetCommands(string file)
		{
			if (!".dll".Equals(Path.GetExtension(file), StringComparison.OrdinalIgnoreCase) || !ReferencesPluginApi(file))
			{
				return Enumerable.Empty<Command>();
			}
			try
			{
				Assembly assembly = Assembly.LoadFrom(file);
				if (!string.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
				{
					Command.LogDefault("Skipped .NET plugin '{0}': an assembly with the same identity is already loaded from '{1}'", file, assembly.Location);
					return Enumerable.Empty<Command>();
				}
				return GetLoadableTypes(file, assembly).Where(type => type.IsVisible).SelectMany(type => GetTypeCommands(file, type)).ToList();
			}
			catch (FileLoadException e) when (e.HResult == BlockedByZoneHResult)
			{
				Command.LogDefault("Windows blocked the .NET plugin '{0}' because it came from the internet. Open the file's Properties and select Unblock, or install the plugin from a .crplugin package.", file);
				return Enumerable.Empty<Command>();
			}
			catch (Exception e)
			{
				Command.LogDefault("Failed to load .NET plugin '{0}': {1}", file, e.Message);
				return Enumerable.Empty<Command>();
			}
		}

		// COR_E_NOTSUPPORTED: .NET Framework refuses LoadFrom of a file that carries the internet zone mark.
		private const int BlockedByZoneHResult = unchecked((int)0x80131515);

		private static IEnumerable<Type> GetLoadableTypes(string file, Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				foreach (string message in e.LoaderExceptions.Select(le => le.Message).Distinct())
				{
					Command.LogDefault("Some types in .NET plugin '{0}' could not be loaded: {1}", file, message);
				}
				return e.Types.Where(type => type != null);
			}
		}

		private static bool ReferencesPluginApi(string file)
		{
			try
			{
				return ReflectionOnlyLoad(file).GetReferencedAssemblies().Any(a => a.Name == pluginApiName);
			}
			catch (Exception)
			{
				return false;
			}
		}

		// A second ReflectionOnlyLoadFrom of an identity already loaded from another path throws, so reuse the first one.
		private static Assembly ReflectionOnlyLoad(string file)
		{
			string fullName = AssemblyName.GetAssemblyName(file).FullName;
			return AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().FirstOrDefault(a => a.FullName == fullName) ?? Assembly.ReflectionOnlyLoadFrom(file);
		}

		private static IEnumerable<Command> GetTypeCommands(string file, Type type)
		{
			AttributePluginCommand.Target target = AttributePluginCommand.Target.For(type);
			foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
			{
				PluginCommandAttribute attribute = method.GetCustomAttribute<PluginCommandAttribute>();
				if (attribute == null)
				{
					continue;
				}
				List<AttributePluginCommand> created;
				try
				{
					created = AttributePluginCommand.Create(attribute, method, target).ToList();
				}
				catch (InvalidPluginException e)
				{
					Command.LogDefault("Skipped .NET plugin command '{0}.{1}' in '{2}': {3}", type.FullName, method.Name, file, e.Message);
					continue;
				}
				foreach (AttributePluginCommand command in created)
				{
					yield return command;
				}
			}
		}
	}
}
