using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

namespace HeavyDuck.Utilities.Forms
{
    /// <summary>
    /// Provides a dynamically generated options dialog.
    /// </summary>
    public class OptionsDialog
    {
        protected const int PADDING_STANDARD = 6;
        protected const int PADDING_FORM_BOTTOM = 12;
        protected const int PADDING_TABS_WIDTH = 10;
        protected const int PADDING_TABS_HEIGHT = 24;
        protected const int PADDING_CHECKBOX = 3;
        protected const int LABEL_WIDTH = 120;
        protected const int CONTROL_WIDTH = 200;

        private Dictionary<string, Option> m_options = new Dictionary<string, Option>();
        private Form m_form;

        protected OptionsDialog(XPathDocument doc)
        {
            XPathNavigator docNav = doc.CreateNavigator();
            XPathNodeIterator groupIter, optionIter, enumIter;
            TabControl tabs;
            TabPage page;

            // create the stupid form
            m_form = new Form();
            m_form.Text = "Options";
            m_form.FormBorderStyle = FormBorderStyle.FixedDialog;
            m_form.ControlBox = false;
            m_form.ShowInTaskbar = false;
            m_form.StartPosition = FormStartPosition.CenterParent;

            // create the tab group
            tabs = new TabControl();
            tabs.Location = new System.Drawing.Point(PADDING_STANDARD, PADDING_STANDARD);
            tabs.ClientSize = new System.Drawing.Size(LABEL_WIDTH + CONTROL_WIDTH + PADDING_STANDARD * 3 + PADDING_TABS_WIDTH, 100);
            m_form.Controls.Add(tabs);

            try
            {
                // start with groups
                groupIter = docNav.Select("/OptionsDefinition/Group");
                while (groupIter.MoveNext())
                {
                    string groupName = groupIter.Current.SelectSingleNode("@name").Value;
                    int nextTop = PADDING_STANDARD;

                    // create the tab page
                    page = new TabPage(groupName);
                    page.UseVisualStyleBackColor = true;

                    // continue with options for this group
                    optionIter = groupIter.Current.Select("Option");
                    while (optionIter.MoveNext())
                    {
                        // read basic info from the node
                        string optionName = optionIter.Current.SelectSingleNode("@name").Value;
                        string optionLabel = optionIter.Current.SelectSingleNode("@label").Value;
                        OptionType type = Util.EnumParse<OptionType>(optionIter.Current.SelectSingleNode("@type").Value);
                        string d = optionIter.Current.SelectSingleNode("@default").Value;
                        object defaultValue;

                        // declare option and controls
                        Label label;
                        Control control;
                        Option option;

                        // do the type-specific control creation nonsense
                        switch (type)
                        {
                            case OptionType.String:
                                // read some stuff that will tell us whether we need a file/folder picker
                                XPathNavigator pickerNode = optionIter.Current.SelectSingleNode("@picker");
                                XPathNavigator filterNode = optionIter.Current.SelectSingleNode("@filter");
                                string picker = pickerNode == null ? null : pickerNode.Value;
                                string filter = filterNode == null ? null : filterNode.Value;

                                // decide what kind of control we want
                                if (picker == null)
                                    control = new TextBox();
                                else
                                    control = CreatePickerPanel(picker, filter);

                                // done with strings
                                defaultValue = d;
                                break;
                            case OptionType.Number:
                                // number specific stuff to read and create
                                decimal min = Convert.ToDecimal(optionIter.Current.SelectSingleNode("@min").Value);
                                decimal max = Convert.ToDecimal(optionIter.Current.SelectSingleNode("@max").Value);
                                int decimals = optionIter.Current.SelectSingleNode("@decimals").ValueAsInt;
                                NumericUpDown nud = new NumericUpDown();
                                
                                // configure the control
                                nud.Minimum = min;
                                nud.Maximum = max;
                                nud.DecimalPlaces = decimals;

                                // yes this is the control
                                control = nud;
                                defaultValue = Convert.ToDecimal(d);
                                break;
                            case OptionType.Enum:
                                // create the combo box
                                ComboBox combo = new ComboBox();
                                
                                // iterate the options
                                enumIter = optionIter.Current.Select("ValueList/Value");
                                while (enumIter.MoveNext())
                                    combo.Items.Add(enumIter.Current.Value);

                                // bind and done
                                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                                control = combo;
                                defaultValue = d;
                                break;
                            case OptionType.Bool:
                                control = new CheckBox();
                                control.Text = optionLabel;
                                defaultValue = Convert.ToBoolean(d);
                                break;
                            default:
                                throw new ArgumentException("Invalid option type " + type.ToString());
                        }

                        // initialize the option's value to the default and add it to the collection
                        option = new Option(optionName, type, defaultValue, control);
                        option.Reset();
                        m_options[optionName] = option;

                        // add label for non-checkboxes
                        if (type != OptionType.Bool)
                        {
                            label = new Label();
                            label.Text = optionLabel;
                            label.AutoSize = false;
                            label.AutoEllipsis = true;
                            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                            label.Height = option.Control.Height;
                            label.Width = LABEL_WIDTH;
                            label.Location = new System.Drawing.Point(PADDING_STANDARD, nextTop);
                            page.Controls.Add(label);
                        }

                        // add the control
                        option.Control.Width = CONTROL_WIDTH;
                        if (type == OptionType.Bool)
                            option.Control.Location = new System.Drawing.Point(PADDING_STANDARD + PADDING_CHECKBOX, nextTop);
                        else
                            option.Control.Location = new System.Drawing.Point(LABEL_WIDTH + PADDING_STANDARD * 2, nextTop);
                        page.Controls.Add(option.Control);

                        // bump down the next control
                        nextTop = option.Control.Bottom + PADDING_STANDARD;
                    }

                    // add the finished page to the tabs
                    tabs.TabPages.Add(page);

                    // increase the height of the tabs if necessary
                    if (nextTop + PADDING_TABS_HEIGHT > tabs.ClientSize.Height) tabs.ClientSize = new System.Drawing.Size(tabs.ClientSize.Width, nextTop + PADDING_TABS_HEIGHT);
                }

                // create buttons
                Button ok_button = new Button();
                Button cancel_button = new Button();
                Button defaults_button = new Button();

                // configure the buttons
                cancel_button.DialogResult = DialogResult.Cancel;
                cancel_button.Text = "Cancel";
                cancel_button.Location = new System.Drawing.Point(tabs.Right - cancel_button.Width, tabs.Bottom + PADDING_STANDARD);
                ok_button.DialogResult = DialogResult.OK;
                ok_button.Text = "Ok";
                ok_button.Location = new System.Drawing.Point(cancel_button.Left - PADDING_STANDARD - ok_button.Width, cancel_button.Top);
                defaults_button.Text = "Defaults";
                defaults_button.Location = new System.Drawing.Point(PADDING_STANDARD, cancel_button.Top);
                defaults_button.Click += new EventHandler(defaults_button_Click);
                defaults_button.TabStop = false;

                // add the buttons to the form
                m_form.Controls.Add(defaults_button);
                m_form.Controls.Add(ok_button);
                m_form.Controls.Add(cancel_button);
                m_form.AcceptButton = ok_button;
                m_form.CancelButton = cancel_button;

                // ok, that's all done, let's size the form
                m_form.ClientSize = new System.Drawing.Size(tabs.Width + PADDING_STANDARD * 2, tabs.Height + ok_button.Height + PADDING_STANDARD * 2 + PADDING_FORM_BOTTOM);
            }
            catch (Exception ex)
            {
                throw new OptionsFormatException("Failed to parse the options definition", ex);
            }
        }

