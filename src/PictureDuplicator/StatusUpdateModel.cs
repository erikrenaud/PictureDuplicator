using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Windows.Threading;

namespace PictureDuplicator
{
    public class StatusUpdateModel : INotifyPropertyChanged
    {
        private int totalSearchedFiles = 0;
        private int totalSynchedFiles = 0;
        private int totalFilesFound = 0;
        private int filesProcessed = 0;
        private string message = "Searching for files...";
        private string errors = string.Empty;

        public string Errors { get { return errors; } set { errors = value; OnPropertyChanged("Errors"); } }

        public int TotalSearchedFiles
        {
            get { return totalSearchedFiles; }
            set { totalSearchedFiles = value; OnPropertyChanged("TotalSearchedFiles"); }
        }

        public int TotalSynchedFiles
        {
            get { return totalSynchedFiles; }
            set { totalSynchedFiles = value; OnPropertyChanged("TotalSynchedFiles"); }
        }

        public int TotalFilesFound
        {
            get { return totalFilesFound; }
            set { totalFilesFound = value; OnPropertyChanged("TotalFilesFound"); OnPropertyChanged("FilesRemaining"); }
        }

        public int FilesProcessed
        {
            get { return filesProcessed; }
            set { filesProcessed = value; OnPropertyChanged("FilesProcessed"); OnPropertyChanged("FilesRemaining"); }
        }

        public string FilesRemaining
        {
            get { return String.Format("{0} / {1}", filesProcessed, totalFilesFound); }
        }

        public string Message
        {
            get { return message; }
            set { message = value; OnPropertyChanged("Message"); }
        }



        private void OnPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                Dispatcher.CurrentDispatcher.Invoke(new Action<string>(SafeOnPropertyChanged), property);
            }
        }


        private void SafeOnPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
