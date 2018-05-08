// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using SIL.IO;
using SIL.Xml;

namespace SIL.FieldWorks.Common.FwUtils
{
	/// <summary>
	/// Summary description for DynamicLoader.
	/// </summary>
	public static class DynamicLoader
	{
		/// <summary>
		/// Dynamically find an assembly and create an object of the name to class.
		/// </summary>
		/// <param name="parentConfigNode">A parent node that must have one child node named 'dynamicloaderinfo', which contains the two required attributes.</param>
		/// <returns></returns>
		public static object CreateObjectUsingLoaderNode(XElement parentConfigNode)
		{
			var dynLoaderNode = parentConfigNode.Element("dynamicloaderinfo");
			if (dynLoaderNode == null)
				throw new ArgumentException(@"Required 'dynamicloaderinfo' XML node not found.", "parentConfigNode");

			return CreateObject(dynLoaderNode);
		}

		// Return the class of object that will be created if CreateObjectUsingLoaderNode is called with this argument.
		// Return null if dynamic loader node not found or if it doesn't specify a valid class.
		public static Type TypeForLoaderNode(XElement parentConfigNode)
		{
			var configuration = parentConfigNode.Element("dynamicloaderinfo");
			if (configuration == null)
				return null;
			string assemblyPath = XmlUtils.GetMandatoryAttributeValue(configuration, "assemblyPath");
			if (assemblyPath == "null")
				return null;
			string className = XmlUtils.GetMandatoryAttributeValue(configuration, "class");
			Assembly assembly;
			GetAssembly(assemblyPath, out assembly);
			return assembly.GetType(className.Trim());
		}

		/// <summary>
		/// Dynamically find an assembly and create an object of the name to class.
		/// The XmlNode must have attributes assemblyPath (typically just a dll name without a path,
		/// if it is in the same location as the XmlUtils.dll), and class, the fully qualified class name.
		/// It may also have a child node "args" with a sequence of "arg" children, each of which
		/// specifies a name and value; these seem to be basically ignored except that if one of
		/// them has the name xpathToConfigurationNode, the value is used as an xpath (relative to the
		/// input configuration) to find a node that is passed as an argument to the constructor.
		/// </summary>
		/// <returns></returns>
		public static object CreateObject(XmlNode configuration)
		{
			return CreateObject(configuration, CreateArgs(configuration));
		}

		private static object[] CreateArgs(XmlNode configuration)
		{
			List<object> argList = new List<object>();
			// see if we can find "args" children that specify arguments to pass in.
			if (configuration != null && configuration.HasChildNodes)
			{
				XmlNodeList argNodes = configuration.SelectNodes("args/arg");
				if (argNodes.Count > 0)
				{
					Dictionary<string, string> argDict = new Dictionary<string, string>();
					foreach (XmlNode argNode in argNodes)
					{
						string argName = XmlUtils.GetMandatoryAttributeValue(argNode, "name");
						string argVal = XmlUtils.GetMandatoryAttributeValue(argNode, "value");
						argDict.Add(argName, argVal);
					}
					string argValue;
					if (argDict.TryGetValue("xpathToConfigurationNode", out argValue))
					{
						// "xpathToConfigurationNode" is a special argument for passing the nodes
						// that the object we're creating knows how to process.
						// NOTE: assume the xpath is with respect to the dynamicloaderinfo "configuration" node
						XmlNode configNodeForObject = configuration.SelectSingleNode(argValue);
						if (configNodeForObject != null)
							argList.Add(configNodeForObject);
					}
				}
			}
			return argList.Count > 0 ? argList.ToArray() : null;
		}

		/// <summary>
		/// Dynamically find an assembly and create an object of the name to class.
		/// configuration has assemblyPath and class (fully qualified) as in other overloads.
		/// The constructor arguments are supplied explicitly.
		/// </summary>
		/// <returns></returns>
		public static object CreateObject(XElement configuration, params object[] args)
		{
			var assemblyPath = XmlUtils.GetMandatoryAttributeValue(configuration, "assemblyPath");
			// JohnT: see AddAssemblyPathInfo. We use this when the object we're trying to persist
			// as a child of another object is null.
			if (assemblyPath == "null")
				return null;
			var className = XmlUtils.GetMandatoryAttributeValue(configuration, "class");
			return CreateObject(assemblyPath, className, args);
		}

