using System;

namespace cYo.Projects.ComicRack.Plugins
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class PluginCommandAttribute : Attribute
	{
		public PluginCommandAttribute(PluginHook hook)
		{
			Hook = hook;
		}

		public PluginHook Hook
		{
			get;
		}

		public string Name
		{
			get;
			set;
		}

		public string Key
		{
			get;
			set;
		}

		public string Configure
		{
			get;
			set;
		}

		public string Description
		{
			get;
			set;
		}

		public string Image
		{
			get;
			set;
		}

		public bool Enabled
		{
			get;
			set;
		} = true;

		public int PCount
		{
			get;
			set;
		}
	}
}
