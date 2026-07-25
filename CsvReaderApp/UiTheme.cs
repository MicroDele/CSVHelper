namespace CsvReaderApp;

internal enum UiIconKind
{
    Open,
    Save,
    Sort,
    Up,
    Down,
    Search,
    Add,
    Remove,
    Clear,
    Ok,
    Cancel
}

internal static class UiTheme
{
    public static readonly Color WeChatGreen = Color.FromArgb(7, 193, 96);
    public static readonly Color PageBackground = Color.FromArgb(245, 247, 246);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(218, 224, 222);
    public static readonly Color Text = Color.FromArgb(31, 35, 33);
    public static readonly Color MutedText = Color.FromArgb(119, 128, 124);
    public static readonly Color Hover = Color.FromArgb(232, 247, 239);
    public static readonly Color Selection = Color.FromArgb(218, 245, 230);
    public static readonly Color ActiveCell = Color.FromArgb(171, 231, 199);
    public static readonly Color Match = Color.FromArgb(255, 245, 198);

    public static void ApplyForm(Form form)
    {
        form.BackColor = PageBackground;
        form.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    }

    public static void ApplyPanel(Control panel)
    {
        panel.BackColor = PageBackground;
    }

    public static void ApplyIconButton(
        Button button,
        ToolTip toolTip,
        UiIconKind icon,
        string description,
        bool primary = false)
    {
        button.Text = string.Empty;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextImageRelation = TextImageRelation.Overlay;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.TabStop = true;
        button.FlatAppearance.BorderSize = 1;
        toolTip.SetToolTip(button, description);

        // 随 Enabled 变化的外观集中处理：禁用态整体暗淡，使「可点/不可点」一眼可辨
        //（如 Save 在未修改时明显发暗，修改后变亮）。
        button.EnabledChanged += (_, _) => RefreshButtonAppearance(button, icon, primary);
        RefreshButtonAppearance(button, icon, primary);
    }

    private static void RefreshButtonAppearance(Button button, UiIconKind icon, bool primary)
    {
        Color iconColor, backColor, foreColor, borderColor, hoverColor, downColor;

        if (!button.Enabled)
        {
            iconColor = Color.FromArgb(189, 196, 193);
            backColor = Color.FromArgb(240, 242, 241);
            foreColor = iconColor;
            borderColor = Color.FromArgb(228, 233, 231);
            hoverColor = backColor;
            downColor = backColor;
        }
        else if (primary)
        {
            iconColor = Color.White;
            backColor = WeChatGreen;
            foreColor = Color.White;
            borderColor = WeChatGreen;
            hoverColor = Color.FromArgb(6, 174, 86);
            downColor = Color.FromArgb(5, 150, 74);
        }
        else
        {
            iconColor = Text;
            backColor = Surface;
            foreColor = Text;
            borderColor = Border;
            hoverColor = Hover;
            downColor = Selection;
        }

        button.Image?.Dispose();
        button.Image = CreateIcon(icon, iconColor);
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatAppearance.BorderColor = borderColor;
        button.FlatAppearance.MouseOverBackColor = hoverColor;
        button.FlatAppearance.MouseDownBackColor = downColor;
    }

    public static Bitmap CreateIcon(UiIconKind icon, Color color)
    {
        var bitmap = new Bitmap(20, 20);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(color, 2F)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        using var brush = new SolidBrush(color);

        switch (icon)
        {
            case UiIconKind.Open:
                graphics.DrawLine(pen, 3, 8, 7, 5);
                graphics.DrawLine(pen, 7, 5, 10, 8);
                graphics.DrawRectangle(pen, 3, 8, 14, 9);
                graphics.DrawLine(pen, 3, 8, 17, 8);
                break;
            case UiIconKind.Save:
                graphics.DrawRectangle(pen, 4, 3, 12, 14);
                graphics.DrawLine(pen, 7, 3, 7, 8);
                graphics.DrawLine(pen, 13, 3, 13, 8);
                graphics.DrawLine(pen, 7, 14, 13, 14);
                break;
            case UiIconKind.Sort:
                DrawArrow(graphics, pen, 7, 16, 7, 4);
                DrawArrow(graphics, pen, 13, 4, 13, 16);
                break;
            case UiIconKind.Up:
                DrawArrow(graphics, pen, 10, 16, 10, 4);
                break;
            case UiIconKind.Down:
                DrawArrow(graphics, pen, 10, 4, 10, 16);
                break;
            case UiIconKind.Search:
                graphics.DrawEllipse(pen, 3, 3, 10, 10);
                graphics.DrawLine(pen, 11, 11, 16, 16);
                break;
            case UiIconKind.Add:
                graphics.DrawLine(pen, 5, 10, 15, 10);
                graphics.DrawLine(pen, 10, 5, 10, 15);
                break;
            case UiIconKind.Remove:
                graphics.DrawLine(pen, 5, 10, 15, 10);
                break;
            case UiIconKind.Clear:
                graphics.DrawLine(pen, 6, 6, 14, 14);
                graphics.DrawLine(pen, 14, 6, 6, 14);
                graphics.DrawLine(pen, 5, 17, 15, 17);
                break;
            case UiIconKind.Ok:
                graphics.DrawLines(pen, new[] { new Point(4, 10), new Point(8, 14), new Point(16, 6) });
                break;
            case UiIconKind.Cancel:
                graphics.DrawLine(pen, 6, 6, 14, 14);
                graphics.DrawLine(pen, 14, 6, 6, 14);
                break;
        }

        return bitmap;
    }

    private static void DrawArrow(Graphics graphics, Pen pen, int fromX, int fromY, int toX, int toY)
    {
        graphics.DrawLine(pen, fromX, fromY, toX, toY);
        var direction = Math.Sign(toY - fromY);
        if (direction < 0)
        {
            graphics.DrawLine(pen, toX, toY, toX - 4, toY + 4);
            graphics.DrawLine(pen, toX, toY, toX + 4, toY + 4);
            return;
        }

        graphics.DrawLine(pen, toX, toY, toX - 4, toY - 4);
        graphics.DrawLine(pen, toX, toY, toX + 4, toY - 4);
    }
}