		/// <summary>
		/// Dynamically find an assembly and create an object of the name to class.
		/// configuration has assemblyPath and class (fully qualified) as in other overloads.
		/// The constructor arguments are supplied explicitly.
		/// </summary>
		/// <returns></returns>
		public static object CreateObject(XmlNode configuration, params object[] args)
		{
			string  assemblyPath = XmlUtils.GetMandatoryAttributeValue(configuration, "assemblyPath");
			// JohnT: see AddAssemblyPathInfo. We use this when the object we're trying to persist
			// as a child of another object is null.
			if (assemblyPath == "null")
				return null;
			string className = XmlUtils.GetMandatoryAttributeValue(configuration, "class");
			return CreateObject(assemblyPath, className, args);
		}
		/// <summary>
		/// Dynamically find an assembly and create an object of the name to class.
		/// </summary>
		public static object CreateObject(string assemblyPath, string className)
		{
			return CreateObject(assemblyPath, className, null);
		}

		private static string CouldNotCreateObjectMsg(string assemblyPath, string className)
		{
			return "Found the DLL "
				+ assemblyPath
				+ " but could not create the class: "
				+ className
				+ ". If there are no 'InnerExceptions' below, then make sure capitalization is correct and that you include the name space.";
		}

		private static object CreateObject(string assemblyPath1, string className1, BindingFlags flags, params object[] args)
		{
			Assembly assembly;
			string assemblyPath = GetAssembly(assemblyPath1, out assembly);

			string className = className1.Trim();
			Object thing = null;
			try
			{
				//make the object
				//Object thing = assembly.CreateInstance(className);
				thing = assembly.CreateInstance(className, false, flags,
					null, args, null, null);
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);
				var bldr = new StringBuilder(CouldNotCreateObjectMsg(assemblyPath, className));

				Exception inner = err;

				while (inner != null)
				{
					bldr.AppendLine();
					bldr.Append("Inner exception message = " + inner.Message);
					inner = inner.InnerException;
				}
				throw new FwConfigurationException(bldr.ToString(), err);
			}
			if (thing == null)
			{
				// Bizarrely, CreateInstance is not specified to throw an exception if it can't
				// find the specified class. But we want one.
				throw new FwConfigurationException(CouldNotCreateObjectMsg(assemblyPath, className));
			}
			return thing;

		}

		public static object CreateNonPublicObject(string assemblyPath1, string className1, params object[] args)
		{
			return CreateObject(assemblyPath1, className1, BindingFlags.Instance | BindingFlags.NonPublic, args);
		}

		/// <summary>
		/// Dynamically find an assembly and create an object of the name to class.
		/// </summary>
		/// <param name="assemblyPath1"></param>
		/// <param name="className1"></param>
		/// <param name="args">args to the constructor</param>
		/// <returns></returns>
		public static object CreateObject(string assemblyPath1, string className1, params object[] args)
		{
			return CreateObject(assemblyPath1, className1, BindingFlags.Instance | BindingFlags.Public, args);
		}

		public static List<T> GetPlugins<T>(string pattern) where T: class
		{
			var codeBasePath = FileLocationUtilities.DirectoryOfTheApplicationExecutable;
			return GetPlugins<T>(codeBasePath, pattern);
		}
		/// <summary>
		/// Return a newly created instance of every type in every DLL in the specified directory which implements the indicated type.
		/// Typically type is an interface, but I think it would work if it is a base class.
		/// (Adapted from http://blogs.msdn.com/b/abhinaba/archive/2005/11/14/492458.aspx)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="folder"></param>
		/// <param name="pattern">Pattern for interesting DLLs in folder. Should end in .dll </param>
		/// <returns></returns>
		public static List<T> GetPlugins<T>(string folder, string pattern) where T : class
		{
			string[] files = Directory.GetFiles(folder, pattern);

			var tList = new List<T>();

			foreach (string file in files)
			{
				try
				{
					// We use LoadFrom rather than LoadFile so that if we have already loaded this assembly, we don't load another copy of it.
					// This is necessary to let our test pass, since otherwise the interface is defined in one copy of the assembly, while
					// the class we load is considered to implement the other copy of that interface in the second copy of the assembly.
					// In real use, this would be a problem if the DLL defining the interface was also one of the ones providing implementations.
					Assembly assembly = Assembly.LoadFrom(file);
					foreach (Type type in assembly.GetTypes())
					{
						if (!type.IsClass || type.IsNotPublic)
							continue;
						if (typeof(T).IsAssignableFrom(type))
						{
							object obj = Activator.CreateInstance(type);
							T t = (T) obj;
							tList.Add(t);
						}
					}
				}
				catch (Exception)
				{
					// Maybe not a .NET DLL? Anyway just ignore it. (Enhance JohnT: any way we can predict what exceptions should be ignored here?
				}
			}
			return tList;
		}

