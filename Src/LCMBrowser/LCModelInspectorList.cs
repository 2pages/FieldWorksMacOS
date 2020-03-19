// Copyright (c) 2016-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure;

namespace LCMBrowser
{
	/// <summary />
	public class LCModelInspectorList : GenericInspectorObjectList
	{
		private LcmCache m_cache;
		private IFwMetaDataCacheManaged m_mdc;
		private Dictionary<CellarPropertyType, string> m_fldSuffix;

		/// <summary>
		/// Gets or sets a value indicating whether or not to use the field definitions
		/// found in the meta data cache when querying an object for it's properties. If this
		/// value is false, then all of the properties found via .Net reflection are loaded
		/// into inspector objects. Otherwise, only those properties specified in the meta
		/// data cache are loaded.
		/// </summary>
		public bool UseMetaDataCache { get; set; }

		/// <summary />
		public LCModelInspectorList(LcmCache cache)
		{
			m_cache = cache;
			m_mdc = m_cache.ServiceLocator.GetInstance<IFwMetaDataCacheManaged>();
			m_fldSuffix = new Dictionary<CellarPropertyType, string>
			{
				[CellarPropertyType.OwningCollection] = "OC",
				[CellarPropertyType.OwningSequence] = "OS",
				[CellarPropertyType.ReferenceCollection] = "RC",
				[CellarPropertyType.ReferenceSequence] = "RS",
				[CellarPropertyType.OwningAtomic] = "OA",
				[CellarPropertyType.ReferenceAtomic] = "RA"
			};
		}

		#region overridden methods
		/// <summary>
		/// Initializes the list using the specified top level object.
		/// </summary>
		public override void Initialize(object topLevelObj)
		{
			Initialize(topLevelObj, null);
		}

		/// <summary>
		/// Initializes the list using the specified top level object.
		/// </summary>
		public void Initialize(object topLevelObj, Dictionary<object, IInspectorObject> iobectsToKeep)
		{
			base.Initialize(topLevelObj);

			foreach (var io in this)
			{
				if (io.Object == null || io.Object.GetType().GetInterface("IRepository`1") == null)
				{
					continue;
				}
				var pi = io.Object.GetType().GetProperty("Count");
				var count = (int)pi.GetValue(io.Object, null);
				io.DisplayValue = FormatCountString(count);
				io.DisplayName = io.DisplayType;
				io.HasChildren = (count > 0);
			}

			Sort(CompareInspectorObjectNames);
		}

		/// <summary>
		/// Gets a list of IInspectorObject objects for the properties of the specified object.
		/// </summary>
		protected override List<IInspectorObject> GetInspectorObjects(object obj, int level)
		{
			if (obj == null)
			{
				return BaseGetInspectorObjects(null, level);
			}
			var tmpObj = obj;
			var io = obj as IInspectorObject;
			if (io != null)
			{
				tmpObj = io.Object;
			}
			if (tmpObj == null)
			{
				return BaseGetInspectorObjects(obj, level);
			}
			if (tmpObj.GetType().GetInterface("IRepository`1") != null)
			{
				return GetInspectorObjectsForRepository(tmpObj, io, level);
			}
			if (tmpObj is IMultiAccessorBase multiAccessorBase)
			{
				return GetInspectorObjectsForMultiString(multiAccessorBase, io, level);
			}
			if (LCMBrowserForm.m_virtualFlag == false && io?.ParentInspectorObject != null && io.ParentInspectorObject.DisplayName == "Values"
				&& io.ParentInspectorObject.ParentInspectorObject.DisplayType == "MultiUnicodeAccessor")
			{
				return GetInspectorObjectsForUniRuns(tmpObj as ITsString, io, level);
			}
			if (tmpObj is ITsString tsString)
			{
				return GetInspectorObjectsForTsString(tsString, io, level);
			}
			if (LCMBrowserForm.m_virtualFlag == false && tmpObj is TextProps textProps)
			{
				return GetInspectorObjectsForTextProps(textProps, io, level);
			}
			if (LCMBrowserForm.m_virtualFlag == false && io != null && io.DisplayName == "Values" && (io.ParentInspectorObject.DisplayType == "MultiUnicodeAccessor"
																									  || io.ParentInspectorObject.DisplayType == "MultiStringAccessor"))
			{
				return GetInspectorObjectsForValues(tmpObj, io as IInspectorObject, level);
			}
			return io != null && io.DisplayName.EndsWith("RC") && io.Flid > 0 && m_mdc.IsCustom(io.Flid)
				? GetInspectorObjectsForCustomRC(tmpObj, io, level)
				: BaseGetInspectorObjects(obj, level);
		}

