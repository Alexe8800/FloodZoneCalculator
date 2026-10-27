#nullable enable

using FloodZoneCalculator.Infrastructure;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Images.cs — работа с растровыми изображениями вкладки «Object data»:
    // схема объекта и изображение гидравлического профиля.
    //
    // Приоритет загрузки: base64 (хранится в профиле, не зависит от пути к файлу),
    // затем файловый путь. Выбор файла читает изображение в память и сразу кладёт его
    // и в PictureBox, и в _objectProfile (path + base64) для последующего сохранения.
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        // Загружает схему объекта в _schemeImage (base64 важнее пути).
        private void LoadSchemeImage(string path, string base64 = "")
        {
            if (_schemeImage == null) return;
            _schemeImage.Image?.Dispose();
            _schemeImage.Image = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(base64))
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(base64));
                    using var source = Image.FromStream(stream);
                    _schemeImage.Image = new Bitmap(source);
                }
                else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using var source = Image.FromFile(path);
                    _schemeImage.Image = new Bitmap(source);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки схемы объекта.", ex);
            }
        }

        // Загружает изображение гидравлического профиля в _hydraulicProfileImage.
        private void LoadHydraulicProfileImage(string path, string base64 = "")
        {
            if (_hydraulicProfileImage == null) return;
            _hydraulicProfileImage.Image?.Dispose();
            _hydraulicProfileImage.Image = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(base64))
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(base64));
                    using var source = Image.FromStream(stream);
                    _hydraulicProfileImage.Image = new Bitmap(source);
                }
                else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using var source = Image.FromFile(path);
                    _hydraulicProfileImage.Image = new Bitmap(source);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки изображения гидравлического профиля.", ex);
            }
        }

        // Кнопка «Добавить изображение схемы»: выбирает файл, кладёт path+base64 в профиль.
        private void OnChooseSchemeClick(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Выберите схему объекта"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _objectProfile.SchemeImagePath = dialog.FileName;
            _objectProfile.SchemeImageBase64 = Convert.ToBase64String(File.ReadAllBytes(dialog.FileName));
            LoadSchemeImage(dialog.FileName, _objectProfile.SchemeImageBase64);
            SetStatus("Схема выбрана. Нажмите сохранение исходных данных.");
        }

        // Аналогично для изображения гидравлического профиля.
        private void OnChooseHydraulicProfileClick(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Выберите изображение гидравлического профиля"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _objectProfile.HydraulicProfileImagePath = dialog.FileName;
            _objectProfile.HydraulicProfileImageBase64 = Convert.ToBase64String(File.ReadAllBytes(dialog.FileName));
            LoadHydraulicProfileImage(dialog.FileName, _objectProfile.HydraulicProfileImageBase64);
            SetStatus("Изображение гидравлического профиля выбрано. Нажмите сохранение исходных данных.");
        }
    }
}