		private static string GetAssembly(string assemblyPath1, out Assembly assembly)
		{
			// Whitespace will cause failures.
			string assemblyPath = assemblyPath1.Trim();
			//allow us to say "assemblyPath="%fwroot%\Src\Foo....  , at least during testing
			// RR: It may allow it, but it crashes, when it can't find the dll.
			//assemblyPath = System.Environment.ExpandEnvironmentVariables(assemblyPath);
			string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			try
			{
				assembly = Assembly.LoadFrom(Path.Combine(baseDir, assemblyPath));
			}
			catch (Exception)
			{
				try
				{
					//Try to find without specifying the directory,
					//so that we find things that are in the Path environment variable
					//This is useful in extension situations where the extension's bin directory
					//is not the same as the FieldWorks binary directory (e.g. WeSay)
					assembly = Assembly.LoadFrom(assemblyPath);
				}
				catch (Exception error)
				{
					throw new RuntimeConfigurationException("Could not find the DLL at :" + assemblyPath, error);
				}
			}
			return assemblyPath;
		}

		/// <summary>
		/// Create the object specified by the assemblyPath and class attributes of node,
		/// and if the resulting object implements IPersistAsXml, call InitXml.
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public static object RestoreObject(XmlNode node)
		{
			object obj = CreateObject(node);
			IPersistAsXml persistObj = obj as IPersistAsXml;
			if (persistObj != null)
				persistObj.InitXml(XElement.Parse(node.OuterXml));
			return obj;
		}

		/// <summary>
		/// Create the object specified by the assemblyPath and class attributes of node,
		/// and if the resulting object implements IPersistAsXml, call InitXml.
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public static object RestoreObject(XElement node)
		{
			object obj = CreateObject(node);
			IPersistAsXml persistObj = obj as IPersistAsXml;
			if (persistObj != null)
				persistObj.InitXml(node.Clone());
			return obj;
		}

		/// <summary>
		/// Create an XmlNode out of the source, and use it to recreate an object.
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		public static object RestoreObject(string source)
		{
			return RestoreObject(XDocument.Parse(source).Root);
		}

		/// <summary>
		/// Return the object obtained by calling RestoreObject on the element
		/// selected from node by xpath.
		/// </summary>
		/// <param name="node"></param>
		/// <param name="xpath"></param>
		/// <returns></returns>
		public static object RestoreFromChild(XElement node, string xpath)
		{
			var child = node.XPathSelectElement(xpath);
			if (child == null)
				throw new Exception("expected child " + xpath);
			return RestoreObject(child);
		}

		/// <summary>
		/// Creates a string representation of the supplied object, an XML string
		/// containing the required assemblyPath and class attributes needed to create an
		/// instance using CreateObject, plus whatever gets added to the node by passsing
		/// it to the PersistAsXml method of the object. The root element name is supplied
		/// as the elementName argument.
		/// </summary>
		public static string PersistObject(object src, string elementName)
		{
			if (src == null)
				return null;
			var obj = src as IPersistAsXml;
			var doc = XDocument.Parse("<" + elementName + "/>");
			var root = doc.Root;
			AddAssemblyClassInfoTo(root, src);
			if (obj != null)
				obj.PersistAsXml(root);
			return root.ToString();
		}

		public static XElement PersistObject(object src, XElement parent, string elementName)
		{
			IPersistAsXml obj = src as IPersistAsXml;
			var node = new XElement(elementName);
			parent.Add(node);
			AddAssemblyClassInfoTo(node, obj);
			if (obj != null)
				obj.PersistAsXml(node);
			return node;
		}

		/// <summary>
		/// Add to the specified node assembly and class information for the specified object.
		/// </summary>
		internal static void AddAssemblyClassInfoTo(XElement node, object obj)
		{
			if (obj == null)
			{
				node.Add(new XAttribute("assemblyPath", "null"));
				return;
			}
			node.Add(new XAttribute("assemblyPath", obj.GetType().Assembly.GetName().Name + ".dll"));
			node.Add(new XAttribute("class", obj.GetType().FullName));
		}
	}

	public interface IPersistAsXml
	{
		/// <summary>
		/// Add to the specified XML node information required to create a new
		/// object equivalent to yourself. The node already contains information
		/// sufficient to create an instance of the proper class.
		/// </summary>
		/// <param name="node"></param>
		void PersistAsXml(XElement node);

		/// <summary>
		/// Initialize an instance into the state indicated by the node, which was
		/// created by a call to PersistAsXml.
		/// </summary>
		/// <param name="node"></param>
		void InitXml(XElement node);
	}

}