		/// <summary>
		/// Gets the inspector objects for the specified repository object;
		/// </summary>
		private List<IInspectorObject> GetInspectorObjectsForRepository(object obj, IInspectorObject ioParent, int level)
		{
			var i = 0;
			var list = new List<IInspectorObject>();
			foreach (var instance in GetRepositoryInstances(obj))
			{
				var io = CreateInspectorObject(instance, obj, ioParent, level);
				switch (LCMBrowserForm.m_virtualFlag)
				{
					case false when obj.ToString().IndexOf("LexSenseRepository") > 0:
					{
						var tmpObj = (ILexSense)io.Object;
						io.DisplayValue = tmpObj.FullReferenceName.Text;
						io.DisplayName = $"[{i++}]: {GetObjectOnly(tmpObj.ToString())}";
						break;
					}
					case false when obj.ToString().IndexOf("LexEntryRepository") > 0:
					{
						var tmpObj = (ILexEntry)io.Object;
						io.DisplayValue = tmpObj.HeadWord.Text;
						io.DisplayName = $"[{i++}]: {GetObjectOnly(tmpObj.ToString())}";
						break;
					}
					default:
						io.DisplayName = $"[{i++}]";
						break;
				}
				list.Add(io);
			}

			i = IndexOf(obj);
			if (i < 0)
			{
				return list;
			}
			this[i].DisplayValue = FormatCountString(list.Count);
			this[i].HasChildren = list.Any();
			return list;
		}

		/// <summary>
		/// Process lines that have a DateTime type.
		/// </summary>
		private List<IInspectorObject> GetInspectorObjectForDateTime(DateTime tmpObj, IInspectorObject ioParent, int level)
		{
			var list = new List<IInspectorObject>();
			var io = CreateInspectorObject(tmpObj, null, ioParent, level);
			io.HasChildren = false;
			list.Add(io);

			return list;
		}

		/// <summary>
		/// Gets the inspector objects for the specified MultiString.
		/// </summary>
		private List<IInspectorObject> GetInspectorObjectsForMultiString(IMultiAccessorBase msa, IInspectorObject ioParent, int level)
		{
			var list = LCMBrowserForm.m_virtualFlag ? BaseGetInspectorObjects(msa, level) : GetMultiStringInspectorObjects(msa, ioParent, level);
			var allStrings = new Dictionary<int, string>();
			try
			{
				// Put this in a try/catch because VirtualStringAccessor
				// didn't implement StringCount when this was written.
				for (var i = 0; i < msa.StringCount; i++)
				{
					int ws;
					var tss = msa.GetStringFromIndex(i, out ws);
					allStrings[ws] = tss.Text;
				}
			}
			catch { }

			if (!LCMBrowserForm.m_virtualFlag)
			{
				return list;
			}
			var io = CreateInspectorObject(allStrings, msa, ioParent, level);
			io.DisplayName = "AllStrings";
			io.DisplayValue = FormatCountString(allStrings.Count);
			io.HasChildren = (allStrings.Count > 0);
			list.Insert(0, io);
			list.Sort((x, y) => x.DisplayName.CompareTo(y.DisplayName));
			return list;
		}