        private void defaults_button_Click(object sender, EventArgs e)
        {
            // confirm, then reinitialize all the controls with their default values
            if (MessageBox.Show(m_form, "This will reset all options to their default values. Continue?", "Reset to Defaults", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            {
                foreach (Option o in m_options.Values)
                {
                    o.InitializeControlValue(o.DefaultValue);
                }
            }
        }

        /// <summary>
        /// Creates a panel containing a text box and browse button.
        /// </summary>
        /// <param name="picker">The type of picker to use (save, open, folder).</param>
        /// <param name="filter">The file type filter, if any.</param>
        protected static Panel CreatePickerPanel(string picker, string filter)
        {
            Panel panel;
            TextBox box;
            Button button;

            // check that picker is valid
            if (string.IsNullOrEmpty(picker)) throw new ArgumentNullException("picker");

            // create the controls
            box = new TextBox();
            box.Name = "box";
            button = new Button();
            button.TabStop = false;
            button.Tag = filter;
            button.Text = "...";
            button.Width = 26;
            panel = new Panel();
            panel.Controls.Add(box);
            panel.Controls.Add(button);

            // adjust all the sizes and anchors
            panel.Height = box.Height;
            button.Height = box.Height;
            box.Location = new System.Drawing.Point(0, 0);
            button.Location = new System.Drawing.Point(box.Width + 6, 0);
            panel.Width = button.Right;
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;
            button.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;

            // set the click event
            switch (picker)
            {
                case "save":
                    button.Click += ShowSavePicker;
                    break;
                case "open":
                    button.Click += ShowOpenPicker;
                    break;
                case "folder":
                    button.Click += ShowFolderPicker;
                    break;
                default:
                    throw new ArgumentException("Invalid picker: " + picker);
            }

            // return the control
            return panel;
        }

        private static TextBox FindPathBox(object sender)
        {
            // do some slightly sketchy poking around to find the text box
            Button button = (Button)sender;
            return (TextBox)button.Parent.Controls["box"];
        }

        private static void ShowSavePicker(object sender, EventArgs e)
        {
            TextBox box = FindPathBox(sender);
            string filter = (string)((Control)sender).Tag;

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.FileName = box.Text;
                dialog.Filter = filter;

                if (dialog.ShowDialog() == DialogResult.OK)
                    box.Text = dialog.FileName;
            }
        }

        private static void ShowOpenPicker(object sender, EventArgs e)
        {
            TextBox box = FindPathBox(sender);
            string filter = (string)((Control)sender).Tag;

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.FileName = box.Text;
                dialog.Filter = filter;

                if (dialog.ShowDialog() == DialogResult.OK)
                    box.Text = dialog.FileName;
            }
        }

