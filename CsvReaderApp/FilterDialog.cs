namespace CsvReaderApp;

internal sealed class FilterDialog : Form
{
    private readonly ComboBox columnBox = new();
    private readonly ComboBox operatorBox = new();
    private readonly TextBox valueBox = new();
    private readonly Button applyButton = new();
    private readonly Button clearButton = new();
    private readonly Button cancelButton = new();
    private readonly ToolTip toolTip = new();

    public FilterDialog(IReadOnlyList<string> columns, StringFilter? current)
    {
        Text = "Filter";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 184);
        KeyPreview = true;
        UiTheme.ApplyForm(this);

        BuildUi();
        columnBox.Items.AddRange(columns.Cast<object>().ToArray());
        operatorBox.Items.AddRange(new object[] { "=", ">", "<", ">=", "<=" });

        if (current is null)
        {
            columnBox.SelectedIndex = columns.Count > 0 ? 0 : -1;
            operatorBox.SelectedItem = "=";
        }
        else
        {
            columnBox.SelectedItem = current.ColumnName;
            operatorBox.SelectedItem = current.Operator;
            valueBox.Text = current.Value;
        }

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.SuppressKeyPress = true;
            }
        };
    }

    public StringFilter? Result { get; private set; }

    private void BuildUi()
    {
        var columnLabel = CreateLabel("Column:", 18);
        var operatorLabel = CreateLabel("Condition:", 58);
        var valueLabel = CreateLabel("Value:", 98);

        columnBox.SetBounds(106, 14, 294, 24);
        operatorBox.SetBounds(106, 54, 120, 24);
        valueBox.SetBounds(106, 94, 294, 24);
        ConfigureInput(columnBox);
        ConfigureInput(operatorBox);
        ConfigureInput(valueBox);
        columnBox.DropDownStyle = ComboBoxStyle.DropDownList;
        operatorBox.DropDownStyle = ComboBoxStyle.DropDownList;
        valueBox.KeyDown += ValueBoxOnKeyDown;

        UiTheme.ApplyIconButton(applyButton, toolTip, UiIconKind.Ok, "Apply filter", primary: true);
        applyButton.SetBounds(190, 138, 34, 30);
        applyButton.Click += (_, _) => Apply();

        UiTheme.ApplyIconButton(clearButton, toolTip, UiIconKind.Clear, "Clear filter");
        clearButton.SetBounds(236, 138, 34, 30);
        clearButton.Click += (_, _) => Clear();

        UiTheme.ApplyIconButton(cancelButton, toolTip, UiIconKind.Cancel, "Cancel");
        cancelButton.SetBounds(282, 138, 34, 30);
        cancelButton.Click += (_, _) => Close();

        Controls.Add(columnLabel);
        Controls.Add(operatorLabel);
        Controls.Add(valueLabel);
        Controls.Add(columnBox);
        Controls.Add(operatorBox);
        Controls.Add(valueBox);
        Controls.Add(applyButton);
        Controls.Add(clearButton);
        Controls.Add(cancelButton);
    }

    private static Label CreateLabel(string text, int top)
    {
        return new Label
        {
            Text = text,
            ForeColor = UiTheme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(18, top),
            Size = new Size(82, 24)
        };
    }

    private static void ConfigureInput(Control control)
    {
        control.BackColor = UiTheme.Surface;
        control.ForeColor = UiTheme.Text;
    }

    private void ValueBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            Apply();
            e.SuppressKeyPress = true;
        }
    }

    private void Apply()
    {
        if (columnBox.SelectedItem is not string columnName || operatorBox.SelectedItem is not string comparisonOperator)
        {
            return;
        }

        Result = new StringFilter(columnName, comparisonOperator, valueBox.Text);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Clear()
    {
        Result = null;
        DialogResult = DialogResult.OK;
        Close();
    }
}
