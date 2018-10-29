using dotNetDiskImager.DiskAccess;
using dotNetDiskImager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace dotNetDiskImager
{
    /// <summary>
    /// Interaction logic for DeviceInfoWindow.xaml
    /// </summary>
    public partial class DeviceInfoWindow : Window
    {
        SolidColorBrush[] colors = new SolidColorBrush[] { new SolidColorBrush(Color.FromRgb(0, 145, 0)), new SolidColorBrush(Color.FromRgb(0, 111, 185)),
            new SolidColorBrush(Color.FromRgb(159, 107, 0)), new SolidColorBrush(Color.FromRgb(229, 20, 0))};
        DrawingBrush unallocatedColor = null;
        int lastUsedColor = 0;

        public DeviceInfoWindow(Window owner, char driveLetter)
        {
            string partitionTableName = "";

            Owner = owner;
            unallocatedColor = FindResource("unallocatedBrush") as DrawingBrush;
            InitializeComponent();

            var partitionInfo = Disk.GetDiskPartitionInfo(driveLetter);

            for (int i = 0; i < partitionInfo.PartitionSizes.Count; i++)
            {
                Add((double)partitionInfo.PartitionSizes[i] / partitionInfo.DiskTotalSize, string.Format("Partition {0}", i + 1), partitionInfo.PartitionSizes[i], true);
            }

            if (((double)partitionInfo.UnallocatedSize / partitionInfo.DiskTotalSize) > 0.01)
            {
                Add((double)partitionInfo.UnallocatedSize / partitionInfo.DiskTotalSize, "Unallocated", partitionInfo.UnallocatedSize, false);
            }

            deviceNameTextBlock.Text = Disk.GetModelFromDrive(driveLetter);

            switch (partitionInfo.PartitionType)
            {
                case PartitionType.GPT:
                    partitionTableName = "GUID Partition Table";
                    break;
                case PartitionType.MBR:
                    partitionTableName = "Master Boot Record";
                    break;
                case PartitionType.RAW:
                    partitionTableName = "RAW";
                    break;
            }

            deviceSizeAndInfoTextBlock.Text = string.Format("{0} ({1}) - {2}", Helpers.BytesToXbytes(partitionInfo.DiskTotalSize), string.Format("{0:#,0} Bytes", partitionInfo.DiskTotalSize), partitionTableName);
        }

        void Add(double percent, string textLabel, ulong partitionSize, bool allocated)
        {
            partitionsHolder.Children.Add(new Rectangle() { Fill = allocated ? (Brush)colors[lastUsedColor] : unallocatedColor, Width = percent * 487 });

            var sp = new StackPanel();

            var grid = new Grid() { VerticalAlignment = VerticalAlignment.Top, Height = 30 };
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(25, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

            var rect = new Rectangle() { Fill = allocated ? (Brush)colors[lastUsedColor++] : unallocatedColor, Width = 20, Height = 20, Margin = new Thickness(5, 0, 0, 0) };
            Grid.SetColumn(rect, 0);
            grid.Children.Add(rect);

            var label = new Label()
            {
                Content = textLabel,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = FindResource("Foreground") as Brush
            };
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            sp.Children.Add(grid);

            sp.Children.Add(new Label()
            {
                Content = Helpers.BytesToXbytes(partitionSize),
                Foreground = FindResource("Foreground") as Brush
            });

            partitionsInfoHolder.Children.Add(sp);

            if (lastUsedColor == colors.Length)
                lastUsedColor = 0;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }
    }
}
