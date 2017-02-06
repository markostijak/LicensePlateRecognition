using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Accord;
using Accord.Imaging;
using Accord.Imaging.Filters;
using Accord.Math.Geometry;
using Tesseract;
using Image = System.Drawing.Image;
using Point = Accord.Point;

namespace LicensePlateRecognition {
    class ImageProcessing {
        private Bitmap originalImage, frame;
        private Stopwatch stopwatch;
        private List<String> plates;

        public ImageProcessing() {
            stopwatch = new Stopwatch();
            InitializeFilters();
            InitializeTesseract();
        }

        // OCR
        private TesseractEngine ocr;

        private void InitializeTesseract() {
            ocr = new TesseractEngine(@"tessdata", "eng");
            ocr.SetVariable("tessedit_char_whitelist", "ABCEFGHJKLMNOPRSTUVW0123456789");
            //ocr.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
        }

        // filtri
        private Grayscale grayscaleFilter;
        private BlobsFiltering plateBlobsFiltering;
        private BlobCounter blobCounter;
        private SimpleShapeChecker shapeChecker;
        private OtsuThreshold otsuThresholdFilter;
        private Pen pen;
        private FillHoles fillHoles;
        private Opening openingFilter;
        private BradleyLocalThresholding bradleyLocalFilter;
        private ContrastCorrection contrastCorrectionFilter;
        private ColorFiltering colorFiltering;
        private BlobCounter plateBlobCounter;
        private Invert invert;


        private void InitializeFilters() {
            grayscaleFilter = new Grayscale(0.299, 0.587, 0.114);
            bradleyLocalFilter = new BradleyLocalThresholding();
            bradleyLocalFilter.WindowSize = 9;
            bradleyLocalFilter.PixelBrightnessDifferenceLimit = 0.01f;
            plateBlobsFiltering = new BlobsFiltering(10, 20, 80, 66);
            blobCounter = new BlobCounter();
            blobCounter.FilterBlobs = true;
            blobCounter.MinWidth = 50;
            blobCounter.MinHeight = 10;
            blobCounter.MaxWidth = 520;
            blobCounter.MaxHeight = 110;
            plateBlobCounter = new BlobCounter();
            shapeChecker = new SimpleShapeChecker();
            otsuThresholdFilter = new OtsuThreshold();
            fillHoles = new FillHoles();
            fillHoles.MaxHoleWidth = 100;
            fillHoles.MaxHoleHeight = 40;
            pen = new Pen(Color.GreenYellow, 4);
            openingFilter = new Opening();
            contrastCorrectionFilter = new ContrastCorrection(80);
            colorFiltering = new ColorFiltering();
            colorFiltering.Red = new IntRange(150, 255);
            colorFiltering.Green = new IntRange(150, 255);
            colorFiltering.Blue = new IntRange(150, 255);
            colorFiltering.FillOutsideRange = true;
            invert = new Invert();
        }

        // za slike
        public void Apply(String path) {
            originalImage = new Bitmap(path);
            process();
        }

        // za video
        public void Apply(Bitmap image) {
            originalImage = image;
            process();
        }

        private void process() {
            stopwatch.Reset();
            stopwatch.Start();
            plates = new List<string>();
            frame = contrastCorrectionFilter.Apply(originalImage);
            frame = grayscaleFilter.Apply(frame);

            BitmapData frameData = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.ReadWrite, frame.PixelFormat);
            UnmanagedImage data = new UnmanagedImage(frameData);

            bradleyLocalFilter.ApplyInPlace(data);
            fillHoles.ApplyInPlace(data);
            openingFilter.ApplyInPlace(data);

            blobCounter.ProcessImage(data);

            data.Dispose();
            frame.UnlockBits(frameData);

            Graphics g = Graphics.FromImage(originalImage);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            foreach (Blob blob in blobs) {
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blob);
                List<IntPoint> corners = null;

                // da li je četverougao?
                if (!shapeChecker.IsQuadrilateral(edgePoints, out corners)) continue;

                if (FindNewCornersAndCheckAspectRatio(corners)) {
                    SimpleQuadrilateralTransformation sqt = new SimpleQuadrilateralTransformation(corners, 300, 66);
                    Bitmap plate = sqt.Apply(originalImage);
                    plate = grayscaleFilter.Apply(plate);
                    otsuThresholdFilter.ApplyInPlace(plate);

                    if (!IsLicensePlate(plate)) continue;

                    String plateText;
                    if (FindText(plate, out plateText)) {
                        g.DrawPolygon(pen, ToPointsArray(corners));
                        frame = plate;
                        plates.Add(plateText);
                    }
                }
            }
            g.Dispose();
            stopwatch.Stop();
        }

        private System.Drawing.Point[] ToPointsArray(List<IntPoint> points) {
            return points.Select(p => new System.Drawing.Point(p.X, p.Y)).ToArray();
        }

        private bool IsLicensePlate(Bitmap plate) {
            invert.ApplyInPlace(plate);
            plateBlobsFiltering.ApplyInPlace(plate);
            plateBlobCounter.ProcessImage(plate);
            Blob[] blobs = plateBlobCounter.GetObjectsInformation();
            int chars = blobs.Length;
            return 5 <= chars && chars <= 9;
        }

        private bool FindText(Bitmap plate, out String text) {
            using (Page page = ocr.Process(plate, PageSegMode.SingleLine)) {
                text = page.GetText();
                if (String.IsNullOrEmpty(text)) return false;
                text = text.Replace(" ", "-");
                text = text.Trim();
                if (text.Length > 9)
                    text = text.Substring(1, text.Length - 1);
                if (text.Length >= 9)
                    return true;

                return false;
            }
        }

        private bool FindNewCornersAndCheckAspectRatio(List<IntPoint> corners) {
            Point g = PointsCloud.GetCenterOfGravity(corners);
            IntPoint p1 = corners[0], p2 = corners[1], p3 = corners[2], p4 = corners[3];

            List<IntPoint> leftPoints = new List<IntPoint>();
            List<IntPoint> rightPoints = new List<IntPoint>();

            foreach (IntPoint p in corners) {
                if (p.X < g.X)
                    leftPoints.Add(p);
                else
                    rightPoints.Add(p);
            }

            if (leftPoints.Count - rightPoints.Count != 0) return false;
            // korekcija
            if (leftPoints[0].Y < leftPoints[1].Y) {
                p4 = leftPoints[0];
                p1 = leftPoints[1];
            } else {
                p4 = leftPoints[1];
                p1 = leftPoints[0];
            }

            if (rightPoints[0].Y < rightPoints[1].Y) {
                p3 = rightPoints[0];
                p2 = rightPoints[1];
            } else {
                p3 = rightPoints[1];
                p2 = rightPoints[0];
            }

            corners[0] = p4;
            corners[1] = p3;
            corners[2] = p2;
            corners[3] = p1;

            float topSideWidth = p1.DistanceTo(p2);
            float rightSideHeight = p3.DistanceTo(p2);
            float ar = topSideWidth/rightSideHeight;
            return 2 <= ar && ar <= 10;
        }

        public Bitmap OriginalImage {
            get { return originalImage; }
        }

        public long ElapsedMilliseconds {
            get { return stopwatch.ElapsedMilliseconds; }
        }

        public List<string> Plates {
            get { return plates; }
        }

        public BitmapSource Image {
            get { return BitmapConverter.ToBitmapSource(originalImage); }
        }
    }
}