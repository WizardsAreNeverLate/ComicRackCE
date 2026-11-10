using System.Text;

namespace SampleLibrary
{
	public static class Slug
	{
		// "The Walking Dead #1" becomes "the-walking-dead-1".
		public static string From(string text)
		{
			var slug = new StringBuilder();
			foreach (char c in (text ?? string.Empty).ToLowerInvariant())
			{
				if (char.IsLetterOrDigit(c))
				{
					slug.Append(c);
				}
				else if (slug.Length > 0 && slug[slug.Length - 1] != '-')
				{
					slug.Append('-');
				}
			}
			return slug.ToString().TrimEnd('-');
		}
	}
}
