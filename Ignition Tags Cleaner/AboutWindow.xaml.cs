using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TagCleaner
{
    public partial class AboutWindow : Window
    {
        #region Constructor
        public AboutWindow()
        {
            InitializeComponent();
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            if (ver != null)
            {
                AboutVersionText.Text = $"Version {ver.Major}.{ver.Minor}.{ver.Build}";
            }

            LoadAppIcon();
        }
        #endregion

        #region App icon
        private void LoadAppIcon()
        {
            try
            {
                // Attempt 1: Pack URI embedded resource
                var iconUri = new Uri("pack://application:,,,/Resources/app_icon.ico", UriKind.Absolute);
                AboutAppIcon.Source = BitmapFrame.Create(iconUri);
            }
            catch
            {
                try
                {
                    // Attempt 2: File system icon extraction
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon.ico");
                    if (!File.Exists(iconPath))
                    {
                        string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\Resources\\app_icon.ico"));
                        if (File.Exists(devPath)) iconPath = devPath;
                    }

                    if (File.Exists(iconPath))
                    {
                        using (var icon = new System.Drawing.Icon(iconPath, 64, 64))
                        {
                            AboutAppIcon.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                }
                catch { }
            }
        }
        #endregion

        #region Events
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion
    }
}
