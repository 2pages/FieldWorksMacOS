// Copyright (c) 2007-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Icu;
using SIL.FieldWorks.Common.FwUtils;
using SIL.PlatformUtilities;

namespace LanguageExplorer.Impls
{
	/// <summary >
	/// This implements a control suitable for placing on a tab of the Tools/Options
	/// dialog. The control is a combobox that lists the languages (writing systems) into
	/// which the program has been (at least partially) localized. Each language name is given in
	/// its own language and script.
	/// </summary>
	public partial class UserInterfaceChooser : ComboBox
	{
		private string m_sUserWs;
		private string m_sNewUserWs;
		// For efficiency, cache the brush to use for drawing.
		private SolidBrush m_foreColorBrush;

		/// <summary />
		public UserInterfaceChooser()
		{
			InitializeComponent();
			if (Platform.IsMono)
			{
				// On Windows, finding fonts for strings appears to work fine.  On Linux, fonts
				// are found based purely on the current locale setting.  So displaying Chinese
				// text when the locale is set to Hindi just doesn't work.  Thus, to get our
				// fancy display of language choices to work on Linux, we have to draw the list
				// ourselves.  (See FWNX-1069.)
				DrawMode = DrawMode.OwnerDrawVariable;
			}
		}

		/// <summary>
		/// Initializes the control, setting the specified user interface writing
		/// system as the current one.
		/// </summary>
		public void Init(string sUserWs)
		{
			m_sUserWs = sUserWs;
			m_sNewUserWs = sUserWs;
			PopulateLanguagesCombo();
		}

		/// <summary>
		/// Gets the new user writing system.
		/// </summary>
		public string NewUserWs => (m_sNewUserWs == "en-US" ? "en" : m_sNewUserWs);

		/// <summary>
		/// Populate the User Interface Languages combobox with the available languages.
		/// </summary>
		private void PopulateLanguagesCombo()
		{
			SuspendLayout();
			// First, find those languages having satellite resource DLLs.
			AddAvailableLangsFromSatelliteDlls();
			// If no English locale was added, then add generic English. Otherwise,
			// if another, non US version of English, was added (e.g. en_GB), then add
			// US English since that is the locale of the fallback resources. This
			// will prevent a region of English existing in the list at the same time
			// with generic English. In other words, generic English and US English
			// are considered to be identical, but they show up in the list
			// differently depending upon whether or not another region of English
			// is also in the list.
			var genericEnglishAdded = IndexInList("en", false) >= 0;
			var englishRegionAdded = IndexInList("en", true) >= 0;
			if (!genericEnglishAdded)
			{
				if (!englishRegionAdded)
				{
					AddLanguage("en");
				}
				else if (IndexInList("en-US", false) < 0)
				{
					AddLanguage("en-US");
				}
			}
			var userWsIsEnglish = m_sUserWs.StartsWith("en");
			// Add the user's UI writing system if it's not an English one,
			// since English writing systems have already been added.
			if (!userWsIsEnglish && m_sUserWs != string.Empty)
			{
				AddLanguage(m_sUserWs);
			}
			var index = IndexInList(m_sUserWs, false);
			if (index >= 0)
			{
				SelectedIndex = index;
			}
			else if (userWsIsEnglish)
			{
				index = IndexInList("en-US", true);
				if (index >= 0)
				{
					SelectedIndex = index;
				}
			}
			ResumeLayout();
		}

		/// <summary>
		/// Adds the available languages from satellite resource DLLs found off the
		/// application's folder.
		/// </summary>
		private void AddAvailableLangsFromSatelliteDlls()
		{
			// Get the folder in which the program file is stored.
			var sDllLocation = Path.GetDirectoryName(Application.ExecutablePath);
			// Get all the sub-folder in the program file's folder.
			var rgsDirs = Directory.GetDirectories(sDllLocation);
			// Go through each sub-folder and if at least one file in a sub-folder ends
			// with ".resource.dll", we know the folder stores localized resources and the
			// name of the folder is the culture ID for which the resources apply. The
			// name of the folder is stripped from the path and used to add a language
			// to the list.
			foreach (var dir in rgsDirs.Where(dir => Directory.GetFiles(dir, "*.resources.dll").Length > 0))
			{
				AddLanguage(Path.GetFileName(dir));
			}
		}

		/// <summary>
		/// Adds the language.
		/// </summary>
		private void AddLanguage(string sLocale)
		{
			Debug.Assert(!string.IsNullOrEmpty(sLocale));
			if (IndexInList(sLocale, false) < 0)
			{
				var ldi = new LanguageDisplayItem(sLocale);
				if (ldi.Name.Length == 0)
				{
					ldi.Name = ldi.Locale;  // TODO: find a better fallback.
				}
				Items.Add(ldi);
			}
		}

