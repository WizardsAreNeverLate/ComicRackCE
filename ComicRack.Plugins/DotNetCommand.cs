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

		private static readonly Dictionary<string, Type> hookSignatures = new Dictionary<string, Type>
		{
			{ PluginEngine.ScriptTypeLibrary, typeof(Action<ComicBook[]>) },
			{ PluginEngine.ScriptTypeBooks, typeof(Action<ComicBook[]>) },
			{ PluginEngine.ScriptTypeNewBooks, typeof(Action<ComicBook[]>) },
			{ PluginEngine.ScriptTypeEditor, typeof(Action<ComicBook[]>) },
			{ PluginEngine.ScriptTypeParseComicPath, typeof(Action<string, ComicNameInfo>) },
			{ PluginEngine.ScriptTypeBookOpened, typeof(Action<ComicBook>) },
			{ PluginEngine.ScriptTypeCreateBookList, typeof(Func<ComicBook[], string, string, IEnumerable<ComicBook>>) },
			{ PluginEngine.ScriptTypeReaderResized, typeof(Action<int, int>) },
			{ PluginEngine.ScriptTypeSearch, typeof(Func<string, string, int, Dictionary<string, string>>) },
			{ PluginEngine.ScriptTypeConfig, typeof(Action) },
			{ PluginEngine.ScriptTypeStartup, typeof(Action) },
			{ PluginEngine.ScriptTypeShutdown, typeof(Func<bool, bool>) },
			{ PluginEngine.ScriptTypeComicInfoHtml, typeof(Func<ComicBook[], string>) },
			{ PluginEngine.ScriptTypeQuickOpenHtml, typeof(Func<ComicBook[], string>) },
			{ PluginEngine.ScriptTypeComicInfoUI, typeof(Func<Control>) },
			{ PluginEngine.ScriptTypeQuickOpenUI, typeof(Func<Control>) },
			{ PluginEngine.ScriptTypeDrawThumbnailOverlay, typeof(Action<ComicBook, Graphics, Rectangle, int>) }
		};

		private readonly MethodInfo method;

		private readonly Target target;

		private DotNetCommand(PluginCommandAttribute attribute, MethodInfo method, Target target)
		{
			this.method = method;
			this.target = target;
			Hook = attribute.Hook;
			Name = attribute.Name;
			Key = attribute.Key;
			Description = attribute.Description;
			Image = attribute.Image;
			Enabled = attribute.Enabled;
			PCount = attribute.PCount;
		}

		private string MethodName => method.DeclaringType.FullName + "." + method.Name;

		public static DotNetCommand Create(PluginCommandAttribute attribute, MethodInfo method, Target target, out string error)
		{
			error = GetSignatureError(method, attribute.Hook);
			if (error == null && !method.IsStatic && target == null)
			{
				error = "the class needs a public constructor taking IPluginEnvironment or no arguments";
			}
			return error == null ? new DotNetCommand(attribute, method, method.IsStatic ? null : target) : null;
		}

		private static string GetSignatureError(MethodInfo method, string hook)
		{
			if (method.ContainsGenericParameters)
			{
				return "generic methods and types are not supported";
			}
			string[] hooks = (hook ?? string.Empty).Split(',').Select(h => h.Trim()).Where(h => h.Length > 0).ToArray();
			if (hooks.Length == 0)
			{
				return "no hook given";
			}
			foreach (string h in hooks)
			{
				if (!hookSignatures.TryGetValue(h, out Type signature))
				{
					return $"unknown hook '{h}'";
				}
				if (!Matches(method, signature.GetMethod("Invoke")))
				{
					return $"signature does not match hook '{h}', expected {FormatType(signature)}";
				}
			}
			return null;
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
			if (string.IsNullOrEmpty(Key))
			{
				Key = MethodName;
			}
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