		/// <summary>
		/// Gets a list of IInspectorObject objects (same as base), but includes s lot of
		/// specifics if you choose not to see virtual fields.
		/// </summary>
		private List<IInspectorObject> GetMultiStringInspectorObjects(object obj, IInspectorObject ioParent, int level)
		{
			if (ioParent != null)
			{
				obj = ioParent.Object;
			}
			var list = new List<IInspectorObject>();
			if (obj is ICollection collection)
			{
				var i = 0;
				foreach (var item in collection)
				{
					var io = CreateInspectorObject(item, obj, ioParent, level);
					io.DisplayName = $"[{i++}]";
					list.Add(io);
				}

				return list;
			}

			foreach (var pi in GetPropsForObj(obj))
			{
				try
				{
					var propObj = pi.GetValue(obj, null);
					var inspectorObject = CreateInspectorObject(pi, propObj, obj, ioParent, level);
					if (obj.ToString().IndexOf("MultiUnicodeAccessor") > 0 && inspectorObject.DisplayName != "Values" || obj.ToString().IndexOf("MultiStringAccessor") > 0 && inspectorObject.DisplayName != "Values")
					{
						continue;
					}
					if (inspectorObject.Object is ICollection collection1)
					{
						inspectorObject.DisplayValue = $"Count = {collection1.Count}";
						inspectorObject.HasChildren = (collection1.Count > 0);
					}
					list.Add(inspectorObject);
				}
				catch (Exception e)
				{
				}
			}

			list.Sort(CompareInspectorObjectNames);
			return list;
		}

		/// <summary>
		/// Gets the inspector objects for the specified TsString.
		/// </summary>
		private List<IInspectorObject> GetInspectorObjectsForTsString(ITsString tss, IInspectorObject ioParent, int level)
		{
			var list = new List<IInspectorObject>();
			var runCount = tss.RunCount;
			var tssriList = new List<TsStringRunInfo>();
			for (var i = 0; i < runCount; i++)
			{
				tssriList.Add(new TsStringRunInfo(i, tss, m_cache));
			}
			var io = CreateInspectorObject(tssriList, tss, ioParent, level);
			io.DisplayName = "Runs";
			io.DisplayValue = FormatCountString(tssriList.Count);
			io.HasChildren = (tssriList.Count > 0);
			list.Add(io);

			if (!LCMBrowserForm.m_virtualFlag)
			{
				return list;
			}
			io = CreateInspectorObject(tss.Length, tss, ioParent, level);
			io.DisplayName = "Length";
			list.Add(io);
			io = CreateInspectorObject(tss.Text, tss, ioParent, level);
			io.DisplayName = "Text";
			list.Add(io);

			return list;
		}

		/// <summary>
		/// Gets the inspector objects for the specified TextProps.
		/// </summary>
		private List<IInspectorObject> GetInspectorObjectsForTextProps(TextProps txp, IInspectorObject ioParent, int level)
		{
			if (ioParent != null)
			{
				txp = ioParent.Object as TextProps;
			}
			var list = new List<IInspectorObject>();
			if (txp is ICollection collection)
			{
				var i = 0;
				foreach (var item in collection)
				{
					var io = CreateInspectorObject(item, txp, ioParent, level);
					io.DisplayName = $"[{i++}]";
					list.Add(io);
				}

				return list;
			}

			var saveIntPropCount = 0;
			var saveStrPropCount = 0;
			foreach (var pi in GetPropsForObj(txp))
			{
				if (pi.Name != "IntProps" && pi.Name != "StrProps" && pi.Name != "IntPropCount" && pi.Name != "StrPropCount")
				{
					continue;
				}
				IInspectorObject io;
				switch (pi.Name)
				{
					case "IntProps":
						var propObj = pi.GetValue(txp, null);
						io = CreateInspectorObject(pi, propObj, txp, ioParent, level);
						io.DisplayValue = "Count = " + saveIntPropCount;
						io.HasChildren = (saveIntPropCount > 0);
						list.Add(io);
						break;
					case "StrProps":
						var propObj1 = pi.GetValue(txp, null);
						io = CreateInspectorObject(pi, propObj1, txp, ioParent, level);
						io.DisplayValue = "Count = " + saveStrPropCount;
						io.HasChildren = (saveStrPropCount > 0);
						list.Add(io);
						break;
					case "StrPropCount":
						saveStrPropCount = (int)pi.GetValue(txp, null);
						break;
					case "IntPropCount":
						saveIntPropCount = (int)pi.GetValue(txp, null);
						break;
				}
			}

			list.Sort(CompareInspectorObjectNames);
			return list;
		}

