// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Globalization;
using System.Xml.Linq;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.Xml;

namespace LanguageExplorer.Filters
{
	internal sealed class DateTimeMatcher : BaseMatcher
	{
		/// <summary>
		/// For use with IPersistAsXml
		/// </summary>
		internal DateTimeMatcher(XElement element)
			: base(element)
		{
			Start = DateTime.Parse(XmlUtils.GetMandatoryAttributeValue(element, "start"), DateTimeFormatInfo.InvariantInfo);
			End = DateTime.Parse(XmlUtils.GetMandatoryAttributeValue(element, "end"), DateTimeFormatInfo.InvariantInfo);
			MatchType = (DateMatchType)XmlUtils.GetMandatoryIntegerAttributeValue(element, "type");
			HandleGenDate = XmlUtils.GetOptionalBooleanAttributeValue(element, "genDate", false);
			IsStartAD = XmlUtils.GetOptionalBooleanAttributeValue(element, "startAD", true);
			IsEndAD = XmlUtils.GetOptionalBooleanAttributeValue(element, "endAD", true);
			UnspecificMatching = XmlUtils.GetOptionalBooleanAttributeValue(element, "unspecific", false);
		}

		internal DateTimeMatcher(DateTime start, DateTime end, DateMatchType type)
		{
			Start = start;
			End = end;
			MatchType = type;
			IsStartAD = true;
			IsEndAD = true;
			UnspecificMatching = false;
		}

		/// <summary>
		/// The start time (used for start of range and not range, on or after)
		/// </summary>
		internal DateTime Start { get; }

		internal DateMatchType MatchType { get; }

		/// <summary>
		/// The end time (used for end of range and not range, on or before)
		/// </summary>
		internal DateTime End { get; }

		/// <summary>
		/// Flag whether we are matching GenDate objects instead of DateTime objects.
		/// </summary>
		internal bool HandleGenDate { get; set; }

		// The next three properties are also used with GenDate comparisons.
		internal bool IsStartAD { get; set; }
		internal bool IsEndAD { get; set; }
		internal bool UnspecificMatching { get; set; }

		public override bool Matches(ITsString arg)
		{
			var text = arg.Text;
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			if (!HandleGenDate && DateTime.TryParse(text, out var time))
			{
				switch (MatchType)
				{
					case DateMatchType.After:
						return time >= Start;
					case DateMatchType.Before:
						return time <= End;
					case DateMatchType.Range:
					case DateMatchType.On:
						return time >= Start && time <= End;
					case DateMatchType.NotRange:
						return time < Start || time > End;
				}
			}
			else if (HandleGenDate && GenDate.TryParse(text, out var gen))
			{
				switch (MatchType)
				{
					case DateMatchType.After:
						return GenDateIsAfterDate(gen, Start, IsStartAD);
					case DateMatchType.Before:
						return GenDateIsBeforeDate(gen, End, IsEndAD);
					case DateMatchType.Range:
					case DateMatchType.On:
						return GenDateIsInRange(gen);
					case DateMatchType.NotRange:
						return !GenDateIsInRange(gen);
				}
			}
			return false;
		}

		private bool GenDateIsBeforeDate(GenDate gen, DateTime date, bool fAD)
		{
			if (UnspecificMatching)
			{
				return GenDateMightBeBeforeDate(gen, date, fAD);
			}
			if (gen.IsAD && !fAD) // AD > BC
			{
				return false;
			}
			if (!gen.IsAD && fAD) // BC < AD
			{
				return gen.Precision != GenDate.PrecisionType.After;
			}
			if (!gen.IsAD && !fAD)      // Both BC
			{
				if (gen.Year > date.Year)
				{
					return gen.Precision != GenDate.PrecisionType.After;
				}
				if (gen.Year < date.Year)
				{
					return false;
				}
			}
			if (gen.IsAD && fAD)        // Both AD
			{
				if (gen.Year < date.Year)
				{
					return gen.Precision != GenDate.PrecisionType.After;
				}
				if (gen.Year > date.Year)
				{
					return false;
				}
			}
			if (gen.Month < date.Month)
			{
				return gen.Month != GenDate.UnknownMonth || gen.Precision == GenDate.PrecisionType.Before;
			}
			if (gen.Month > date.Month)
			{
				return false;
			}
			if (gen.Day == GenDate.UnknownDay)
			{
				return gen.Precision == GenDate.PrecisionType.Before;
			}
			return gen.Day <= date.Day && gen.Precision != GenDate.PrecisionType.After;
		}

