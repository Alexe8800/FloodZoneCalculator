using System;
using System.Windows.Forms;
using FloodZoneCalculator.Infrastructure;
using FloodZoneCalculator.Presentation;

namespace FloodZoneCalculator
{

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            System.Windows.Forms.Application.ThreadException += (sender, args) =>
            {
                AppLogger.Error("Необработанная ошибка UI.", args.Exception);
                MessageBox.Show("Произошла ошибка. Подробности записаны в logs.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception
                    ?? new Exception(Convert.ToString(args.ExceptionObject));
                AppLogger.Error("Необработанная ошибка приложения.", exception);
            };

            AppLogger.Info("Запуск FloodZoneCalculator.");
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.Run(new ConnectionForm());
            AppLogger.Info("Приложение завершено.");
        }
    }
}