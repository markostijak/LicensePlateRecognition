using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace LicensePlateRecognition {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private ImageProcessing imageProcessing;
        private String[] images;
        private int counter;
        private bool procesing;

        public MainWindow() {
            InitializeComponent();
        }

        private void Window_Loaded(Object sender, RoutedEventArgs e) {
            imageProcessing = new ImageProcessing();
            images = new String[0];
            statusLabel.Content = "Ready";
        }

        private void leftButton_Click(Object sender, RoutedEventArgs e) {
            if (images.Length > 1)
                NextImage(--counter);
        }

        private void rightButton_Click(Object sender, RoutedEventArgs e) {
            if (images.Length > 1)
                NextImage(++counter);
        }

        private void Grid_DragOver(Object sender, DragEventArgs e) {
            lock (this) {
                if (procesing) return;
            }
            bool dropEnabled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
                string[] filenames = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (filenames != null) {
                    if (filenames.Length == 1 && Directory.Exists(filenames[0])) {
                        String dir = filenames[0];
                        filenames = Directory.GetFiles(dir);
                    }
                    if (!filenames.Select(Path.GetExtension).Any(ext => ".jpg".Equals(ext) || ".jpeg".Equals(ext) || ".png".Equals(ext) || ".tiff".Equals(ext) || ".bmp".Equals(ext)))
                        dropEnabled = false;
                }
            } else {
                dropEnabled = false;
            }


            if (!dropEnabled) {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void Grid_Drop(Object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                String[] filenames = (string[]) e.Data.GetData(DataFormats.FileDrop);

                if (Directory.Exists(filenames[0])) {
                    filenames = Directory.GetFiles(filenames[0], "*.*").Where(f => f.ToLower().EndsWith(".jpg") || f.ToLower().EndsWith(".jpeg") || f.ToLower().EndsWith(".png") || f.ToLower().EndsWith(".tiff") || f.ToLower().EndsWith(".bmp")).ToArray();
                }
                images = filenames;
                counter = 0;
                SetImageStretch();
                NextImage(counter);
            }
        }

        private void MenuItem_Click_3(Object sender, RoutedEventArgs e) {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Select a pictures";
            op.Filter = "All supported graphics|*.jpg;*.jpeg;*.png;*.tiff;*.bmp|" + "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" + "Portable Network Graphic (*.png)|*.png|" + "TIFF (*.tiff)|*.tiff|" + "Bitmap (*.bmp)|*.bmp";
            op.Multiselect = true;

            if (op.ShowDialog() == true) {
                images = op.FileNames;
                counter = 0;
                SetImageStretch();
                NextImage(counter);
            }
        }

        private void MenuItem_Click_2(Object sender, RoutedEventArgs e) {
            Close();
        }

        private void Window_KeyDown(Object sender, KeyEventArgs e) {
            if (images.Length <= 1 || procesing) return;
            switch (e.Key) {
                case Key.Up:
                case Key.Right:
                    NextImage(++counter);
                    break;
                case Key.Down:
                case Key.Left:
                    NextImage(--counter);
                    break;
            }
        }

        private void LoadImages(String path) {
            if (Directory.Exists(path))
                images = Directory.GetFiles(path);
            else if (File.Exists(path))
                images = new[] {path};

            SetImageStretch();
            NextImage(counter);
        }

        private void NextImage(int index) {
            lock (this) {
                if (images == null || images.Length < 1) return;
            }
            if (index >= images.Length)
                counter = 0;
            else if (index < 0)
                counter = images.Length - 1;
            ProcessImage();
        }

        private async void ProcessImage() {
            lock (this) {
                procesing = true;
            }
            leftButton.IsEnabled = rightButton.IsEnabled = false;
            statusLabel.Content = "License plate recognition in process. Please wait...";
            listView.Items.Clear();

            ShowImage(images[counter]);
            await Task.Run(() => imageProcessing.Apply(images[counter]));
            ShowImage(imageProcessing.Image);

            List<String> plates = imageProcessing.Plates;
            String s = "License plate recognition finished in " + imageProcessing.ElapsedMilliseconds + " ms. ";
            switch (plates.Count) {
                case 0:
                    s += "Nothing found.";
                    break;
                case 1:
                    s += HumanFriendlyInteger.IntegerToWritten(plates.Count) + " license plate found.";
                    break;
                default:
                    s += HumanFriendlyInteger.IntegerToWritten(plates.Count) + " license plates found.";
                    break;
            }
            statusLabel.Content = s;

            foreach (var plate in plates)
                listView.Items.Add(new ListViewItem {Content = plate});

            leftButton.IsEnabled = rightButton.IsEnabled = true;
            lock (this) {
                procesing = false;
            }
        }

        private void ShowImage(String path) {
            ImageSource image = new BitmapImage(new Uri(path));
            imageView.Source = image;
        }

        private void ShowImage(ImageSource image) {
            imageView.Source = image;
        }

        private void SetImageStretch() {
            if (imageView.Stretch == Stretch.None)
                imageView.Stretch = Stretch.Uniform;
        }
    }
}