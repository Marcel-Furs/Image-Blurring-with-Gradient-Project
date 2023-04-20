using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;

namespace RozmycieGradient
{


    public class GradientBlur
    {
        object locker = new object();

        public System.Drawing.Bitmap Create(BitmapImage source, int threads, int intensinity)
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

            Color[,] sourcePixels = GetPixels(sourceBitmap);
            //Color[,] targetPixels = new Color[sourceBitmap.PixelWidth, sourceBitmap.PixelHeight];
            System.Drawing.Bitmap target = new System.Drawing.Bitmap(source.PixelWidth, source.PixelHeight);

            // kazdy watek ma osobna bitmape ktora polaczy sie w jedna sklejajac paski 

            List<Task> tasks = new List<Task>();
            if(threads > sourceBitmap.PixelWidth)
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
                    if(_i == threads - 1)
                    {
                        xStop = pixelWidth;
                    }
                    // Iterujemy przez każdy piksel obrazu źródłowego i obliczamy jego wartość po rozmyciu za pomocą maski filtra
                    for (int x = xStart; x < xStop; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            CountPixel(x, y, filterSize, filterWeights, sourcePixels, target, pixelWidth, pixelHeight);
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

        private void CountPixel(int x, int y, int filterSize, double[,] filterWeights, Color[,] sourcePixels, System.Drawing.Bitmap target, int pixelWidth, int pixelHeight)
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
                        Color pixelColor = sourcePixels[pixelX, pixelY]; //sourceBitmap.GetPixel();

                        // Obliczamy sumę wag oraz składowe koloru dla danego piksela
                        sumWeights += filterWeights[filterX, filterY];
                        red += pixelColor.R * filterWeights[filterX, filterY];
                        green += pixelColor.G * filterWeights[filterX, filterY];
                        blue += pixelColor.B * filterWeights[filterX, filterY];
                        alpha += pixelColor.A * filterWeights[filterX, filterY];
                    }
                }
            }
            // Dzielimy składowe koloru przez sumę wag, aby uzyskać średnią wagową
            // Ustawiamy kolor dla danego piksela na obrazie wynikowym
            Color pixelColorx = sourcePixels[x, y];

            lock(locker)
            {
                target.SetPixel(x, y, System.Drawing.Color.FromArgb((byte)alpha, (byte)red, (byte)green, (byte)blue));
            }
        }


        private Color[,] GetPixels(WriteableBitmap bitmap)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            byte[] bytes = new byte[width * height * 4];
            bitmap.CopyPixels(bytes, width * 4, 0);

            Color[,] pixels = new Color[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int index = (y * width + x) * 4;
                    byte b = bytes[index];
                    byte g = bytes[index + 1];
                    byte r = bytes[index + 2];
                    byte a = bytes[index + 3];
                    pixels[x, y] = Color.FromArgb(a, r, g, b);
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