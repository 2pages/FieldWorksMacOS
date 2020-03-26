// Copyright (c) 2019-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SIL.Extensions;
using SIL.Windows.Forms.WritingSystems.WSIdentifiers;

namespace SIL.FieldWorks.FwCoreDlgs
{
	/// <summary/>
	internal sealed partial class AdvancedScriptRegionVariantView : UserControl, ISelectableIdentifierOptions
	{
		private AdvancedScriptRegionVariantModel _model;

		/// <summary/>
		internal AdvancedScriptRegionVariantView(AdvancedScriptRegionVariantModel model = null)
		{
			_model = model;
			InitializeComponent();
		}

		#region ISelectableIdentifierOptions impl
		/// <summary/>
		public void Selected()
		{
			UpdateDisplayFromModel(null, null);
		}

		/// <summary/>
		public void MoveDataFromViewToModel()
		{
		}

		/// <summary/>
		public void UnwireBeforeClosing()
		{
			RemoveAllEventHandlers();
		}
		#endregion

		private static void UpdateDisplayFromModel(object o, EventArgs eventArgs)
		{
		}

		private void RemoveAllEventHandlers()
		{
			// disconnect all change event handlers
			_scriptChooser.SelectedIndexChanged -= _scriptChooser_SelectedIndexChanged;
			_regionChooser.SelectedIndexChanged -= _regionChooser_SelectedIndexChanged;
			_standardVariantCombo.SelectedIndexChanged -= _standardVariantCombo_SelectedIndexChanged;
			_scriptCodeTextBox.TextChanged -= _scriptCodeTextBox_TextChanged;
			_variantsTextBox.TextChanged -= _variantsTextBox_TextChanged;
			_scriptNameTextbox.TextChanged -= _scriptNameTextbox_TextChanged;
			_regionNameTextBox.TextChanged -= _regionNameTextBox_TextChanged;
			_regionCodeTextbox.TextChanged -= _regionCodeTextbox_TextChanged;
			_ietftagTextBox.TextChanged -= _ietftagTextBox_TextChanged;
		}

		private void AddAllEventHandlers()
		{
			// Reconnect all change event handlers
			_scriptChooser.SelectedIndexChanged += _scriptChooser_SelectedIndexChanged;
			_regionChooser.SelectedIndexChanged += _regionChooser_SelectedIndexChanged;
			_standardVariantCombo.SelectedIndexChanged += _standardVariantCombo_SelectedIndexChanged;
			_scriptCodeTextBox.TextChanged += _scriptCodeTextBox_TextChanged;
			_variantsTextBox.TextChanged += _variantsTextBox_TextChanged;
			_scriptNameTextbox.TextChanged += _scriptNameTextbox_TextChanged;
			_regionNameTextBox.TextChanged += _regionNameTextBox_TextChanged;
			_regionCodeTextbox.TextChanged += _regionCodeTextbox_TextChanged;
			_ietftagTextBox.TextChanged += _ietftagTextBox_TextChanged;
		}

		/// <summary/>
		protected override void Dispose(bool disposing)
		{
			UnwireBeforeClosing();
			if (disposing)
			{
				components?.Dispose();
			}
			base.Dispose(disposing);
		}

		/// <summary/>
		internal void BindToModel(AdvancedScriptRegionVariantModel modelCurrentWsSetupModel)
		{
			_specialTypeComboBox.SelectedIndex = 0;
			RemoveAllEventHandlers();
			_model = modelCurrentWsSetupModel;
			_abbreviation.Text = _model.Abbreviation;
			_ietftagTextBox.Text = _model.Code;
			_regionNameTextBox.Text = _model.RegionName;
			_regionCodeTextbox.Text = _model.RegionCode;
			_regionCodeTextbox.Enabled = _regionNameTextBox.Enabled = _model.EnableRegionCode;
			_scriptCodeTextBox.Text = _model.ScriptCode;
			_scriptCodeTextBox.Enabled = _scriptNameTextbox.Enabled = _model.EnableScriptCode;
			_scriptNameTextbox.Text = _model.ScriptName;
			_variantsTextBox.Text = _model.OtherVariants;
			_scriptChooser.Items.Clear();
			var scripts = _model.GetScripts().ToArray();
			foreach (var scriptChoice in scripts)
			{
				_scriptChooser.Items.Add(new ScriptChoiceView(scriptChoice.Label, scriptChoice));
			}
			if (_model.Script != null)
			{
				var index = scripts.IndexOf(r => r.Equals(_model.Script));
				_scriptChooser.SelectedIndex = index;
			}
			_regionChooser.Items.Clear();
			var modelRegions = _model.GetRegions().ToArray();
			foreach (var regionChoice in modelRegions)
			{
				_regionChooser.Items.Add(new RegionChoiceView(regionChoice.Label, regionChoice));
			}
			if (_model.Region != null)
			{
				var index = modelRegions.IndexOf(r => r.Equals(_model.Region));
				_regionChooser.SelectedIndex = index;
			}
			_standardVariantCombo.Items.Clear();
			var standardVariants = _model.GetStandardVariants().ToArray();
			foreach (var variant in standardVariants)
			{
				_standardVariantCombo.Items.Add(new VariantChoiceView(variant));
			}
			// _model.StandardVariant can be null in which case we will select 'None'
			var variantIndex = standardVariants.IndexOf(v => v.Code == _model.StandardVariant);
			_standardVariantCombo.SelectedIndex = variantIndex;
			// Clear all error indicators
			_scriptCodeTextBox.BackColor = Color.Empty;
			_variantsTextBox.BackColor = Color.Empty;
			_regionCodeTextbox.BackColor = Color.Empty;
			_ietftagTextBox.BackColor = Color.Empty;
			AddAllEventHandlers();
		}

