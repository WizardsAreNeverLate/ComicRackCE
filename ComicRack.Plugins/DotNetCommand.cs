using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using cYo.Projects.ComicRack.Engine;

namespace cYo.Projects.ComicRack.Plugins
{
	internal sealed class DotNetCommand : Command
	{
		internal sealed class Target
		{
			private readonly ConstructorInfo constructor;

			private readonly object gate = new object();

			private object value;

			private Target(ConstructorInfo constructor)
			{
				this.constructor = constructor;
			}

			public static Target For(Type type)
			{
				if (type.IsAbstract || type.ContainsGenericParameters)
				{
					return null;
				}
				ConstructorInfo constructor = type.GetConstructor(new[] { typeof(IPluginEnvironment) }) ?? type.GetConstructor(Type.EmptyTypes);
				return constructor == null ? null : new Target(constructor);
			}

			public object Get(IPluginEnvironment env)
			{
				lock (gate)
				{
					return value ??= constructor.Invoke(constructor.GetParameters().Length == 0 ? null : new object[] { env });
				}
			}
		}

		private static readonly (PluginHook Hook, string Name, Type Signature)[] hooks =
		{
			(PluginHook.Library, PluginEngine.ScriptTypeLibrary, typeof(Action<ComicBook[]>)),
			(PluginHook.Books, PluginEngine.ScriptTypeBooks, typeof(Action<ComicBook[]>)),
			(PluginHook.NewBooks, PluginEngine.ScriptTypeNewBooks, typeof(Action<ComicBook[]>)),
			(PluginHook.Editor, PluginEngine.ScriptTypeEditor, typeof(Action<ComicBook[]>)),
			(PluginHook.ParseComicPath, PluginEngine.ScriptTypeParseComicPath, typeof(Action<string, ComicNameInfo>)),
			(PluginHook.BookOpened, PluginEngine.ScriptTypeBookOpened, typeof(Action<ComicBook>)),
			(PluginHook.CreateBookList, PluginEngine.ScriptTypeCreateBookList, typeof(Func<ComicBook[], string, string, IEnumerable<ComicBook>>)),
			(PluginHook.ReaderResized, PluginEngine.ScriptTypeReaderResized, typeof(Action<int, int>)),
			(PluginHook.Search, PluginEngine.ScriptTypeSearch, typeof(Func<string, string, int, Dictionary<string, string>>)),
			(PluginHook.Startup, PluginEngine.ScriptTypeStartup, typeof(Action)),
			(PluginHook.Shutdown, PluginEngine.ScriptTypeShutdown, typeof(Func<bool, bool>)),
			(PluginHook.ComicInfoHtml, PluginEngine.ScriptTypeComicInfoHtml, typeof(Func<ComicBook[], string>)),
			(PluginHook.QuickOpenHtml, PluginEngine.ScriptTypeQuickOpenHtml, typeof(Func<ComicBook[], string>)),
			(PluginHook.ComicInfoUI, PluginEngine.ScriptTypeComicInfoUI, typeof(Func<Control>)),
			(PluginHook.QuickOpenUI, PluginEngine.ScriptTypeQuickOpenUI, typeof(Func<Control>)),
			(PluginHook.DrawThumbnailOverlay, PluginEngine.ScriptTypeDrawThumbnailOverlay, typeof(Action<ComicBook, Graphics, Rectangle, int>))
		};

		private readonly MethodInfo method;

		private readonly Target target;

		private DotNetCommand(PluginCommandAttribute attribute, MethodInfo method, Target target, string hook)
			: this(method, target, hook, string.IsNullOrEmpty(attribute.Key) ? FullName(method) : attribute.Key)
		{
			Name = attribute.Name;
			Description = attribute.Description;
			Image = attribute.Image;
			Enabled = attribute.Enabled;
			PCount = attribute.PCount;
		}

		private DotNetCommand(MethodInfo method, Target target, string hook, string key)
		{
			this.method = method;
			this.target = target;
			Hook = hook;
			Key = key;
		}

		private string MethodName => FullName(method);

		private static string FullName(MethodInfo method) => method.DeclaringType.FullName + "." + method.Name;