		/// <summary>
		/// Determines whether the specified locale is in the chooser's list.
		/// </summary>
		/// <param name="locale">locale (i.e. culture) to look for.</param>
		/// <param name="allowMatchOnParentCulture">True to allow matches on a locale's parent
		/// culture (i.e. "en-US" = "en". Otherwise, don't match on just the parent
		/// culture.</param>
		private int IndexInList(string locale, bool allowMatchOnParentCulture)
		{
			for (var i = 0; i < Items.Count; i++)
			{
				var ldi = Items[i] as LanguageDisplayItem;
				if (ldi != null)
				{
					if (ldi.Locale == locale)
					{
						return i;
					}
					if (allowMatchOnParentCulture && ldi.Locale.StartsWith(locale))
					{
						return i;
					}
				}
			}
			return -1;
		}

		/// <summary>
		/// Handles the SelectedIndexChanged event
		/// </summary>
		protected override void OnSelectedIndexChanged(EventArgs e)
		{
			base.OnSelectedIndexChanged(e);
			var ldi = (LanguageDisplayItem)SelectedItem;
			m_sNewUserWs = ldi.Locale;
		}

		/// <summary>
		/// Handles the measure item event.
		/// </summary>
		protected override void OnMeasureItem(MeasureItemEventArgs e)
		{
			base.OnMeasureItem(e);
			if (!Platform.IsMono)
			{
				return;
			}
			// See the comment above about FWNX-1069.
			var lang = ((LanguageDisplayItem)Items[e.Index]).Locale;
			using (var font = GetFontForLanguage(lang))
			{
				var name = ((LanguageDisplayItem)Items[e.Index]).Name;
				var stringSize = e.Graphics.MeasureString(name, font);
				// Set the height and width of the item
				e.ItemHeight = (int)stringSize.Height;
				e.ItemWidth = (int)stringSize.Width;
			}
		}

		/// <summary>
		/// Handles the draw item event.
		/// </summary>
		protected override void OnDrawItem(DrawItemEventArgs e)
		{
			base.OnDrawItem(e);
			Brush brush;
			// Create the brush using the ForeColor specified by the DrawItemEventArgs
			if (m_foreColorBrush == null)
			{
				m_foreColorBrush = new SolidBrush(e.ForeColor);
			}
			else if (m_foreColorBrush.Color != e.ForeColor)
			{
				// The control's ForeColor has changed, so dispose of the cached brush and
				// create a new one.
				m_foreColorBrush.Dispose();
				m_foreColorBrush = new SolidBrush(e.ForeColor);
			}
			// Select the appropriate brush depending on if the item is selected.
			// Since State can be a combination (bit-flag) of enum values, you can't use
			// "==" to compare them.
			brush = (e.State & DrawItemState.Selected) == DrawItemState.Selected ? SystemBrushes.HighlightText : m_foreColorBrush;
			// Perform the painting.
			var lang = ((LanguageDisplayItem)Items[e.Index]).Locale;
			using (var font = GetFontForLanguage(lang))
			{
				var name = ((LanguageDisplayItem)Items[e.Index]).Name;
				e.DrawBackground();
				e.Graphics.DrawString(name, font, brush, e.Bounds);
			}
		}

		/// <summary>
		/// Gets the font for the language tag.  This is the essential part of the owner draw.
		/// </summary>
		private static Font GetFontForLanguage(string lang)
		{
			// For some reason, Mono requires both FwUtils in the next line.
			var fontName = FwUtils.GetFontNameForLanguage(lang);
			return string.IsNullOrEmpty(fontName) ? new Font(FontFamily.GenericSansSerif, 8.25F) : new Font(fontName, 8.25F);
		}

		/// <summary />
		private sealed class LanguageDisplayItem
		{
			/// <summary />
			public LanguageDisplayItem(string sLocale)
			{
				Locale = sLocale;
				Name = DisplayName(sLocale);
			}

			/// <summary>
			/// Displays the name.
			/// </summary>
			private static string DisplayName(string sLocale)
			{
				string sName;
				try
				{
					var ci = new CultureInfo(sLocale);
					sName = ci.NativeName;
				}
				catch
				{
					sName = new Locale(sLocale).GetDisplayName(sLocale);
				}
				return sName;
			}

			/// <summary>
			/// Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
			/// </summary>
			/// <returns>
			/// A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
			/// </returns>
			public override string ToString()
			{
				return Name;
			}

			/// <summary>
			/// Determines whether the specified <see cref="T:System.Object"></see> is equal to the current <see cref="T:System.Object"></see>.
			/// </summary>
			/// <param name="obj">The <see cref="T:System.Object"></see> to compare with the current <see cref="T:System.Object"></see>.</param>
			/// <returns>
			/// true if the specified <see cref="T:System.Object"></see> is equal to the current <see cref="T:System.Object"></see>; otherwise, false.
			/// </returns>
			public override bool Equals(object obj)
			{
				var ldi = obj as LanguageDisplayItem;
				if (ldi != null)
				{
					return (ldi.Locale == Locale && ldi.Name == Name);
				}

				return false;
			}

			/// <summary>
			/// Otherwise useless override, which is needed, because of the 'Equals" override.
			/// </summary>
			/// <returns></returns>
			public override int GetHashCode()
			{
				return base.GetHashCode();
			}

			/// <summary>
			/// Gets or sets the name.
			/// </summary>
			internal string Name { get; set; }

			/// <summary>
			/// Gets the locale.
			/// </summary>
			internal string Locale { get; }
		}
	}
}