        private static void ShowFolderPicker(object sender, EventArgs e)
        {
            TextBox box = FindPathBox(sender);

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = box.Text;
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                    box.Text = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Shows the options dialog.
        /// </summary>
        /// <param name="owner">The parent window.</param>
        /// <returns>The dialog result.</returns>
        public DialogResult Show(IWin32Window owner)
        {
            DialogResult result;

            // initialize proposed values to the current value
            foreach (Option option in m_options.Values)
                option.InitializeControlValue();

            // show the dialog
            result = m_form.ShowDialog(owner);

            // if the result is Ok, commit the values
            if (result == DialogResult.OK)
            {
                foreach (Option option in m_options.Values)
                    option.AcceptControlValue();
            }

            // return the result
            return result;
        }

        /// <summary>
        /// Gets an option.
        /// </summary>
        /// <param name="name">The name of the option.</param>
        public IOption this[string name]
        {
            get { return m_options[name]; }
        }

        /// <summary>
        /// Writes the current value of each option to a TextWriter.
        /// </summary>
        /// <param name="output">The TextWriter to write to.</param>
        public void SaveValuesXml(TextWriter output)
        {
            XmlWriterSettings settings;

            // set up the writer
            settings = new XmlWriterSettings();
            settings.Encoding = new UTF8Encoding(false);
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.OmitXmlDeclaration = true;

            // write stuff
            using (XmlWriter writer = XmlWriter.Create(output, settings))
            {
                // the main element
                writer.WriteStartElement("OptionValues");

                // the loop
                foreach (Option o in m_options.Values)
                {
                    writer.WriteStartElement("Option");
                    writer.WriteAttributeString("name", o.Name);
                    writer.WriteValue(o.Value);
                    writer.WriteEndElement();
                }

                // end, flushing is always problematic
                writer.WriteEndElement();
                writer.Flush();
            }
        }

        /// <summary>
        /// Reads option values from a TextReader.
        /// </summary>
        /// <param name="reader">A TextReader containing XML data with an OptionValues element.</param>
        public void LoadValuesXml(TextReader reader)
        {
            XPathDocument doc = new XPathDocument(reader);
            XPathNavigator docNav = doc.CreateNavigator();
            XPathNodeIterator iter = docNav.Select("//OptionValues/Option");

            while (iter.MoveNext())
            {
                string name = iter.Current.SelectSingleNode("@name").Value;
                string value = iter.Current.Value;
                Option o;

                // grab the option, if there is one
                if (!m_options.ContainsKey(name)) continue;
                o = m_options[name];

                // set the value as appropriate
                switch (o.OptionType)
                {
                    case OptionType.String:
                    case OptionType.Enum:
                        o.Value = value;
                        break;
                    case OptionType.Number:
                        o.Value = XmlConvert.ToDecimal(value);
                        break;
                    case OptionType.Bool:
                        o.Value = XmlConvert.ToBoolean(value);
                        break;
                }
            }
        }

        /// <summary>
        /// Creates a new OptionsDialog from an XML string.
        /// </summary>
        /// <param name="optionsXml">The options definition XML.</param>
        public static OptionsDialog FromString(string optionsXml)
        {
            using (StringReader reader = new StringReader(optionsXml))
                return new OptionsDialog(new XPathDocument(reader));
        }

        /// <summary>
        /// Creates a new OptionsDialog from a stream.
        /// </summary>
        /// <param name="stream">The stream containing options definition XML.</param>
        public static OptionsDialog FromStream(Stream stream)
        {
            return new OptionsDialog(new XPathDocument(stream));
        }
    }

