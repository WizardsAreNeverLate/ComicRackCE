using System;

namespace cYo.Projects.ComicRack.Plugins
{
	/// <summary>
	/// Marks a class as a .NET plugin and describes it. The class also implements one or more hook interfaces (<see cref="IDotNetCommand"/>).
	/// </summary>
	/// <remarks>
	/// The class must be public, not abstract and not generic. It needs a public constructor that takes an <see cref="IPluginContext"/>,
	/// or a public constructor without parameters. If it has both, ComicRack uses the one with the context.
	/// ComicRack reads this attribute when it scans the script folders and does not create the class then.
	/// It creates one instance when a hook of the class runs for the first time. All hooks of the class share this instance, and it lives until ComicRack closes.
	/// A plugin that is disabled is never created.
	/// If the constructor throws, every hook of the class fails with that exception and ComicRack does not call the constructor again.
	/// If the class implements <see cref="IDisposable"/>, ComicRack calls Dispose when it closes, but only if it created the instance.
	/// The attribute is not inherited: a class that derives from a plugin class needs its own attribute.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class PluginAttribute : Attribute
	{
		/// <param name="key">The unique name of the plugin. It must not be empty. ComicRack skips a plugin whose key another plugin or script already uses.</param>
		public PluginAttribute(string key)
		{
			Key = key;
		}

		public string Key { get; }

		/// <summary>The name that ComicRack shows. The default is the <see cref="Key"/>.</summary>
		public string Name
		{
			get => field ?? Key;
			set;
		}

		public string Description { get; set; }

		/// <summary>
		/// File name of the command image. ComicRack looks in the plugin folder first, then in the embedded resources of the plugin assembly.
		/// </summary>
		public string Image { get; set; }

		/// <summary>False to install the plugin disabled. The user can enable it in the Preferences. The default is true.</summary>
		public bool Enabled { get; set; } = true;

		/// <summary>The number of parameters (0 to 2) the smart list editor asks for. Only <see cref="ICreateBookList"/> uses it.</summary>
		public int PCount { get; set; }
	}
}