		/// <summary>
		/// Gets a list of IInspectorObject objects representing all the properties for the
		/// specified object, which is assumed to be at the specified level.
		/// </summary>
		protected virtual List<IInspectorObject> GetInspectorObjectsForValues(object obj, IInspectorObject ioParent, int level)
		{
			if (ioParent != null)
			{
				obj = ioParent.Object;
			}
			var list = new List<IInspectorObject>();
			if (ioParent.OwningObject is IMultiAccessorBase multiStr)
			{
				foreach (var ws in multiStr.AvailableWritingSystemIds)
				{
					var wsObj = m_cache.ServiceLocator.WritingSystemManager.Get(ws);
					var ino = CreateInspectorObject(multiStr.get_String(ws), obj, ioParent, level);
					ino.DisplayName = wsObj.DisplayLabel;
					list.Add(ino);
				}
				return list;
			}

			var props = GetPropsForObj(obj);
			foreach (var pi in props)
			{
				try
				{
					var propObj = pi.GetValue(obj, null);
					list.Add(CreateInspectorObject(pi, propObj, obj, ioParent, level));
				}
				catch (Exception e)
				{
					list.Add(CreateExceptionInspectorObject(e, obj, pi.Name, level, ioParent));
				}
			}

			list.Sort(CompareInspectorObjectNames);
			return list;
		}

		/// <summary>
		/// Condenses the 'Run' information for MultiUnicodeAccessor entries because
		/// there will only be 1 run,
		/// </summary>
		protected virtual List<IInspectorObject> GetInspectorObjectsForUniRuns(ITsString obj, IInspectorObject ioParent, int level)
		{
			var list = new List<IInspectorObject>();
			if (obj == null)
			{
				return list;
			}
			var ino = CreateInspectorObject(obj, ioParent.OwningObject, ioParent, level);
			ino.DisplayName = "Writing System";
			ino.DisplayValue = obj.get_WritingSystemAt(0).ToString();
			ino.HasChildren = false;
			list.Add(ino);

			var tss = new TsStringRunInfo(0, obj, m_cache);
			ino = CreateInspectorObject(tss, obj, ioParent, level);
			ino.DisplayName = "Text";
			ino.DisplayValue = tss.Text;
			ino.HasChildren = false;
			list.Add(ino);
			return list;
		}

		/// <summary>
		/// Create the reference collection list for the custom reference collection.
		/// </summary>
		protected virtual List<IInspectorObject> GetInspectorObjectsForCustomRC(object obj, IInspectorObject ioParent, int level)
		{
			if (obj == null)
			{
				return null;
			}
			// Inspectors for custom reference collections are supposed to be configured with
			// obj being an array of the HVOs.
			var collection = obj as ICollection;
			if (collection == null)
			{
				MessageBox.Show("Custom Reference collection not properly configured with array of HVOs");
				return null;
			}
			var list = new List<IInspectorObject>();
			var n = 0;
			// Just like an ordinary reference collection, we want to make one inspector for each
			// item in the collection, where the first argument to CreateInspectorObject is the
			// cmObject. Keep this code in sync with BaseGetInspectorObjects.
			foreach (int hvoItem in collection)
			{
				var hvoNum = int.Parse(hvoItem.ToString());
				var objItem = m_cache.ServiceLocator.GetObject(hvoNum);
				var io = CreateInspectorObject(objItem, obj, ioParent, level);
				io.DisplayName = $"[{n++}]";
				list.Add(io);
			}
			return list;
		}