    /// <summary>
    /// The exception that is thrown when an error occurs while parsing options definition XML.
    /// </summary>
    public class OptionsFormatException : Exception
    {
        public OptionsFormatException(string message) : base(message) { }
        public OptionsFormatException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Defines properties and methods used to access the value of an option in an OptionsDialog.
    /// </summary>
    public interface IOption
    {
        /// <summary>
        /// Gets the name of the option.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets or sets the current value of the option.
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// Gets the default value of the option.
        /// </summary>
        object DefaultValue { get; }

        /// <summary>
        /// Gets the current value of the option as a string.
        /// </summary>
        string ValueAsString { get; }

        /// <summary>
        /// Gets the current value of the option as a bool.
        /// </summary>
        bool ValueAsBoolean { get; }

        /// <summary>
        /// Gets the current value of the option as an integer.
        /// </summary>
        int ValueAsInt { get; }

        /// <summary>
        /// Gets the current value of the option as a decimal.
        /// </summary>
        decimal ValueAsDecimal { get; }
    }

    /// <summary>
    /// Types of options in an OptionsDialog.
    /// </summary>
    internal enum OptionType
    {
        String,
        Enum,
        Number,
        Bool
    }

    /// <summary>
    /// Represents an option in an OptionsDialog.
    /// </summary>
    internal class Option : IOption
    {
        private string m_name;
        private OptionType m_option_type;
        private Control m_control;
        private object m_value;
        private object m_value_default;

        /// <summary>
        /// Creates a new instance of Option.
        /// </summary>
        /// <param name="name">The name of the option.</param>
        /// <param name="type">The type of the option.</param>
        /// <param name="defaultValue">The default value of the option.</param>
        /// <param name="control">The control that will be used to edit the option.</param>
        public Option(string name, OptionType type, object defaultValue, Control control)
        {
            m_name = name;
            m_option_type = type;
            m_value_default = defaultValue;
            m_control = control;
        }

        /// <summary>
        /// Resets the value of the option to its default.
        /// </summary>
        public void Reset()
        {
            m_value = m_value_default;
        }

        /// <summary>
        /// Initializes the edit control with the current value of the option.
        /// </summary>
        public void InitializeControlValue()
        {
            InitializeControlValue(m_value);
        }

        /// <summary>
        /// Initializes the edit control with a particular value.
        /// </summary>
        /// <param name="value">The value.</param>
        public void InitializeControlValue(object value)
        {
            switch (m_option_type)
            {
                case OptionType.String:
                    if (m_control is Panel)
                        ((Panel)m_control).Controls["box"].Text = (string)value;
                    else
                        m_control.Text = (string)value;
                    break;
                case OptionType.Number:
                    ((NumericUpDown)m_control).Value = (decimal)value;
                    break;
                case OptionType.Enum:
                    m_control.Text = (string)value;
                    break;
                case OptionType.Bool:
                    ((CheckBox)m_control).Checked = (bool)value;
                    break;
                default:
                    throw new InvalidOperationException("Unknown option type " + m_option_type.ToString());
            }
        }

        /// <summary>
        /// Sets the option's value to the current value of its edit control.
        /// </summary>
        public void AcceptControlValue()
        {
            switch (m_option_type)
            {
                case OptionType.String:
                    if (m_control is Panel)
                        m_value = ((Panel)m_control).Controls["box"].Text;
                    else
                        m_value = m_control.Text;
                    break;
                case OptionType.Number:
                    m_value = ((NumericUpDown)m_control).Value;
                    break;
                case OptionType.Enum:
                    m_value = m_control.Text;
                    break;
                case OptionType.Bool:
                    m_value = ((CheckBox)m_control).Checked;
                    break;
                default:
                    throw new InvalidOperationException("Unknown option type " + m_option_type.ToString());
            }
        }

        /// <summary>
        /// Gets the type of this option.
        /// </summary>
        public OptionType OptionType
        {
            get { return m_option_type; }
        }

        /// <summary>
        /// Gets the control used to edit this option.
        /// </summary>
        public Control Control
        {
            get { return m_control; }
            set { m_control = value; }
        }

        #region IOption Members

        public string Name
        {
            get { return m_name; }
        }

        public object Value
        {
            get { return m_value; }
            set { m_value = value; }
        }

        public object DefaultValue
        {
            get { return m_value_default; }
        }

        public string ValueAsString
        {
            get { return m_value.ToString(); }
        }

        public bool ValueAsBoolean
        {
            get { return Convert.ToBoolean(m_value); }
        }

        public int ValueAsInt
        {
            get { return Convert.ToInt32(m_value); }
        }

        public decimal ValueAsDecimal
        {
            get { return Convert.ToDecimal(m_value); }
        }

        #endregion
    }
}
