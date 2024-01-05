using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PictureDuplicator.Properties;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;


namespace PictureDuplicator
{
    public class Worker
    {
        public class WorkFile
        {
            public string FilePath { get; set; }
            public int Rating { get; set; }

            public WorkFile()
            {
                Rating = 0;
            }

            public FileInfo FileInfo
            {
                get
                {
                    return new FileInfo(FilePath);
                }
            }

            public FileInfo FileInfoRated
            {
                get
                {
                    var newPath = FlattenPath(Settings.Default.RatedPath, FilePath);
                    if (!newPath.ToLower().EndsWith(".jpg"))
                    {
                        newPath = newPath.Replace(new FileInfo(newPath).Extension, ".jpg");
                    }
                    var fi = new FileInfo(newPath);
                    return fi;
                }
            }

            private string FlattenPath(string basePath, string FilePath)
            {
                var s = this.FileInfo.FullName;
                s = s.Replace(Settings.Default.PicturePath, string.Empty);
                //s = s.Replace('\\', '_');

                return basePath + s;
            }

            public bool IsValidExtension
            {
                get
                {
                    return (this.FileInfo.Extension.ToLower() == ".jpg" || this.FileInfo.Extension.ToLower() == ".jpeg" || this.FileInfo.Extension.ToLower() == ".png" || this.FileInfo.Extension.ToLower() == ".heic");
                }
            }

            public bool FileTypeContainsMetadata
            {
                get
                {
                    return (this.FileInfo.Extension.ToLower() == ".jpg" || this.FileInfo.Extension.ToLower() == ".jpeg");
                }
            }
        }

        public Worker()
        {
            Data = new StatusUpdateModel();
            FilesToProcess = new Stack<WorkFile>(100000);
        }

        public StatusUpdateModel Data { get; set; }
        private Stack<WorkFile> FilesToProcess { get; set; }
        private bool exploringFiles = true;
        private bool isRunning = true;
        Thread fileExplorerThread;
        Thread fileProcessorThread;

        public void Start()
        {
            Directory.CreateDirectory(Settings.Default.RatedPath);

            fileExplorerThread = new Thread((start) => ExploreFiles());
            fileProcessorThread = new Thread((start) => ProcessFiles());

            fileExplorerThread.Start();
            fileProcessorThread.Start();
        }

        public void Stop()
        {
            isRunning = false;

            for (int time = 0; time < 5000; time += 150)
            {
                System.Threading.Thread.Sleep(150);
                if (!fileExplorerThread.IsAlive && !fileProcessorThread.IsAlive)
                    break;
            }

            if (fileExplorerThread.IsAlive) fileExplorerThread.Join();
            if (fileProcessorThread.IsAlive) fileProcessorThread.Join();
        }

        private void ExploreFiles()
        {
            try
            {
                ExploreFiles(Settings.Default.PicturePath);
                exploringFiles = false;
            }
            catch (Exception e)
            {
                isRunning = false;
                Data.Errors += "ExploreFilesError : " + e.Message + System.Environment.NewLine + e.StackTrace + System.Environment.NewLine;
            }
        }