		/// <summary>
		/// Gets a list of IInspectorObject objects representing all the properties for the
		/// specified object, which is assumed to be at the specified level.
		/// </summary>
		protected virtual List<IInspectorObject> BaseGetInspectorObjects(object obj, int level)
		{
			var ioParent = obj as IInspectorObject;
			if (ioParent != null)
			{
				obj = ioParent.Object;
			}

			var list = new List<IInspectorObject>();
			if (obj is ICollection collection)
			{
				var i = 0;
				foreach (var item in collection)
				{
					var io = CreateInspectorObject(item, obj, ioParent, level);
					io.DisplayName = $"[{i++}]";
					list.Add(io);
				}
				return list;
			}

			foreach (var pi in GetPropsForObj(obj))
			{
				try
				{
					var propObj = pi.GetValue(obj, null);
					var io1 = CreateInspectorObject(pi, propObj, obj, ioParent, level);
					if (io1.DisplayType == "System.DateTime")
					{
						io1.HasChildren = false;
					}
					list.Add(io1);
				}
				catch (Exception e)
				{
					list.Add(CreateExceptionInspectorObject(e, obj, pi.Name, level, ioParent));
				}
			}

			if (LCMBrowserForm.CFields != null && LCMBrowserForm.CFields.Any() && obj != null)
			{
				list.AddRange(LCMBrowserForm.CFields.Where(cf2 => obj.ToString().Contains(m_mdc.GetClassName(cf2.ClassID))).Select(cf2 => CreateCustomInspectorObject(obj, ioParent, level, cf2)));
			}

			list.Sort(CompareInspectorObjectNames);
			return list;
		}

		/// <summary>
		/// Gets the properties specified in the meta data cache for the specified object .
		/// </summary>
		protected override PropertyInfo[] GetPropsForObj(object obj)
		{
			if (m_mdc != null && obj is ICmObject cmObject && UseMetaDataCache)
			{
				return GetFieldsFromMetaDataCache(cmObject);
			}
			var propArray = base.GetPropsForObj(obj);
			var cmObj = obj as ICmObject;
			var props = new List<PropertyInfo>(propArray);
			if (m_mdc == null || cmObj == null)
			{
				return propArray;
			}

			RevisePropsList(cmObj, ref props);
			return props.ToArray();
		}

		/// <summary>
		/// Gets the fields from meta data cache.
		/// </summary>
		private PropertyInfo[] GetFieldsFromMetaDataCache(ICmObject cmObj)
		{
			if (cmObj == null)
			{
				return base.GetPropsForObj(cmObj);
			}
			var props = new List<PropertyInfo>();
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty;
			// Get only the fields for the object that are specified in the meta data cache.
			foreach (var flid in m_mdc.GetFields(cmObj.ClassID, true, (int)CellarPropertyTypeFilter.All))
			{
				var fieldName = m_mdc.GetFieldName(flid);
				var fieldType = (CellarPropertyType)m_mdc.GetFieldType(flid);
				if (m_fldSuffix.TryGetValue(fieldType, out var suffix))
				{
					fieldName += suffix;
				}

				var pi = cmObj.GetType().GetProperty(fieldName, flags);
				if (pi != null)
				{
					props.Add(pi);
				}
			}

			return props.Count > 0 ? props.ToArray() : base.GetPropsForObj(cmObj);
		}

