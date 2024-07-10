using Emgu.CV.CvEnum;
using Emgu.CV.Reg;
using Emgu.CV.Structure;
using Emgu.CV;
using GalaSoft.MvvmLight.Command;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Infragistics.Controls.Charts;
using MvvmHelpers;
using System.Windows.Media.Media3D;
using Infragistics.Windows.Helpers;



namespace imageprojection
{
    public class MainViewModel : INotifyPropertyChanged
    {
        #region 생성자
        public MainViewModel()
        {
            //  SelectChange = new GalaSoft.MvvmLight.Command.RelayCommand<object>(SelChange);
            btnClickCommand = new RelayCommand(BtnClickCommandAction);
        }
        #endregion


        #region 속성
        public ICommand btnClickCommand { get; }

        private string imagePath { get; set; }
        private WriteableBitmap btImage;
        public WriteableBitmap BtImage { get => btImage; set => btImage = value; }

        private Bitmap projectionBitmap;
        public Bitmap ProjectionBitmap { get => projectionBitmap; set => projectionBitmap = value; }

        private WriteableBitmap resultImage;
        public WriteableBitmap ResultImage { get => resultImage; set => resultImage = value; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableRangeCollection<PointF> resultChartData = new ObservableRangeCollection<PointF>();
        public ObservableRangeCollection<PointF> ResultChartData
        {
            get => resultChartData;
            set => resultChartData = value;
        }

        #endregion


        #region 메서드
        public void BtnClickCommandAction()
        {
            //파일선택 dialog
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.bmp) | *.bmp",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(openFileDialog.FileName);
                    bitmapImage.EndInit();

                    BtImage = new WriteableBitmap(bitmapImage);
                    OnPropertyChanged("BtImage");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}");
                }
            }
            else
            {
                return;
            }

            using (MemoryStream memoryStream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BtImage));
                encoder.Save(memoryStream);
                ProjectionBitmap = new Bitmap(memoryStream);
            }

            Mat mat = BitmapToMat(ProjectionBitmap);

            PerformProjection(mat);

            OnPropertyChanged("BtResultImage");
            Console.WriteLine("이미지 프로젝션 완료 및 저장.");

        }

        public Bitmap ToBitmap(Image<Bgr, byte> image)
        {
            // Emgu CV Image의 데이터를 가져오기
            byte[] data = image.Bytes;

            // 이미지의 너비와 높이
            int width = image.Width;
            int height = image.Height;

            // 새로운 Bitmap 생성
            Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            // Bitmap의 LockBits를 사용하여 데이터 복사
            System.Drawing.Imaging.BitmapData bmpData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            // 데이터 복사
            System.Runtime.InteropServices.Marshal.Copy(data, 0, bmpData.Scan0, data.Length);

            // Bitmap 해제
            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        public void PerformProjection(Mat binaryImage)
        {
            // 이미지 크기 가져오기
            int width = binaryImage.Width;
            int height = binaryImage.Height;

            List<int> verticalProjection;
            List<int> HorizonProjection;

            HorizonProjection = CalculateHorizontalProjection(binaryImage);

            Mat ResultImagemat = ModifyHorizon(binaryImage.Clone(), HorizonProjection);

            ResultImagemat.Save("D:\\modify.bmp");
            HorizonProjection = CalculateHorizontalProjection(ResultImagemat);
            DrawProjectionGraph(HorizonProjection);

        }

        private void DrawProjectionGraph(List<int> projectionData)
        {
            ResultChartData.Clear();
            for (int i = 0; i < projectionData.Count; ++i)
            {
                ResultChartData.Add(new PointF(projectionData.Count - Convert.ToSingle(i), Convert.ToSingle(projectionData[i])));
            }

            OnPropertyChanged("ResultChartData");
        }

        private List<int> CalculateVerticalProjection(Mat grayImage)
        {
            int width = grayImage.Width;
            int height = grayImage.Height;

            int[] projection = new int[width];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    projection[x] += GetValue(grayImage, y, x);
                }
            }
            List<int> result = projection.ToList();
            return result;
        }

        private List<int> CalculateHorizontalProjection(Mat grayImage)
        {
            int width = grayImage.Width;
            int height = grayImage.Height;
            int[] projection = new int[height];
            List<int> result = new List<int>();
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    projection[y] += GetValue(grayImage, y, x);
                }
                projection[y] /=  width;
            }
            result = projection.ToList();

            return result;
        }

        private Mat ModifyVerticalImage(Mat grayImage, List<int> verticalProjection)
        {
            int width = grayImage.Width;
            int height = grayImage.Height;

            // 수직 방향 프로젝션 적용
            for (int x = 0; x < width; ++x)
            {
                double scale = verticalProjection[x] / 128;
                for (int y = 0; y < height; ++y)
                {
                    SetValue(grayImage, y, x, (byte)(GetValue(grayImage, y, x) * scale));
                }
            }
            return grayImage;
        }

        private Mat ModifyHorizon(Mat grayImage, List<int> horizontalProjection)
        {
            int width = grayImage.Width;
            int height = grayImage.Height;

            // 가로 방향 프로젝션 적용
            for (int y = 0; y < height; y++)
            {
                //double scale = horizontalProjection[y]  128;
                int a = 0;
                a =  128 - horizontalProjection[y];

                for (int x = 0; x < width; x++)
                {
                    //  grayImage.Data[y, x, 0] = (byte)(grayImage.Data[y, x, 0] * scale);
                    if((GetValue(grayImage, y, x) + a) >= 255)
                    {
                        int sdsd = 0;
                    }
                    SetValue(grayImage, y, x, (byte)(GetValue(grayImage, y, x) + a));
                }
            }

            return grayImage;
        }

        public void SetValue(Mat mat, int row, int col, dynamic value)
        {
            var target = CreateElement(mat.Depth, value);
            Marshal.Copy(target, 0, mat.DataPointer + (row * mat.Cols + col) * mat.ElementSize, 1);
        }

        public Mat BitmapToMat(Bitmap bitmap)
        {
            // Bitmap의 PixelFormat 확인
            PixelFormat pixelFormat = bitmap.PixelFormat;

            // 8bppIndexed 포맷 확인
            if (pixelFormat != PixelFormat.Format8bppIndexed)
            {
                throw new NotSupportedException("지원하지 않는 PixelFormat입니다.");
            }

            // Bitmap의 데이터를 byte[] 배열로 복사
            BitmapData bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, pixelFormat);

            byte[] imageData;
            try
            {
                int bytesPerPixel = Image.GetPixelFormatSize(pixelFormat) / 8;
                int imageSize = bmpData.Stride * bitmap.Height;
                imageData = new byte[imageSize];
                System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, imageData, 0, imageSize);
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            // byte[] 배열을 Mat으로 변환
            Mat mat = new Mat(bitmap.Height, bitmap.Width, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
            mat.SetTo(imageData);

            return mat;
        }

        public dynamic GetValue(Mat mat, int row, int col)
        {
            var value = CreateElement(mat.Depth);
            Marshal.Copy(mat.DataPointer + (row * mat.Cols + col) * mat.ElementSize, value, 0, 1);
            return value[0];
        }

        private dynamic CreateElement(DepthType depthType)
        {
            if (depthType == DepthType.Cv8S)
            {
                return new sbyte[1];
            }
            if (depthType == DepthType.Cv8U)
            {
                return new byte[1];
            }
            if (depthType == DepthType.Cv16S)
            {
                return new short[1];
            }
            if (depthType == DepthType.Cv16U)
            {
                return new ushort[1];
            }
            if (depthType == DepthType.Cv32S)
            {
                return new int[1];
            }
            if (depthType == DepthType.Cv32F)
            {
                return new float[1];
            }
            if (depthType == DepthType.Cv64F)
            {
                return new double[1];
            }
            return new float[1];
        }

        private dynamic CreateElement(DepthType depthType, dynamic value)
        {
            var element = CreateElement(depthType);
            element[0] = value;
            return element;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

    }
}