		private sealed class ScriptChoiceView : Tuple<string, ScriptListItem>
		{
			internal ScriptChoiceView(string label, ScriptListItem scriptChoice) : base(label, scriptChoice)
			{
			}

			public override string ToString()
			{
				return Item1;
			}
		}

		private sealed class RegionChoiceView : Tuple<string, RegionListItem>
		{
			internal RegionChoiceView(string label, RegionListItem regionListItem) : base(label, regionListItem)
			{
			}

			public override string ToString()
			{
				return Item1;
			}
		}

		private sealed class VariantChoiceView : Tuple<string, VariantListItem>
		{
			internal VariantChoiceView(VariantListItem variant): base(variant.Name, variant)
			{
			}

			public override string ToString()
			{
				return Item1;
			}
		}

		private void _variantsTextBox_TextChanged(object sender, EventArgs e)
		{
			if(_model.ValidateOtherVariants(_variantsTextBox.Text))
			{
				var cursorPos = _variantsTextBox.SelectionStart;
				_model.OtherVariants = _variantsTextBox.Text;
				BindToModel(_model);
				_variantsTextBox.Focus();
				_variantsTextBox.SelectionStart = cursorPos;
			}
			else if (_variantsTextBox.Text.Length > 0)
			{
				_variantsTextBox.BackColor = Color.Red;
			}
		}

		private void _scriptChooser_SelectedIndexChanged(object sender, EventArgs e)
		{
			_model.Script = ((ScriptChoiceView)_scriptChooser.SelectedItem).Item2;
			BindToModel(_model);
		}

		private void _regionChooser_SelectedIndexChanged(object sender, EventArgs e)
		{
			_model.Region = ((RegionChoiceView)_regionChooser.SelectedItem).Item2;
			BindToModel(_model);
		}

		private void _standardVariantCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			_model.StandardVariant = ((VariantChoiceView)_standardVariantCombo.SelectedItem).Item2?.ToString();
			BindToModel(_model);
		}

		private void _scriptCodeTextBox_TextChanged(object sender, EventArgs e)
		{
			if (_model.ValidateScriptCode(_scriptCodeTextBox.Text))
			{
				var cursorPos = _scriptCodeTextBox.SelectionStart;
				_model.ScriptCode = _scriptCodeTextBox.Text;
				BindToModel(_model);
				_scriptCodeTextBox.Focus();
				_scriptCodeTextBox.SelectionStart = cursorPos;
			}
			else if(_scriptCodeTextBox.Text.Length > 0)
			{
				_scriptCodeTextBox.BackColor = Color.Red;
			}
		}

		private void _scriptNameTextbox_TextChanged(object sender, EventArgs e)
		{
			_model.ScriptName = _scriptNameTextbox.Text;
		}

		private void _regionNameTextBox_TextChanged(object sender, EventArgs e)
		{
			_model.RegionName = _regionNameTextBox.Text;
		}

		private void _regionCodeTextbox_TextChanged(object sender, EventArgs e)
		{
			if (_model.ValidateRegionCode(_regionCodeTextbox.Text))
			{
				var cursorPos = _regionCodeTextbox.SelectionStart;
				_model.RegionCode = _regionCodeTextbox.Text;
				BindToModel(_model);
				_regionCodeTextbox.Focus();
				_regionCodeTextbox.SelectionStart = cursorPos;
			}
			else if (_regionCodeTextbox.Text.Length > 0)
			{
				_regionCodeTextbox.BackColor = Color.Red;
			}
		}

		private void _ietftagTextBox_TextChanged(object sender, EventArgs e)
		{
			if (_model.ValidateIetfCode(_ietftagTextBox.Text))
			{
				var cursorPos = _ietftagTextBox.SelectionStart;
				_model.Code = _ietftagTextBox.Text;
				BindToModel(_model);
				_ietftagTextBox.Focus();
				_ietftagTextBox.SelectionStart = cursorPos;
			}
			else if (_ietftagTextBox.Text.Length > 0)
			{
				_ietftagTextBox.BackColor = Color.Red;
			}
		}
	}
}