		/// <summary>
		/// Create InspectorObjects for the custom fields for the current object.
		/// </summary>
		private IInspectorObject CreateCustomInspectorObject(object obj, IInspectorObject parentIo, int level, CustomFields cf)
		{
			var to = obj as ICmObject;
			var managedSilDataAccess = m_cache.GetManagedSilDataAccess();
			var iValue = string.Empty;
			var fieldId = cf.FieldID;
			IInspectorObject io = null;
			if (obj != null)
			{
				switch (cf.Type)
				{
					case "ITsString":
						var oValue = m_cache.DomainDataByFlid.get_StringProp(to.Hvo, fieldId);
						io = base.CreateInspectorObject(null, oValue, obj, parentIo, level);
						iValue = oValue.Text;
						io.HasChildren = false;
						io.DisplayName = cf.Name;
						break;
					case "System.Int32":
						var sValue = m_cache.DomainDataByFlid.get_IntProp(to.Hvo, fieldId);
						io = base.CreateInspectorObject(null, sValue, obj, parentIo, level);
						iValue = sValue.ToString();
						io.HasChildren = false;
						io.DisplayName = cf.Name;
						break;
					case "SIL.FieldWorks.Common.FwUtils.GenDate":
						// tried get_TimeProp, get_UnknowbProp, get_Prop
						var genObj = managedSilDataAccess.get_GenDateProp(to.Hvo, fieldId);
						io = base.CreateInspectorObject(null, genObj, obj, parentIo, level);
						iValue = genObj.ToString();
						io.HasChildren = true;
						io.DisplayName = cf.Name;
						break;
					case "LcmReferenceCollection<ICmPossibility>":  // ReferenceCollection
						var count = m_cache.DomainDataByFlid.get_VecSize(to.Hvo, fieldId);
						iValue = $"Count = {count}";
						var objects = m_cache.GetManagedSilDataAccess().VecProp(to.Hvo, fieldId);
						objects.Initialize();
						io = base.CreateInspectorObject(null, objects, obj, parentIo, level);
						io.HasChildren = count > 0;
						io.DisplayName = $"{cf.Name}RC";
						break;
					case "ICmPossibility":  // ReferenceAtomic
						var rValue = m_cache.DomainDataByFlid.get_ObjectProp(to.Hvo, fieldId);
						var posObj = (rValue == 0 ? null : (ICmPossibility)m_cache.ServiceLocator.GetObject(rValue));
						io = base.CreateInspectorObject(null, posObj, obj, parentIo, level);
						iValue = (posObj == null ? "null" : posObj.NameHierarchyString);
						io.HasChildren = posObj != null;
						io.DisplayName = $"{cf.Name}RA";
						break;
					case "IStText": //    multi-paragraph text (OA) StText)
						var mValue = m_cache.DomainDataByFlid.get_ObjectProp(to.Hvo, fieldId);
						var paraObj = (mValue == 0 ? null : (IStText)m_cache.ServiceLocator.GetObject(mValue));
						io = base.CreateInspectorObject(null, paraObj, obj, parentIo, level);
						iValue = (paraObj == null ? "null" : "StText: " + paraObj.Hvo.ToString());
						io.HasChildren = mValue > 0;
						io.DisplayName = $"{cf.Name}OA";
						break;
					default:
						MessageBox.Show($@"The type of the custom field is {cf.Type}");
						break;
				}
			}

			io.DisplayType = cf.Type;
			io.DisplayValue = iValue ?? "null";
			io.Flid = cf.FieldID;

			return io;
		}

		/// <summary>
		/// Removes properties from the specified list of properties, those properties the
		/// user has specified he doesn't want to see in the browser.
		/// </summary>
		private void RevisePropsList(ICmObject cmObj, ref List<PropertyInfo> props)
		{
			if (cmObj == null)
			{
				return;
			}
			for (var i = props.Count - 1; i >= 0; i--)
			{
				if (props[i].Name == "Guid")
				{
					continue;
				}
				if (!LCMClassList.IsPropertyDisplayed(cmObj, props[i].Name))
				{
					props.RemoveAt(i);
					continue;
				}
				var work = LCMBrowserForm.StripOffTypeChars(props[i].Name);
				var flid = 0;
				if (m_mdc.FieldExists(cmObj.ClassID, work, true))
				{
					flid = m_mdc.GetFieldId2(cmObj.ClassID, work, true);
				}
				else
				{
					if (LCMBrowserForm.m_virtualFlag == false)
					{
						props.RemoveAt(i);
						continue;
					}
				}
				switch (LCMBrowserForm.m_virtualFlag)
				{
					case false when flid >= 20000000 && flid < 30000000:
						props.RemoveAt(i);
						continue;
					case false when m_mdc.get_IsVirtual(flid):
						props.RemoveAt(i);
						break;
				}
			}
		}

