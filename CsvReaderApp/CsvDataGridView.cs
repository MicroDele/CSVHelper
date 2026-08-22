namespace CsvReaderApp;

internal sealed class CsvDataGridView : DataGridView
{
    public event EventHandler<DataGridViewCellMouseEventArgs>? RowHeaderMouseDown;
    public event EventHandler<DataGridViewCellMouseEventArgs>? AltCellMouseDown;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit.Type == DataGridViewHitTestType.RowHeader && hit.RowIndex >= 0)
        {
            Focus();
            RowHeaderMouseDown?.Invoke(
                this,
                new DataGridViewCellMouseEventArgs(-1, hit.RowIndex, e.X, e.Y, e));
            return;
        }

        if (e.Button == MouseButtons.Left
            && (ModifierKeys & Keys.Alt) == Keys.Alt
            && hit.Type == DataGridViewHitTestType.Cell
            && hit.RowIndex >= 0
            && hit.ColumnIndex >= 0)
        {
            Focus();
            AltCellMouseDown?.Invoke(
                this,
                new DataGridViewCellMouseEventArgs(hit.ColumnIndex, hit.RowIndex, e.X, e.Y, e));
            return;
        }

        base.OnMouseDown(e);
    }
}
