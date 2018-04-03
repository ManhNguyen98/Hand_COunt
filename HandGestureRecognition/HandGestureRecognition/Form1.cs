using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Emgu.CV.Structure;
using Emgu.CV;
using HandGestureRecognition.SkinDetector;

namespace HandGestureRecognition
{
    public partial class Form1 : Form
    {

        IColorSkinDetector skinDetector;

        Image<Bgr, Byte> currentFrame;        // bi?n currentFrame d? ch? khung hi?n t?i
        Image<Bgr, Byte> currentFrameCopy;    // bi?n currentFramecompy d? ch? khung tuong thích trên frame  ph?n chi?u

        Capture grabber;                   // bi?n grabber d? có du?c hình ?nh t? video file or camera
        AdaptiveSkinDetector detector;

        // m?t vài bi?n d? xác d?nh m?t s? thu?c tính c?a video file
        int frameWidth;   // d? r?ng c?a frame
        int frameHeight;  // chi?u cao c?a frame

        Hsv hsv_min;    // ngu?ng du?i và ngu?ng trên c?a hsv  
        Hsv hsv_max;
        Ycc YCrCb_min;  // ngu?ng du?i và ngu?ng trên c?a YCrCB
        Ycc YCrCb_max;

        // khai báo 1 s? bi?n d? ch? các di?m gi?i h?n và các di?m khuy?t trên bàn tay
        Seq<Point> hull;
        Seq<Point> filteredHull;
        Seq<MCvConvexityDefect> defects;
        MCvConvexityDefect[] defectArray;
        Rectangle handRect; // bi?n d? hi?n th? khung
        MCvBox2D box; // bi?n  kh?i t?o 1 khung (hình ch? nh?t)
        Ellipse ellip; // bi?n kh?i t?o 1 ellip

        // constructor kh?i t?o giá tr?
        public Form1()
        {
            InitializeComponent();
            Run();
            //grabber = new Capture();
            //grabber.QueryFrame(); // nh?n khung hình t? video file
            //frameWidth = grabber.Width;    // thiet lap kich thuoc cua khung lay tu kich thuoc cua video file da co
            //frameHeight = grabber.Height;
            detector = new AdaptiveSkinDetector(1, AdaptiveSkinDetector.MorphingMethod.NONE); // nh?n di?n skin
            /* xác d?nh ngu?ng trên và ngu?ng du?i c?a hsv and YCrCB color space 
             có th? di?u ch?nh d? phù hop v?i video file  */
            hsv_min = new Hsv(0, 45, 0);
            hsv_max = new Hsv(20, 255, 255);
            YCrCb_min = new Ycc(0, 131, 80);
            YCrCb_max = new Ycc(255, 185, 135);
            box = new MCvBox2D();
            ellip = new Ellipse();

            // g?n thêm FrameGrabber vào Eventhandler d? truy c?p vào hsv frame and YCrCB frame
            Application.Idle += new EventHandler(FrameGrabber);

        }
        private void Run()
        {
            try
            {
                grabber = new Capture();
            }
            catch (Exception Ex)
            {
                MessageBox.Show(Ex.Message);
                return;
            }
            Application.Idle += ProcessFrame;
        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            imageBoxFrameGrabber.Image = grabber.QuerySmallFrame();
        }