		/// <summary>
		/// Gets an inspector object for the specified property info., checking for various
		/// LCM interface types.
		/// </summary>
		protected override IInspectorObject CreateInspectorObject(PropertyInfo pi, object obj, object owningObj, IInspectorObject ioParent, int level)
		{
			var io = base.CreateInspectorObject(pi, obj, owningObj, ioParent, level);
			if (pi == null && io != null)
			{
				io.DisplayType = StripOffLCMNamespace(io.DisplayType);
			}
			else if (pi != null && io == null)
			{
				io.DisplayType = pi.PropertyType.Name;
			}
			else if (pi != null)
			{
				io.DisplayType = (io.DisplayType == "System.__ComObject" ?
				pi.PropertyType.Name : StripOffLCMNamespace(io.DisplayType));
			}
			switch (obj)
			{
				case null:
					return io;
				case char c:
					io.DisplayValue = $"'{io.DisplayValue}'   (U+{(int)c:X4})";
					return io;
				case ILcmVector _:
				{
					var mi = obj.GetType().GetMethod("ToArray");
					try
					{
						var array = mi.Invoke(obj, null) as ICmObject[];
						io.Object = array;
						io.DisplayValue = FormatCountString(array.Length);
						io.HasChildren = (array.Length > 0);
					}
					catch (Exception e)
					{
						io = CreateExceptionInspectorObject(e, obj, pi.Name, level, ioParent);
					}

					break;
				}
				case ICollection<ICmObject> collection:
				{
					var array = collection.ToArray();
					io.Object = array;
					io.DisplayValue = FormatCountString(array.Length);
					io.HasChildren = array.Length > 0;
					break;
				}
			}

			const string fmtAppend = "{0}, {{{1}}}";
			const string fmtReplace = "{0}";
			const string fmtStrReplace = "\"{0}\"";

			switch (obj)
			{
				case ICmFilter cmFilter:
				{
					io.DisplayValue = string.Format(fmtAppend, io.DisplayValue, cmFilter.Name);
					break;
				}
				case IMultiAccessorBase accessorBase:
				{
					io.DisplayValue = string.Format(fmtReplace, accessorBase.AnalysisDefaultWritingSystem.Text);
					break;
				}
				case ITsString tsString:
				{
					io.DisplayValue = string.Format(fmtStrReplace, tsString.Text);
					io.HasChildren = true;
					break;
				}
				case ITsTextProps tsTextProps:
					io.Object = new TextProps(tsTextProps, m_cache);
					io.DisplayValue = string.Empty;
					io.HasChildren = true;
					break;
				case IPhNCSegments phNcSegments:
				{
					io.DisplayValue = string.Format(fmtAppend, io.DisplayValue, phNcSegments.Name.AnalysisDefaultWritingSystem.Text);
					break;
				}
				case IPhEnvironment environment:
				{
					io.DisplayValue = $"{io.DisplayValue}, {{Name: {environment.Name.AnalysisDefaultWritingSystem.Text}, Pattern: {environment.StringRepresentation.Text}}}";
					break;
				}
				case IMoEndoCompound endoCompound:
				{
					io.DisplayValue = string.Format(fmtAppend, io.DisplayValue, endoCompound.Name.AnalysisDefaultWritingSystem.Text);
					break;
				}
				default:
				{
					if (obj.GetType().GetInterface("IRepository`1") != null)
					{
						io.DisplayName = io.DisplayType;
					}
					break;
				}
			}

			return io;
		}

