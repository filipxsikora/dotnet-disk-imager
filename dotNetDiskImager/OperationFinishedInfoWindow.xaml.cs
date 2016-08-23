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
    /// Interaction logic for OperationFinishedInfoWindow.xaml
    /// </summary>
    public partial class OperationFinishedInfoWindow : Window
    {
        public OperationFinishedInfoWindow(Window owner, LastOperationInfo info)
        {
            Owner = owner;
            InitializeComponent();
            BuildMessage(info);
        }

        void BuildMessage(LastOperationInfo info)
        {
            /*StringBuilder sb = new StringBuilder(4096);

            sb.Append("Description: ");
            sb.Append(BuildOperationMessage(info.OperationFinishedArgs));
            sb.Append(string.Format("\n\nElapsed time: {0}\n\n", Helpers.TimeSpanToString(info.ElapsedTime)));
            sb.Append(string.Format("Image file: {0}\n\n", info.ImageFile));
            sb.Append(string.Format("Device{0}: ", info.Devices.Length > 1 ? "s" : ""));
            foreach(var device in info.Devices)
            {
                sb.Append(string.Format("{0}:\\ ", device));
            }
            sb.Append("\n\n");
            if (info.OperationFinishedArgs.Exception != null)
            {
                sb.Append("Error description: ");
                sb.Append(info.OperationFinishedArgs.Exception.Message);
                if (info.OperationFinishedArgs.Exception.InnerException != null)
                {
                    sb.Append(info.OperationFinishedArgs.Exception.InnerException.Message);
                }
            }

            //moreInfoTextBox.Text = sb.ToString();
            paragraph.Inlines.Add(new Run(sb.ToString()));*/

            paragraph.Inlines.Add(new Run("Description: ") { FontWeight = FontWeights.Bold });
            paragraph.Inlines.Add(BuildOperationMessage(info.OperationFinishedArgs) + "\n\n");
            paragraph.Inlines.Add(new Run("Elapsed time: ") { FontWeight = FontWeights.Bold });
            paragraph.Inlines.Add(Helpers.TimeSpanToString(info.ElapsedTime) + "\n\n");
            paragraph.Inlines.Add(new Run("Image file: ") { FontWeight = FontWeights.Bold });
            paragraph.Inlines.Add(info.ImageFile + "\n\n");
            paragraph.Inlines.Add(new Run(string.Format("Device{0}: ", info.Devices.Length > 1 ? "s" : "")) { FontWeight = FontWeights.Bold });
            foreach (var device in info.Devices)
            {
                paragraph.Inlines.Add(string.Format("{0}:\\ ", device));
            }
            paragraph.Inlines.Add("\n\n");
            if (info.OperationFinishedArgs.Exception != null)
            {
                paragraph.Inlines.Add(new Run("Error description: ") { FontWeight = FontWeights.Bold });
                paragraph.Inlines.Add(info.OperationFinishedArgs.Exception.Message);
                if (info.OperationFinishedArgs.Exception.InnerException != null)
                {
                    paragraph.Inlines.Add(info.OperationFinishedArgs.Exception.InnerException.Message);
                }
            }
        }

        string BuildOperationMessage(OperationFinishedEventArgs e)
        {
            string message;
            if ((e.DiskOperation & DiskOperation.Read) > 0)
            {
                message = "Reading";
                if ((e.DiskOperation & DiskOperation.Verify) > 0)
                {
                    message += " and verify";
                }
            }
            else if ((e.DiskOperation & DiskOperation.Write) > 0)
            {
                message = "Writing";
                if ((e.DiskOperation & DiskOperation.Verify) > 0)
                {
                    message += " and verify";
                }
            }
            else
            {
                message = "Verifying";
            }

            switch (e.OperationState)
            {
                case OperationFinishedState.Success:
                    message += " was finished successfully";
                    break;
                case OperationFinishedState.Canceled:
                    message += " was canceled";
                    break;
                case OperationFinishedState.Error:
                    message += " was unsuccessful";
                    break;
            }

            return message;
        }
    }
}
