namespace MusicOrganizer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"Une erreur inattendue est survenue et l'application doit continuer prudemment.\n\n{ex?.Message}",
                "Gestionnaire Musique - Erreur",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        Application.ThreadException += (_, e) =>
        {
            MessageBox.Show(
                $"Une erreur inattendue est survenue.\n\n{e.Exception.Message}",
                "Gestionnaire Musique - Erreur",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        Application.Run(new MainForm());
    }
}
