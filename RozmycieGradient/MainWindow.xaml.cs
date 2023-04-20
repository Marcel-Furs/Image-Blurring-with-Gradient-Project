using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace RozmycieGradient
{
    public partial class MainWindow : Window
    {
        object locker = new object();
        struct Color
        {
            public byte a;
            public byte r;
            public byte g;
            public byte b;
        };

        BitmapImage selectedBitmap;
        BitmapImage loadBitmap;
        Bitmap saveBitmap;
        //=============================================
        [DllImport(@"..\..\..\..\..\x64\Debug\GradientAsm.dll")]
        static extern long GetFilters(byte r, byte g, byte b, byte a, int filter);

        //=============================================
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Load(object sender, RoutedEventArgs e)
        {

            OpenFileDialog openFileDialog = new OpenFileDialog(); //Klasa które odpowiada za okndo dialogowe, które wybiera plik
            openFileDialog.Filter = "Picture files (*.jgp)|*.jpg|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true) //jesli plik zostanie wybrany to:
            {
                string selectedFileName = openFileDialog.FileName;
                selectedBitmap = new BitmapImage();
                selectedBitmap.BeginInit();
                selectedBitmap.UriSource = new Uri(selectedFileName);
                selectedBitmap.EndInit();
                pictureSource.Source = selectedBitmap;
            }
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        { }
        private void Run(object sender, RoutedEventArgs e)
        {
            if (csOption.IsChecked == true)
            {
                Stopwatch watch = new System.Diagnostics.Stopwatch();
                var blur = new GradientBlur();
                //var watch = new System.Diagnostics.Stopwatch();
                watch.Start();

                int intensinity = (int)intensitySlider.Value;
                int threads = (int)threadsSlider.Value;

                pictureModified.Source = ConvertBitmap(blur.Create(selectedBitmap, threads, intensinity));
                loadBitmap = (BitmapImage)pictureModified.Source;
                saveBitmap = BitmapImage2Bitmap(loadBitmap);
                watch.Stop();
                textcs.Text = $"{watch.ElapsedMilliseconds} ms";
            }
            else if (asmOption.IsChecked == true)
            {
                Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();

                int intensinity = (int)intensitySlider.Value;
                int threads = (int)threadsSlider.Value;

                Bitmap bitmap = CreateASM(selectedBitmap, threads, intensinity);
                pictureModified.Source = ConvertBitmap(bitmap);
                loadBitmap = (BitmapImage)pictureModified.Source;
                saveBitmap = BitmapImage2Bitmap(loadBitmap);

                watch.Stop();
                textasm.Text = $"{watch.ElapsedMilliseconds} ms";
            }
        }

        private System.Windows.Controls.Image GetPictureModified()
        {
            return pictureModified;
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog sf = new SaveFileDialog();
                sf.Filter = "JPG(*.JPG)|*.jpg";
                if (sf.ShowDialog() == true)
                {
                    if (saveBitmap == null) MessageBox.Show("Prosze wybrac zdjecie");
                    saveBitmap.Save(sf.FileName);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was a problem saving the file." +
                    "Check the file permissions.");
            }
        }

        public BitmapImage ConvertBitmap(System.Drawing.Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();

            return image;
        }

        private void threadsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (threadsTb != null)
            {
                int threads = (int)threadsSlider.Value;
                threadsTb.Text = threads + "";
            }
        }
        private void intensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (intensinityTb != null)
            {
                int intensity = (int)intensitySlider.Value;
                intensinityTb.Text = intensity + "";
            }
        }
        //===================================================================================================================================================
        public System.Drawing.Bitmap CreateASM(BitmapImage source, int threads, int intensinity)
        {
            // Tworzymy obiekt typu WriteableBitmap i ustawiamy go jako źródło obrazu
            WriteableBitmap sourceBitmap = new WriteableBitmap(source);

            // Określamy rozmiar maski filtra (np. 3x3) i stworzymy tablicę wag dla każdego piksela
            const int filterSize = 3;

            double n = 10.0;
            if (intensinity == 1) n = 10.0;
            if (intensinity == 2) n = 9.0;
            if (intensinity == 3) n = 8.0;
            if (intensinity == 4) n = 7.0;
            if (intensinity == 5) n = 6.0;
            if (intensinity == 6) n = 5.0;
            if (intensinity == 7) n = 4.0;
            if (intensinity == 8) n = 3.0;
            if (intensinity == 9) n = 2.0;
            if (intensinity == 10) n = 1.0;

            double[,] filterWeights = new double[filterSize, filterSize]
            {
            { 1/n, 1/n, 1/n },
            { 1/n, 1/n, 1/n },
            { 1/n, 1/n, 1/n }
            };

            // Tworzymy obiekt typu WriteableBitmap, który będzie przechowywać wynik rozmycia obrazu
            //WriteableBitmap targetBitmap = new WriteableBitmap(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight,
            //                                                   sourceBitmap.DpiX, sourceBitmap.DpiY, PixelFormats.Bgra32, null);

            System.Drawing.Color[,] sourcePixels = GetPixels(sourceBitmap);

            //Color[,] targetPixels = new Color[sourceBitmap.PixelWidth, sourceBitmap.PixelHeight];
            System.Drawing.Bitmap target = new System.Drawing.Bitmap(source.PixelWidth, source.PixelHeight);

            List<Task> tasks = new List<Task>();
            if (threads > sourceBitmap.PixelWidth)
            {
                threads = sourceBitmap.PixelWidth;
            }

            int step = sourceBitmap.PixelWidth / threads;
            for (int i = 0; i < threads; i++)
            {
                int height = sourceBitmap.PixelHeight;
                int pixelWidth = sourceBitmap.PixelWidth;
                int pixelHeight = sourceBitmap.PixelHeight;

                int _i = i;
                Task t = Task.Run(() =>
                {
                    int xStart = _i * step;
                    int xStop = (_i + 1) * step;
                    if (_i == threads - 1)
                    {
                        xStop = pixelWidth;
                    }
                    // Iterujemy przez każdy piksel obrazu źródłowego i obliczamy jego wartość po rozmyciu za pomocą maski filtra
                    for (int x = xStart; x < xStop; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            CountPixelASM(x, y, filterSize, filterWeights, sourcePixels, target, pixelWidth, pixelHeight);
                        }
                    }
                });
                tasks.Add(t);
            }

            foreach (var t in tasks)
            {
                t.Wait();
            }

            return target;
        }

        private void CountPixelASM(int x, int y, int filterSize, double[,] filterWeights, System.Drawing.Color[,] sourcePixels, System.Drawing.Bitmap target, int pixelWidth, int pixelHeight)
        {
            // Inicjalizujemy zmienną przechowującą sumę wag dla danego piksela
            double sumWeights = 0;

            // Inicjalizujemy zmienne przechowujące składowe koloru dla danego piksela po rozmyciu
            double red = 0;
            double green = 0;
            double blue = 0;
            double alpha = 0;
            // Iterujemy przez maskę filtra i obliczamy sumę wag oraz składowe koloru dla danego piksela 
            for (int filterX = 0; filterX < filterSize; filterX++)
            {
                for (int filterY = 0; filterY < filterSize; filterY++)
                {
                    // Określamy indeks piksela, dla którego obliczamy wartości
                    int pixelX = x - filterSize / 2 + filterX;
                    int pixelY = y - filterSize / 2 + filterY;

                    // Sprawdzamy, czy indeksy piksela są prawidłowe (nie wychodzą poza granice obrazu)
                    if (pixelX >= 0 && pixelX < pixelWidth && pixelY >= 0 && pixelY < pixelHeight)
                    {
                        // Pobieramy kolor piksela
                        System.Drawing.Color pixelColor = sourcePixels[pixelX, pixelY];

                        // Obliczamy sumę wag oraz składowe koloru dla danego piksela
                        sumWeights += filterWeights[filterX, filterY];
                        int filter = (int)(filterWeights[filterX, filterY] * 100);
                        long result = GetFilters(pixelColor.R, pixelColor.G, pixelColor.B, pixelColor.A, filter);

                        double[] ingredients = InterpretAsmResult(result);
                        red += ingredients[0];
                        green += ingredients[1];
                        blue += ingredients[2];
                        alpha += ingredients[3];
                    }
                }
            }

            lock (locker)
            {
                target.SetPixel(x, y, System.Drawing.Color.FromArgb((byte)alpha, (byte)red, (byte)green, (byte)blue));
            }
        }


        double[] InterpretAsmResult(long asmResult)
        {
            double A = ((double)(asmResult & 0xFFFF)) / 100;
            double B = ((double)((asmResult >> 16) & 0xFFFF)) / 100;
            double G = ((double)((asmResult >> 32) & 0xFFFF)) / 100;
            double R = ((double)((asmResult >> 48) & 0xFFFF)) / 100;
            return new double[] { R, G, B, A };
        }


        private System.Drawing.Color[,] GetPixels(WriteableBitmap bitmap)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            byte[] bytes = new byte[width * height * 4];
            bitmap.CopyPixels(bytes, width * 4, 0);

            System.Drawing.Color[,] pixels = new System.Drawing.Color[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int index = (y * width + x) * 4;
                    byte b = bytes[index];
                    byte g = bytes[index + 1];
                    byte r = bytes[index + 2];
                    byte a = bytes[index + 3];
                    pixels[x, y] = System.Drawing.Color.FromArgb(a, r, g, b);
                }
            }
            return pixels;
        }

        public static Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

    }
}