		// A Configure command carries the same Key as its main command so the engine pairs them like it does for Python.
		public static IEnumerable<DotNetCommand> Create(PluginCommandAttribute attribute, MethodInfo method, Target target)
		{
			CheckCallable(method, target);
			var selected = Select(attribute.Hook);
			foreach (var hook in selected)
			{
				if (!Matches(method, hook.Signature.GetMethod("Invoke")))
				{
					throw new InvalidPluginException($"signature does not match hook '{hook.Name}', expected {FormatType(hook.Signature)}");
				}
			}
			var main = new DotNetCommand(attribute, method, method.IsStatic ? null : target, string.Join(",", selected.Select(hook => hook.Name)));
			yield return main;
			if (!string.IsNullOrEmpty(attribute.Configure))
			{
				MethodInfo configure = FindConfigure(method.DeclaringType, attribute.Configure);
				CheckCallable(configure, target);
				yield return new DotNetCommand(configure, configure.IsStatic ? null : target, PluginEngine.ScriptTypeConfig, main.Key);
			}
		}

		private static MethodInfo FindConfigure(Type type, string name)
		{
			MethodInfo[] candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => m.Name == name).ToArray();
			if (candidates.Length == 0)
			{
				throw new InvalidPluginException($"configure method '{name}' not found; it must be a public method declared in {type.FullName}");
			}
			if (candidates.Length > 1)
			{
				throw new InvalidPluginException($"configure method '{name}' is overloaded");
			}
			if (!Matches(candidates[0], typeof(Action).GetMethod("Invoke")))
			{
				throw new InvalidPluginException($"configure method '{name}' must have the signature {FormatType(typeof(Action))}");
			}
			return candidates[0];
		}

		private static void CheckCallable(MethodInfo method, Target target)
		{
			if (method.ContainsGenericParameters)
			{
				throw new InvalidPluginException("generic methods and types are not supported");
			}
			if (!method.IsStatic && target == null)
			{
				throw new InvalidPluginException("the class needs a public constructor taking IPluginEnvironment or no arguments");
			}
		}

		private static (PluginHook Hook, string Name, Type Signature)[] Select(PluginHook value)
		{
			var selected = hooks.Where(hook => value.HasFlag(hook.Hook)).ToArray();
			PluginHook unknown = selected.Aggregate(value, (rest, hook) => rest & ~hook.Hook);
			if (unknown != PluginHook.None)
			{
				throw new InvalidPluginException($"unknown hook value {(int)unknown}");
			}
			if (selected.Length == 0)
			{
				throw new InvalidPluginException("no hook given");
			}
			return selected;
		}

		private static bool Matches(MethodInfo method, MethodInfo signature)
		{
			ParameterInfo[] expected = signature.GetParameters();
			ParameterInfo[] actual = method.GetParameters();
			if (expected.Length != actual.Length || !expected.Zip(actual, (e, a) => a.ParameterType.IsAssignableFrom(e.ParameterType)).All(ok => ok))
			{
				return false;
			}
			if (signature.ReturnType == typeof(void))
			{
				return method.ReturnType == typeof(void);
			}
			return signature.ReturnType.IsAssignableFrom(method.ReturnType);
		}

		private static string FormatType(Type type)
		{
			if (!type.IsGenericType)
			{
				return type.Name;
			}
			return type.Name.Substring(0, type.Name.IndexOf('`')) + "<" + string.Join(", ", type.GetGenericArguments().Select(FormatType)) + ">";
		}

		protected override void MakeDefaults()
		{
			if (string.IsNullOrEmpty(Name))
			{
				Name = method.Name;
			}
		}

		protected override void Log(string text, params object[] o)
		{
			LogDefault(text, o);
		}

		protected override object OnInvoke(object[] data)
		{
			try
			{
				return method.Invoke(target?.Get(Environment), data);
			}
			catch (TargetInvocationException e) when (e.InnerException != null)
			{
				Log("'{0}' failed: {1}", MethodName, e.InnerException.Message);
				ExceptionDispatchInfo.Capture(e.InnerException).Throw();
				throw;
			}
		}
	}
}