        private void ExploreFiles(string directory)
        {
            if ((new DirectoryInfo(directory).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                return; //skip hidden directories

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (!isRunning)
                    return;

                var workFile = GetWorkFile(file);
                if (workFile != null)
                {
                    lock (this) FilesToProcess.Push(workFile);
                    Data.TotalFilesFound++;
                }
                Data.TotalSearchedFiles++;
            }

            foreach (var innerDirectory in Directory.EnumerateDirectories(directory))
            {
                ExploreFiles(innerDirectory);
            }
        }

        private WorkFile GetWorkFile(string file)
        {
            WorkFile workFile = new WorkFile() { FilePath = file };

            if (!workFile.IsValidExtension)
                return null;

            if (workFile.FileInfo.DirectoryName.ToLower().StartsWith(Settings.Default.RatedPath.ToLower()))
                return null;

            if (workFile.FileTypeContainsMetadata)
            {
                using (var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BitmapDecoder decoder = null;
                    BitmapMetadata metadata = null;
                    int msRating = 0;
                    int acdseeRating = 0;
                    try
                    {
                        decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                        if ((decoder.Frames[0] == null) || (decoder.Frames[0].Metadata == null))
                        {
                            return null;
                        }
                        metadata = decoder.Frames[0].Metadata as BitmapMetadata;
                        msRating = metadata.Rating;
                        try
                        {
                            acdseeRating = int.Parse(metadata.GetQuery(@"/xmp/http\:\/\/ns.acdsee.com\/iptc\/1.0\/:rating").ToString());
                        }
                        catch { }
                    }
                    catch (Exception e)
                    {
                        Data.Errors += file + " Couldn't read extract metadata. " + e.Message + System.Environment.NewLine;
                        return null;
                    }

                    string synchedFile = null;
                    try
                    {
                        if (msRating != acdseeRating)
                        {
                            JpegBitmapEncoder output = new JpegBitmapEncoder();
                            var metaDataClone = metadata.Clone() as BitmapMetadata;
                            int highestRating = msRating > acdseeRating ? msRating : acdseeRating;
                            metaDataClone.Rating = highestRating;
                            metaDataClone.SetQuery(@"/xmp/http\:\/\/ns.acdsee.com\/iptc\/1.0\/:rating", highestRating.ToString());
                            output.Frames.Add(BitmapFrame.Create(decoder.Frames[0], decoder.Frames[0].Thumbnail, metaDataClone, decoder.Frames[0].ColorContexts));
                            synchedFile = file + "XXX";

                            using (Stream outputFile = File.Open(synchedFile, FileMode.Create, FileAccess.ReadWrite))
                            {
                                output.Save(outputFile);
                                outputFile.Close();
                            }
                            Data.TotalSynchedFiles++;
                        }
                    }
                    catch (Exception e)
                    {
                        Data.Errors += file + " Couldn't sync file after sync. " + e.Message + System.Environment.NewLine;
                    }

                    fs.Close();
                    try
                    {
                        if (synchedFile != null)
                        {
                            File.Delete(file);
                            File.Move(synchedFile, file);
                        }
                    }
                    catch (Exception e)
                    {
                        Data.Errors += file + " Couldn't delete-move file after sync. " + e.Message + System.Environment.NewLine;
                    }

                    workFile.Rating = msRating > acdseeRating ? msRating : acdseeRating;
                }
            }

            //set rating from filename
            var filename = Path.GetFileNameWithoutExtension(workFile.FileInfo.Name.ToLower());
            {
                if (filename.EndsWith("r1")) workFile.Rating = 1;
                else if (filename.EndsWith("r2")) workFile.Rating = 2;
                else if (filename.EndsWith("r3")) workFile.Rating = 3;
                else if (filename.EndsWith("r4")) workFile.Rating = 4;
                else if (filename.EndsWith("r5")) workFile.Rating = 5;
            }

            if (workFile.FileInfoRated.Exists && workFile.Rating == 0)
                workFile.FileInfoRated.Delete();

            if (workFile.Rating > 0)
                return workFile;
            return null;
        }

        private void ProcessFiles()
        {
            try
            {
                List<WorkFile> processedFiles = new List<WorkFile>();

                int count;
                lock (this) count = FilesToProcess.Count;

                while (exploringFiles || count > 0)
                {
                    if (!isRunning)
                        return;

                    WorkFile file = null;
                    lock (this)
                    {
                        if (FilesToProcess.Count > 0)
                            file = FilesToProcess.Pop();
                    }

                    if (file != null)
                    {
                        Data.Message = file.FilePath;
                        ManageRating(file);
                        processedFiles.Add(file);
                        Data.Message = string.Empty;
                        Data.FilesProcessed++;
                    }

                    lock (this) count = FilesToProcess.Count;
                    if (count == 0) Thread.Sleep(50);
                }

                Data.Message = "Finished";
            }
            catch (Exception e)
            {
                isRunning = false;
                Data.Errors += "ProcessFilesError : " + e.Message + System.Environment.NewLine + e.StackTrace + System.Environment.NewLine;
            }
        }

        private void ManageRating(WorkFile file)
        {
            try
            {
            BitmapDecoder photoDecoder;
            using (var fs = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                photoDecoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

                if (file.Rating > 0 && !file.FileInfoRated.Exists)
                {
                    file.FileInfoRated.Directory.Create();
                    var photo = photoDecoder.Frames[0];
                    var resizedImage = Resize(photo,
                        (int)(FindScaleFactor(photo.PixelWidth, photo.PixelHeight, 1920, 1080) * photo.PixelWidth),
                        (int)(FindScaleFactor(photo.PixelWidth, photo.PixelHeight, 1920, 1080) * photo.PixelHeight));
                    var photoEncoder = new JpegBitmapEncoder();
                    photoEncoder.Frames.Add(resizedImage);
                    using (var fss = new FileStream(file.FileInfoRated.FullName, FileMode.CreateNew, FileAccess.Write))
                    {
                        photoEncoder.Save(fss);
                    }
                }

                fs.Close();
            }
            }
            catch (Exception e)
            {
                Data.Errors += file.FilePath + "ProcessFileError : " + e.Message + System.Environment.NewLine + e.StackTrace + System.Environment.NewLine;
            }
        }

        public BitmapFrame Resize(BitmapFrame photo, int width, int height)
        {
            var group = new DrawingGroup();
            RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.Fant);
            group.Children.Add(new ImageDrawing(photo, new Rect(0, 0, width, height)));
            var targetVisual = new DrawingVisual();
            var targetContext = targetVisual.RenderOpen();
            targetContext.DrawDrawing(group);
            var target = new RenderTargetBitmap(
                width, height, 96, 96, PixelFormats.Default);
            targetContext.Close();
            target.Render(targetVisual);
            var targetFrame = BitmapFrame.Create(target, photo.Thumbnail, photo.Metadata.Clone() as BitmapMetadata, photo.ColorContexts);
            return targetFrame;
        }

        private double FindScaleFactor(double originalX, double originalY, double maxX, double maxY)
        {
            if (originalX < originalY)
            {
                double tmp = maxX;
                maxX = maxY;
                maxY = tmp;
            }

            if (originalX <= maxX && originalY <= maxY)
            {
                // don't shrink
                return 1.0;
            }
            else
            {
                // resize keeping aspect ratio the same
                if ((maxX / maxY) > (originalX / originalY))
                {
                    // height is our constraining property
                    return maxY / originalY;
                }
                else
                {
                    // either width is our constraining property, or the user
                    // managed to nail our aspect ratio perfectly.
                    return maxX / originalX;
                }
            }
            throw new InvalidOperationException();
        }
    }
}
