using System;

namespace cYo.Projects.ComicRack.Plugins
{
	internal sealed class InvalidPluginException : Exception
	{
		public InvalidPluginException(string message)
			: base(message)
		{
		}
	}
}
