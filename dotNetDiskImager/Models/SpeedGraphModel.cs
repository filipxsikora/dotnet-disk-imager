using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace dotNetDiskImager.Models
{
    public class SpeedGraphModel
    {
        private enum GraphMode { Normal, Verify };
        ulong currentMaximum = 0;

        OxyPlot.Wpf.LineAnnotation speedLine;
        RectangleAnnotation progressOverlay;
        LinearAxis yAxis;
        LinearAxis xAxis;
        AreaSeries speedSeries;
        bool firsCall = true;
        GraphMode graphMode = GraphMode.Normal;
        Appearance appearance = Appearance.Light;

        public void UpdateSpeedLineValue(ulong value)
        {
            if ((value / 0.8) > currentMaximum)
            {
                currentMaximum = (ulong)(value / 0.8);
                yAxis.Maximum = currentMaximum;
                yAxis.MajorStep = currentMaximum / 5;
            }

            speedLine.Dispatcher.Invoke(() =>
            {
                speedLine.Text = string.Format("Speed: {0}/s", Helpers.BytesToXbytes(value));
                if (!firsCall)
                {
                    DoubleAnimation anim = new DoubleAnimation(value, TimeSpan.FromMilliseconds(AppSettings.Settings.EnableAnimations ? 500 : 0));
                    anim.SetCurrentValue(Timeline.DesiredFrameRateProperty, 30);
                    speedLine.BeginAnimation(OxyPlot.Wpf.LineAnnotation.YProperty, anim);
                }
                else
                {
                    firsCall = false;
                    speedLine.Y = value;
                }
            });
        }

        public void UpdateColors(Appearance appearance)
        {
            this.appearance = appearance;
            UpdateColorsInternal();
        }

        void UpdateColorsInternal()
        {
            if (appearance == Appearance.Light)
            {
                speedLine.Color = Colors.Black;
                speedLine.TextColor = Colors.Black;

                if (graphMode == GraphMode.Normal)
                {
                    xAxis.MajorGridlineColor = OxyColor.FromRgb(205, 239, 211);
                    yAxis.MajorGridlineColor = OxyColor.FromRgb(205, 239, 211);
                }
                else
                {
                    xAxis.MajorGridlineColor = OxyColor.FromRgb(239, 234, 204);
                    yAxis.MajorGridlineColor = OxyColor.FromRgb(239, 234, 204);
                }
            }
            else
            {
                speedLine.Color = Colors.White;
                speedLine.TextColor = Colors.White;

                if (graphMode == GraphMode.Normal)
                {
                    xAxis.MajorGridlineColor = OxyColor.FromRgb(18, 58, 26);
                    yAxis.MajorGridlineColor = OxyColor.FromRgb(18, 58, 26);
                }
                else
                {
                    xAxis.MajorGridlineColor = OxyColor.FromRgb(59, 52, 18);
                    yAxis.MajorGridlineColor = OxyColor.FromRgb(59, 52, 18);
                }
            }
        }

        public void AddDataPoint(int x, ulong y)
        {
            double speedValue = y;
            if ((y / 0.8) > currentMaximum)
            {
                currentMaximum = (ulong)(y / 0.8);
                yAxis.Maximum = currentMaximum;
                yAxis.MajorStep = currentMaximum / 5;
            }
            progressOverlay.MaximumX = x;
            speedSeries.Points.Add(new DataPoint(x, y));
            Model.InvalidatePlot(true);
        }

        public void ResetToNormal()
        {
            graphMode = GraphMode.Normal;
            speedSeries.Points.Clear();
            progressOverlay.MaximumX = -1;
            currentMaximum = 100;
            yAxis.Maximum = currentMaximum;
            yAxis.MajorStep = currentMaximum / 5;
            speedLine.Text = "";
            speedLine.Y = -10;
            speedSeries.Fill = OxyColor.FromRgb(9, 175, 36);
            speedSeries.Color = OxyColor.FromRgb(9, 175, 36);
            progressOverlay.Fill = OxyColor.FromArgb(96, 61, 229, 0);
            UpdateColorsInternal();
            Model.InvalidatePlot(true);
            firsCall = true;
        }

        public void ResetToVerify()
        {
            graphMode = GraphMode.Verify;
            speedSeries.Points.Clear();
            progressOverlay.MaximumX = -1;
            currentMaximum = 100;
            yAxis.Maximum = currentMaximum;
            yAxis.MajorStep = currentMaximum / 5;
            speedLine.Text = "";
            speedLine.Y = -10;
            speedSeries.Fill = OxyColor.FromRgb(176, 152, 0);
            speedSeries.Color = OxyColor.FromRgb(176, 152, 0);
            progressOverlay.Fill = OxyColor.FromArgb(96, 241, 220, 0);
            UpdateColorsInternal();
            Model.InvalidatePlot(true);
            firsCall = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel" /> class.
        /// </summary>
        public SpeedGraphModel()
        {
            // Create the plot model
            var tmp = new PlotModel
            {
                PlotMargins = new OxyThickness(0),
                //PlotAreaBorderColor = OxyColor.FromRgb(141, 141, 141),
                //PlotAreaBorderThickness = new OxyThickness(1),
                Padding = new OxyThickness(0, 0, 1, 1),
                IsLegendVisible = false
            };

            speedLine = new OxyPlot.Wpf.LineAnnotation()
            {
                LineStyle = LineStyle.Solid,
                Color = Colors.White,
                TextColor = Colors.White,
                Y = -10,
                Type = LineAnnotationType.Horizontal,
                Text = "",
                TextVerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                TextMargin = 5
            };

            xAxis = new LinearAxis()
            {
                TickStyle = TickStyle.None,
                TextColor = OxyColors.Transparent,
                Position = AxisPosition.Bottom,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorStep = 10,
                MajorGridlineColor = OxyColor.FromRgb(205, 239, 211),
                Minimum = 1,
                Maximum = 100
            };

            yAxis = new LinearAxis()
            {
                TickStyle = TickStyle.None,
                TextColor = OxyColors.Transparent,
                Position = AxisPosition.Left,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = OxyColor.FromRgb(205, 239, 211),
                Minimum = 0,
                Maximum = 100,
                MajorStep = 20
            };

            progressOverlay = new RectangleAnnotation()
            {
                MaximumX = -1,
                Fill = OxyColor.FromArgb(96, 61, 229, 0)
            };

            // Create two line series (markers are hidden by default)
            speedSeries = new AreaSeries
            {
                MarkerType = MarkerType.None,
                Fill = OxyColor.FromRgb(9, 175, 36)
            };

            // Add the series to the plot model
            tmp.Series.Add(speedSeries);

            tmp.Axes.Add(xAxis);
            tmp.Axes.Add(yAxis);

            tmp.Annotations.Add(progressOverlay);
            tmp.Annotations.Add(speedLine.InternalAnnotation);

            // Axes are created automatically if they are not defined

            // Set the Model property, the INotifyPropertyChanged event will make the WPF Plot control update its content
            Model = tmp;
        }

        /// <summary>
        /// Gets the plot model.
        /// </summary>
        public PlotModel Model { get; private set; }
    }
}