		#endregion

		/// <summary />
		private string StripOffLCMNamespace(string type)
		{
			if (string.IsNullOrEmpty(type))
			{
				return string.Empty;
			}
			if (!type.StartsWith("SIL.LCModel"))
			{
				return type;
			}
			type = type.Replace("SIL.LCModel.Infrastructure.Impl.", string.Empty);
			type = type.Replace("SIL.LCModel.Infrastructure.", string.Empty);
			type = type.Replace("SIL.LCModel.DomainImpl.", string.Empty);
			type = type.Replace("SIL.LCModel.", string.Empty);

			return CleanupGenericListType(type);
		}

		/// <summary>
		/// Gets a list of all the instances in the specified repository.
		/// </summary>
		private List<object> GetRepositoryInstances(object repository)
		{
			var list = new List<object>();

			try
			{
				// Get an object that represents all the repository's collection of instances
				var repoInstances = (repository.GetType().GetMethods().Where(mi => mi.Name == "AllInstances").Select(mi => mi.Invoke(repository, null))).FirstOrDefault();
				if (repoInstances == null)
				{
					throw new MissingMethodException($"Repository {repository.GetType().Name} is missing 'AllInstances' method.");
				}
				if (!(repoInstances is IEnumerable ienum))
				{
					throw new NullReferenceException($"Repository {repository.GetType().Name} is not an IEnumerable");
				}
				var enumerator = ienum.GetEnumerator();
				while (enumerator.MoveNext())
				{
					list.Add(enumerator.Current);
				}
			}
			catch (Exception e)
			{
				list.Add(e);
			}

			return list;
		}

		/// <summary>
		/// Finds the item in the list having the specified hvo.
		/// </summary>
		public int GotoGuid(Guid guid)
		{
			if (!m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().TryGetObject(guid, out var obj))
			{
				return -1;
			}
			var ownerTree = new List<ICmObject>
			{
				obj
			};
			while (obj.Owner != null)
			{
				obj = obj.Owner;
				ownerTree.Add(obj);
			}
			var index = -1;
			for (var i = ownerTree.Count - 2; i >= 0; i--)
			{
				index = ExpandObject(ownerTree[i]);
			}

			return index;
		}

		/// <summary>
		/// Expands the row corresponding to the specified CmObject.
		/// </summary>
		private int ExpandObject(ICmObject obj)
		{
			for (var i = 0; i < Count; i++)
			{
				var rowObj = this[i].OriginalObject;
				if (rowObj == obj)
				{
					if (!IsExpanded(i))
					{
						base.ExpandObject(i);
					}

					return i;
				}

				if (!(rowObj is ILcmVector))
				{
					continue;
				}
				var index = FindObjInVector(obj, rowObj as ILcmVector);
				if (index < 0)
				{
					continue;
				}

				if (!IsExpanded(i))
				{
					base.ExpandObject(i);
				}

				index += (i + 1);
				if (!IsExpanded(index))
				{
					base.ExpandObject(index);
				}

				return index;
			}

			return -1;
		}

		/// <summary>
		/// Finds the index of the specified CmObject's guid in the specified ILcmVector.
		/// </summary>
		private int FindObjInVector(ICmObject obj, ILcmVector vect)
		{
			var guids = vect.ToGuidArray();
			for (var i = 0; i < guids.Length; i++)
			{
				if (obj.Guid == guids[i])
				{
					return i;
				}
			}

			return -1;
		}

		/// <summary>
		/// Returns the object number only (as a string).
		/// </summary>
		private string GetObjectOnly(string objectName)
		{
			var idx = objectName.IndexOf(":");
			return idx <= 0 ? string.Empty : objectName.Substring(idx + 1);
		}
	}
}