        // truy c?p vào khung tham chi?u t? video file
        void FrameGrabber(object sender, EventArgs e)
        {
            currentFrame = grabber.QueryFrame();
            if (currentFrame != null)
            {
                currentFrameCopy = currentFrame.Copy(); // có du?c khung ánh x? c?a bàn tay(khung tr?ng den)

                // s? d?ng YcrCbskinDetector d? nh?n di?n skin
                skinDetector = new YCrCbSkinDetector();

                Image<Gray, Byte> skin = skinDetector.DetectSkin(currentFrameCopy, YCrCb_min, YCrCb_max);

                ExtractContourAndHull(skin);

                DrawAndComputeFingersNum();

                imageBoxSkin.Image = skin;
                imageBoxFrameGrabber.Image = currentFrame;
            }
        }
        // class MemStorage() d? t?o b? nh? m? cho openCV
        MemStorage storage = new MemStorage();
        // chi?t xu?t ra du?ng vi?n bao b?c bàn tay
        private void ExtractContourAndHull(Image<Gray, byte> skin)
        {
            {
                // tìm du?ng vi?n bao b?c bàn tay
                Contour<Point> contours = skin.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST, storage);
                // biggest contour chính là du?ng bi?u th? du?ng bao b?c bàn tay
                Contour<Point> biggestContour = null;
                // class contour() dê ta? du?ng vi?n s? d?ng b? nh? storage
                Double Result1 = 0;
                Double Result2 = 0;
                while (contours != null)
                {
                    Result1 = contours.Area;
                    if (Result1 > Result2)
                    {
                        Result2 = Result1;
                        biggestContour = contours;
                    }
                    contours = contours.HNext;
                }

                if (biggestContour != null)
                {
                    // class ApproxPoly(Double, MemStorage) x?p x? 1 du?ng cong và tr? v? k?t qu? x?p x?
                    Contour<Point> currentContour = biggestContour.ApproxPoly(biggestContour.Perimeter * 0.0025, storage);
                    // dung màu xanh là cây d? bi?u di?n du?ng vi?n bao tay
                    currentFrame.Draw(currentContour, new Bgr(Color.LimeGreen), 2);

                    biggestContour = currentContour;


                    hull = biggestContour.GetConvexHull(Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    box = biggestContour.GetMinAreaRect();
                    PointF[] points = box.GetVertices();
                    /* hi?n th? toàn b? khung chua bàn tay khi s? d?ng 2 câu l?nh sau
                     -handRect = box.MinAreaRect();
                     -currentFrame.Draw(handRect, new Bgr(200, 0, 0), 1);*/

                    Point[] ps = new Point[points.Length];
                    for (int i = 0; i < points.Length; i++)
                        ps[i] = new Point((int)points[i].X, (int)points[i].Y);

                    currentFrame.DrawPolyline(hull.ToArray(), true, new Bgr(200, 125, 75), 2);
                    currentFrame.Draw(new CircleF(new PointF(box.center.X, box.center.Y), 3), new Bgr(200, 125, 75), 2);

                    PointF center;
                    float radius;
                    #region v? ellip gi?i h?n
                    // v? du?ng ellip gi?i h?n bao quanh khi s? d?ng nh?ng câu l?nh du?i dây (2 câu l?nh s? ra 1 màu khác nhau)

                    //ellip.MCvBox2D= CvInvoke.cvFitEllipse2(biggestContour.Ptr);
                    //currentFrame.Draw(new Ellipse(ellip.MCvBox2D), new Bgr(Color.LavenderBlush), 3);

                    //CvInvoke.cvMinEnclosingCircle(biggestContour.Ptr, out  center, out  radius);
                    //currentFrame.Draw(new CircleF(center, radius), new Bgr(Color.Gold), 2);

                    //currentFrame.Draw(new CircleF(new PointF(ellip.MCvBox2D.center.X, ellip.MCvBox2D.center.Y), 3), new Bgr(100, 25, 55), 2);
                    //currentFrame.Draw(ellip, new Bgr(Color.DeepPink), 2);

                    //CvInvoke.cvEllipse(currentFrame, new Point((int)ellip.MCvBox2D.center.X, (int)ellip.MCvBox2D.center.Y), new System.Drawing.Size((int)ellip.MCvBox2D.size.Width, (int)ellip.MCvBox2D.size.Height), ellip.MCvBox2D.angle, 0, 360, new MCvScalar(120, 233, 88), 1, Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED, 0);
                    //currentFrame.Draw(new Ellipse(new PointF(box.center.X, box.center.Y), new SizeF(box.size.Height, box.size.Width), box.angle), new Bgr(0, 0, 0), 2);
                    #endregion

                    filteredHull = new Seq<Point>(storage);
                    for (int i = 0; i < hull.Total; i++)
                    {
                        if (Math.Sqrt(Math.Pow(hull[i].X - hull[i + 1].X, 2) + Math.Pow(hull[i].Y - hull[i + 1].Y, 2)) > box.size.Width / 10)
                        {
                            filteredHull.Push(hull[i]);
                        }
                    }

                    defects = biggestContour.GetConvexityDefacts(storage, Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);

                    defectArray = defects.ToArray();
                }
            }
        }
        // ve va dem so luong ngon tay
        //   int fingerNum = 0;
        private void DrawAndComputeFingersNum()
        {

            int fingerNum = 0;

            #region defects drawing
            for (int i = 0; i < defects.Total; i++)
            {
                LoadListView();
                // kh?i t?o 3 di?m startpoint , depthpoint và endpoint c?a convexity defect
                // hàm PointF(single,single) d? kh?i t?o 1 di?m v?i các  t?a d? c? th? 
                PointF startPoint = new PointF((float)defectArray[i].StartPoint.X,
                                                (float)defectArray[i].StartPoint.Y);

                PointF depthPoint = new PointF((float)defectArray[i].DepthPoint.X,
                                                (float)defectArray[i].DepthPoint.Y);

                PointF endPoint = new PointF((float)defectArray[i].EndPoint.X,
                                                (float)defectArray[i].EndPoint.Y);
                // hàm 	LineSegment2D(Point2D<T>, Point2D<T>) d? t?o 1 do?n th?ng v?i di?m d?u và di?m cu?i c? th? 

                LineSegment2D startDepthLine = new LineSegment2D(defectArray[i].StartPoint, defectArray[i].DepthPoint);

                LineSegment2D depthEndLine = new LineSegment2D(defectArray[i].DepthPoint, defectArray[i].EndPoint);

                // hàm  CircleF(PointF, Single) d? t?o 1 vòng tròn v?i bán kính c? th?

                CircleF startCircle = new CircleF(startPoint, 5f);

                CircleF depthCircle = new CircleF(depthPoint, 5f);

                CircleF endCircle = new CircleF(endPoint, 5f);



                // n?u do?n n?i gi?a start point và end point d? l?n thì s? du?c tính là 1 ngón tay
                if ((startCircle.Center.Y < box.center.Y || depthCircle.Center.Y < box.center.Y) && (startCircle.Center.Y < depthCircle.Center.Y) && (Math.Sqrt(Math.Pow(startCircle.Center.X - depthCircle.Center.X, 2) + Math.Pow(startCircle.Center.Y - depthCircle.Center.Y, 2)) > box.size.Height / 6.5))
                {
                    fingerNum++;
                    // v? do?n màu da cam d? xác d?nh s? ngón tay t? startpoint d?n depthpoint
                    currentFrame.Draw(startDepthLine, new Bgr(Color.Orange), 2);
                    // currentFrame.Draw(depthEndLine, new Bgr(Color.Magenta), 2);
                }



                currentFrame.Draw(startCircle, new Bgr(Color.Red), 2); // start point bi?u di?n b?ng n?t màu d?
                currentFrame.Draw(depthCircle, new Bgr(Color.Yellow), 5); // depthpoint bi?u di?n b?ng n?t màu vàng
                                                                          // currentFrame.Draw(endCircle, new Bgr(Color.DarkBlue), 4); // endpoint bi?u di?n b?ng n?t màu darkblue
            }
            #endregion


            // hàm MCvFont(FONT, Double, Double) d? t?o phông ch? (hi? th? s? lu?ng ngón tay), quy mô theo chi?u ngang và d?c
            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 5d, 5d);
            currentFrame.Draw(fingerNum.ToString(), ref font, new Point(50, 150), new Bgr(Color.White));

            void LoadListView()
            {
                Lsv.Items.Add(fingerNum.ToString());
            }

        }

        private void imageBoxFrameGrabber_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("do you want to quit!", "Note", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
               == DialogResult.Yes)
                Application.Exit();
        }

        private void splitContainerFrames_Panel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}