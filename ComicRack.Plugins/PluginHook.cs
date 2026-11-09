using System;

namespace cYo.Projects.ComicRack.Plugins
{
	[Flags]
	public enum PluginHook
	{
		None = 0,
		Library = 1 << 0,
		Books = 1 << 1,
		NewBooks = 1 << 2,
		Editor = 1 << 3,
		ParseComicPath = 1 << 4,
		BookOpened = 1 << 5,
		CreateBookList = 1 << 6,
		ReaderResized = 1 << 7,
		Search = 1 << 8,
		Config = 1 << 9,
		Startup = 1 << 10,
		Shutdown = 1 << 11,
		ComicInfoHtml = 1 << 12,
		QuickOpenHtml = 1 << 13,
		ComicInfoUI = 1 << 14,
		QuickOpenUI = 1 << 15,
		DrawThumbnailOverlay = 1 << 16
	}
}