		private static bool GenDateMightBeBeforeDate(GenDate gen, DateTime date, bool fAD)
		{
			if (gen.IsAD && !fAD) // AD > BC
			{
				return gen.Precision == GenDate.PrecisionType.Before;
			}
			if (!gen.IsAD && fAD) // BC < AD
			{
				return true;
			}
			if (!gen.IsAD && !fAD)      // Both BC
			{
				if (gen.Year > date.Year)
				{
					return true;
				}
				if (gen.Year < date.Year)
				{
					return gen.Precision == GenDate.PrecisionType.Before;
				}
			}
			if (gen.IsAD && fAD)        // Both AD
			{
				if (gen.Year < date.Year)
				{
					return true;
				}
				if (gen.Year > date.Year)
				{
					return gen.Precision == GenDate.PrecisionType.Before;
				}
			}
			if (gen.Month < date.Month)
			{
				return gen.Month != GenDate.UnknownMonth || gen.Precision != GenDate.PrecisionType.After;
			}
			if (gen.Month > date.Month)
			{
				return gen.Precision == GenDate.PrecisionType.Before;
			}
			return gen.Day <= date.Day || gen.Precision == GenDate.PrecisionType.Before;
		}

		private bool GenDateIsAfterDate(GenDate gen, DateTime date, bool fAD)
		{
			if (UnspecificMatching)
			{
				return GenDateMightBeAfterDate(gen, date, fAD);
			}
			if (gen.IsAD && !fAD) // AD > BC
			{
				return gen.Precision != GenDate.PrecisionType.Before;
			}
			if (!gen.IsAD && fAD) // BC < AD
			{
				return false;
			}
			if (!gen.IsAD && !fAD)      // Both BC
			{
				if (gen.Year > date.Year)
				{
					return false;
				}
				if (gen.Year < date.Year)
				{
					return gen.Precision != GenDate.PrecisionType.Before;
				}
			}
			if (gen.IsAD && fAD)        // Both AD
			{
				if (gen.Year < date.Year)
				{
					return false;
				}
				if (gen.Year > date.Year)
				{
					return gen.Precision != GenDate.PrecisionType.Before;
				}
			}
			if (gen.Month < date.Month)
			{
				return gen.Month == GenDate.UnknownMonth && gen.Precision == GenDate.PrecisionType.After;
			}
			if (gen.Month > date.Month)
			{
				return gen.Precision != GenDate.PrecisionType.Before;
			}
			if (gen.Day == GenDate.UnknownDay)
			{
				return gen.Precision == GenDate.PrecisionType.After;
			}
			return gen.Day >= date.Day && gen.Precision != GenDate.PrecisionType.Before;
		}

		private static bool GenDateMightBeAfterDate(GenDate gen, DateTime date, bool fAD)
		{
			if (gen.IsAD && !fAD) // AD > BC
			{
				return true;
			}
			if (!gen.IsAD && fAD) // BC < AD
			{
				return gen.Precision == GenDate.PrecisionType.After;
			}
			if (!gen.IsAD && !fAD)      // Both BC
			{
				if (gen.Year > date.Year)
				{
					return gen.Precision == GenDate.PrecisionType.After;
				}
				if (gen.Year < date.Year)
				{
					return true;
				}
			}
			if (gen.IsAD && fAD)        // Both AD
			{
				if (gen.Year < date.Year)
				{
					return gen.Precision == GenDate.PrecisionType.After;
				}
				if (gen.Year > date.Year)
				{
					return true;
				}
			}
			if (gen.Month < date.Month)
			{
				return gen.Month == GenDate.UnknownMonth && gen.Precision != GenDate.PrecisionType.Before;
			}
			if (gen.Month > date.Month)
			{
				return true;
			}
			if (gen.Day == GenDate.UnknownDay)
			{
				return gen.Precision != GenDate.PrecisionType.Before;
			}
			return gen.Day == date.Day ? gen.Precision != GenDate.PrecisionType.Before : gen.Day > date.Day || gen.Precision == GenDate.PrecisionType.After;
		}

		private bool GenDateIsInRange(GenDate gen)
		{
			return GenDateIsAfterDate(gen, Start, IsStartAD) && GenDateIsBeforeDate(gen, End, IsEndAD);
		}

		public override bool SameMatcher(IMatcher other)
		{
			if (!(other is DateTimeMatcher dateTimeMatcher))
			{
				return false;
			}
			return End == dateTimeMatcher.End && Start == dateTimeMatcher.Start && MatchType == dateTimeMatcher.MatchType && HandleGenDate == dateTimeMatcher.HandleGenDate
			       && (!HandleGenDate || IsStartAD == dateTimeMatcher.IsStartAD && IsEndAD == dateTimeMatcher.IsEndAD && UnspecificMatching == dateTimeMatcher.UnspecificMatching);
		}

		public override void PersistAsXml(XElement element)
		{
			base.PersistAsXml(element);
			XmlUtils.SetAttribute(element, "start", Start.ToString("s", DateTimeFormatInfo.InvariantInfo));
			XmlUtils.SetAttribute(element, "end", End.ToString("s", DateTimeFormatInfo.InvariantInfo));
			XmlUtils.SetAttribute(element, "type", ((int)MatchType).ToString());
			XmlUtils.SetAttribute(element, "genDate", HandleGenDate.ToString());
			if (HandleGenDate)
			{
				XmlUtils.SetAttribute(element, "startAD", IsStartAD.ToString());
				XmlUtils.SetAttribute(element, "endAD", IsEndAD.ToString());
				XmlUtils.SetAttribute(element, "unspecific", UnspecificMatching.ToString());
			}
		}
	}
}