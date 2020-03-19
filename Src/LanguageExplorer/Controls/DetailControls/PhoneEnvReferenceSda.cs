// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;
using SIL.LCModel.Application;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Infrastructure;

namespace LanguageExplorer.Controls.DetailControls
{
	internal class PhoneEnvReferenceSda : DomainDataByFlidDecoratorBase
	{
		private Dictionary<int, List<int>> m_MainObjEnvs = new Dictionary<int, List<int>>();
		private Dictionary<int, ITsString> m_EnvStringReps = new Dictionary<int, ITsString>();
		private Dictionary<int, string> m_ErrorMsgs = new Dictionary<int, string>();
		private int m_NextDummyId = -500000;

		public PhoneEnvReferenceSda(ISilDataAccessManaged domainDataByFlid)
			: base(domainDataByFlid)
		{
			SetOverrideMdc(new PhoneEnvReferenceDataCacheDecorator(MetaDataCache as IFwMetaDataCacheManaged));
		}

		public override int get_VecSize(int hvo, int tag)
		{
			return tag == PhoneEnvReferenceView.kMainObjEnvironments ? m_MainObjEnvs.TryGetValue(hvo, out var objs) ? objs.Count : 0 : base.get_VecSize(hvo, tag);
		}

		public override int get_VecItem(int hvo, int tag, int index)
		{
			return tag == PhoneEnvReferenceView.kMainObjEnvironments ? m_MainObjEnvs.TryGetValue(hvo, out var objs) ? objs[index] : 0 : base.get_VecItem(hvo, tag, index);
		}

		public override void DeleteObj(int hvoObj)
		{
			if (hvoObj < 0)
			{
				if (m_MainObjEnvs.ContainsKey(hvoObj))
				{
					m_MainObjEnvs.Remove(hvoObj);
				}
				if (m_EnvStringReps.ContainsKey(hvoObj))
				{
					m_EnvStringReps.Remove(hvoObj);
				}
				if (m_ErrorMsgs.ContainsKey(hvoObj))
				{
					m_ErrorMsgs.Remove(hvoObj);
				}
				foreach (var x in m_MainObjEnvs.Where(x => x.Value.Contains(hvoObj)))
				{
					x.Value.Remove(hvoObj);
				}
			}
			else
			{
				base.DeleteObj(hvoObj);
			}
		}

		public override ITsString get_StringProp(int hvo, int tag)
		{
			return tag == PhoneEnvReferenceView.kEnvStringRep ? m_EnvStringReps.TryGetValue(hvo, out var tss) ? tss : null : base.get_StringProp(hvo, tag);
		}

		public override void SetString(int hvo, int tag, ITsString _tss)
		{
			if (tag == PhoneEnvReferenceView.kEnvStringRep)
			{
				m_EnvStringReps[hvo] = _tss;
			}
			else
			{
				base.SetString(hvo, tag, _tss);
			}
		}

		public override string get_UnicodeProp(int hvo, int tag)
		{
			return tag == PhoneEnvReferenceView.kErrorMessage ? m_ErrorMsgs.TryGetValue(hvo, out var sMsg) ? sMsg : null : base.get_UnicodeProp(hvo, tag);
		}

		public override void SetUnicode(int hvo, int tag, string _rgch, int cch)
		{
			if (tag == PhoneEnvReferenceView.kErrorMessage)
			{
				m_ErrorMsgs[hvo] = _rgch;
			}
			else
			{
				base.SetUnicode(hvo, tag, _rgch, cch);
			}
		}

		public override int MakeNewObject(int clid, int hvoOwner, int tag, int ord)
		{
			if (tag == PhoneEnvReferenceView.kMainObjEnvironments)
			{
				var hvo = --m_NextDummyId;
				if (!m_MainObjEnvs.TryGetValue(hvoOwner, out var objs))
				{
					objs = new List<int>();
					m_MainObjEnvs.Add(hvoOwner, objs);
				}
				objs.Insert(ord, hvo);
				return hvo;
			}
			return base.MakeNewObject(clid, hvoOwner, tag, ord);
		}

		#region extra methods borrowed from IVwCacheDa

		public void CacheReplace(int hvoObj, int tag, int ihvoMin, int ihvoLim, int[] _rghvo, int chvo)
		{
			if (tag != PhoneEnvReferenceView.kMainObjEnvironments)
			{
				return;
			}
			if (m_MainObjEnvs.TryGetValue(hvoObj, out var objs))
			{
				var cDel = ihvoLim - ihvoMin;
				if (cDel > 0)
				{
					objs.RemoveRange(ihvoMin, cDel);
				}
				objs.InsertRange(ihvoMin, _rghvo);
			}
			else
			{
				objs = new List<int>();
				objs.AddRange(_rghvo);
				m_MainObjEnvs.Add(hvoObj, objs);
			}
		}

		public void CacheVecProp(int hvoObj, int tag, int[] rghvo, int chvo)
		{
			if (tag != PhoneEnvReferenceView.kMainObjEnvironments)
			{
				return;
			}
			if (m_MainObjEnvs.TryGetValue(hvoObj, out var objs))
			{
				objs.Clear();
			}
			else
			{
				objs = new List<int>();
				m_MainObjEnvs.Add(hvoObj, objs);
			}
			objs.AddRange(rghvo);
		}

		#endregion
	}
}