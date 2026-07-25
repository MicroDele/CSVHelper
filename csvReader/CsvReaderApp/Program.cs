namespace CsvReaderApp;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--inspect")
        {
            var text = File.ReadAllText(args[1], new System.Text.UTF8Encoding(false, true));
            var document = CsvDocument.Parse(text);
            var first = document.Rows.Count > 0 && document.Rows[0].Length > 0 ? document.Rows[0][0] : string.Empty;
            var output = $"rows={document.Rows.Count} columns={document.Headers.Length} first={first}";
            if (args.Length >= 3)
            {
                File.WriteAllText(args[2], output);
            }
            else
            {
                Console.WriteLine(output);
            }
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }    
}
