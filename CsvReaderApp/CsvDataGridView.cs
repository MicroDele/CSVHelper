namespace CsvReaderApp;

internal sealed class CsvDataGridView : DataGridView
{
    public event EventHandler<DataGridViewCellMouseEventArgs>? RowHeaderMouseDown;

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

        base.OnMouseDown(e);
    }
}
