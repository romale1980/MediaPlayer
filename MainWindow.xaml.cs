using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CoreAudioApi;
using Microsoft.Win32;
using System.Windows.Media.Animation;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MediaPlayer
{
    public partial class MainWindow : Window
    {
        private MMDevice device;
        bool progress_bl = false;
        List<PlayList> list = new List<PlayList>();
        List<PlayList> list_pl_content = new List<PlayList>();
        List<PlayList> list_run = new List<PlayList>();
        int counter = 0;
        string listname = "";
        string listtype = "";
        int numlistelem = -1;
        SaveProg save = new SaveProg();
        string[] args;
        string argspath=null;
        string DIR = null;
        string USERDIR = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

        public MainWindow()
        {
            args = Environment.GetCommandLineArgs();
            argspath = args[args.Length - 1];
            Process[] ps1 = System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName);
            Process currentProcess = Process.GetCurrentProcess();
            foreach (Process p1 in ps1)
            {
                if (p1.Id != currentProcess.Id)
                {
                    Helper.SendWindowsStringMessage(p1.MainWindowHandle, argspath);
                    System.Environment.Exit(0);
                }
            }
            InitializeComponent();
            DIR = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            device = DevEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
            volum.Value = (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            device.AudioEndpointVolume.OnVolumeNotification += new AudioEndpointVolumeNotificationDelegate(AudioEndpointVolume_OnVolumeNotification);
            new DispatcherTimer() { IsEnabled = true, Interval = TimeSpan.FromMilliseconds(100) }.Tick += delegate(object sender, EventArgs e)
            {
                try
                {
                    int h = (int)(media.NaturalDuration.TimeSpan.TotalSeconds - media.Position.TotalSeconds) / 3600;
                    int m = (int)(media.NaturalDuration.TimeSpan.TotalSeconds - media.Position.TotalSeconds) / 60 - h * 60;
                    int s = (int)(media.NaturalDuration.TimeSpan.TotalSeconds - media.Position.TotalSeconds) - m * 60 - h * 3600;
                    if(s >= 0) timeleft.Content = (h < 10 ? "0" : "") + h.ToString() + ":" + (m < 10 ? "0" : "") +
                          m.ToString() + ":" + (s < 10 ? "0" : "") + s.ToString();
                    h = (int)media.Position.TotalSeconds / 3600;
                    m = (int)media.Position.TotalSeconds / 60 - h * 60;
                    s = (int)media.Position.TotalSeconds - m * 60 - h * 3600;
                    if (s >= 0) time.Content =
                          (h < 10 ? "0" : "") + h.ToString() + ":" + (m < 10 ? "0" : "") +
                          m.ToString() + ":" + (s < 10 ? "0" : "") + s.ToString();
                    progress.Value = media.Position.TotalSeconds / media.NaturalDuration.TimeSpan.TotalSeconds * progress.Maximum;
                    slider.Value = media.Position.TotalSeconds / media.NaturalDuration.TimeSpan.TotalSeconds * slider.Maximum;
                }
                catch (Exception)
                {
                }
            };
            if (!Directory.Exists(USERDIR + "\\MediaPlayer\\Playlists\\Audio"))
                Directory.CreateDirectory(USERDIR + "\\MediaPlayer\\Playlists\\Audio");
            if (!Directory.Exists(USERDIR + "\\MediaPlayer\\Playlists\\Video"))
                Directory.CreateDirectory(USERDIR + "\\MediaPlayer\\Playlists\\Video");
            if (!Directory.Exists(USERDIR + "\\MediaPlayer\\Playlists\\Image"))
                Directory.CreateDirectory(USERDIR + "\\MediaPlayer\\Playlists\\Image");
            if (!Directory.Exists(USERDIR + "\\MediaPlayer\\Playlists\\All"))
                Directory.CreateDirectory(USERDIR + "\\MediaPlayer\\Playlists\\All");
            LoadProg();
            
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Helper.WM_COPYDATA)
            {
                Helper.DATASTRUCT data = (Helper.DATASTRUCT)Marshal.PtrToStructure(lParam, typeof(Helper.DATASTRUCT));
                media.Source = new Uri(data.s);
                media.Play();
                WindowState st = this.WindowState;
                this.WindowState = WindowState.Minimized;
                if (st != WindowState.Minimized) this.WindowState = st;
                else
                    if (st == WindowState.Minimized) this.WindowState = WindowState.Normal;
            }
            return IntPtr.Zero;
        }


        private void volum_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            device.AudioEndpointVolume.MasterVolumeLevelScalar = ((float)volum.Value / 100.0f);
        }

        void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            volum.Value = (int)(data.MasterVolume * 100);
        }

        private void pause_Click(object sender, RoutedEventArgs e)
        {
            if (media.Source != null)
            {
                if (media.Position != new TimeSpan(0)) media.Pause();
                progress_bl = false;
                playstate.Visibility = Visibility.Hidden;
                pausestate.Visibility = Visibility.Visible;
            }
            EnabledCurrentFile();
        }

        private void play_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (playlistSel.SelectedItem != null && media.Source == null &&
                    File.Exists(((PlayList)playlistSel.SelectedItem).longname))
                {
                    //if (media.Source == null && progress_bl)
                   // {
                        media.Source = new Uri(((PlayList)playlistSel.SelectedItem).longname.ToString());
                   // }
                    listname = ((PlayList)listPlay.SelectedItem).longname;
                    numlistelem = playlistSel.SelectedIndex;
                    listtype = GetDirListName();
                    list_run.Clear();
                    list_run = list_pl_content.GetRange(0, list_pl_content.Count);
                }
                if (media.Source != null)
                {
                    media.Play();
                    progress_bl = true;
                    playstate.Visibility = Visibility.Visible;
                    pausestate.Visibility = Visibility.Hidden;
                }
            }
            catch (Exception) { stop_Click(sender, e); time.Content = "00:00:00"; }
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            titleplay.Text = "";
            media.Stop();
            media.Source = null;
            slider.Value = 0;
            progress.Value = 0;
            playstate.Visibility = Visibility.Hidden;
            pausestate.Visibility = Visibility.Hidden;
            EnabledCurrentFile();
            timeleft.Content = "00:00:00";
            if(tags.Visibility == Visibility.Visible)
                tags.Visibility = Visibility.Hidden;
        }

        private void progress_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                media.Position = TimeSpan.FromSeconds(Mouse.GetPosition(progress).X / progress.ActualWidth * media.NaturalDuration.TimeSpan.TotalSeconds);
            }
            catch(Exception) { }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void min_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void max_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                save.winWidth = win.ActualWidth;
                save.winHeight = win.ActualHeight;
                this.WindowState = WindowState.Maximized;
            }
            else
                if (this.WindowState == WindowState.Maximized)
                    this.WindowState = WindowState.Normal;
        }

        private void close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (slider.IsMouseCaptureWithin == true)
            {
                media.Volume = 0;
                try
                {
                    if (progress_bl && pause.Visibility == Visibility.Visible) media.Pause();
                    if (!progress_bl && playstate.Visibility == Visibility.Visible) media.Play();
                    progress.Value = slider.Value;
                    media.Position = TimeSpan.FromSeconds(progress.Value / progress.Maximum * media.NaturalDuration.TimeSpan.TotalSeconds);
                    if (progress_bl && playstate.Visibility == Visibility.Visible) media.Play();
                    if (!progress_bl && pause.Visibility == Visibility.Visible) media.Pause();
                }
                catch (Exception) { }
            }
            else media.Volume = 1;
        }

        private void SetMedia(string str)
        {
            media.Source = new Uri(str);
        }

        private void Playlist_Click(object sender, RoutedEventArgs e)
        {
            if (pllist.Width > 0 && pllistContent.Width > 0)
            {
                Anime_Open_Playlist(new DoubleAnimation(), 0.5);
                listPlay.SelectedIndex = -1;
            }
            OpenClosePl(sender, e);    
        }

        private void OpenClosePl(object sender, RoutedEventArgs e)
        {
            pllist.Visibility = System.Windows.Visibility.Visible;
            DoubleAnimation da = new DoubleAnimation();
            if (pllist.Width != 0 && createBord.RenderTransform.Value.M11 == 1) create_Click(sender, e);
            da.From = pllist.Width == 0 ? 0 : pllist.ActualWidth;
            da.To = pllist.Width == 0 ? 200 : 0;
            da.Duration = new Duration(TimeSpan.FromSeconds(0.5));
            pllist.BeginAnimation(Label.WidthProperty, da);
            if (numlistelem > -1)
                if (media.Source != null && pllist.Width == 0 &&
                    media.Source.LocalPath == list_run[numlistelem].longname)
                {
                    SetDirListName(listtype);
                    foreach (PlayList item in list)
                    {
                        if (item.longname == listname)
                        {
                            listPlay.SelectedIndex = list.IndexOf(item);
                            break;
                        }
                    }
                }
        }

        private void create_Click(object sender, RoutedEventArgs e)
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimation da = new DoubleAnimation();
            da.Duration = TimeSpan.FromMilliseconds(200);
            da.From = createBord.RenderTransform.Value.M11 == 1 ? 1 : 0;
            da.To = createBord.RenderTransform.Value.M11 == 1 ? 0 : 1;
            storyboard.Children.Add(da);
            Storyboard.SetTargetProperty(da, new PropertyPath("RenderTransform.ScaleX"));
            Storyboard.SetTarget(da, createBord);
            storyboard.Begin();
            if (createBord.RenderTransform.Value.M11 == 0) newList.Text = "";
        }

        private string GetDirListName()
        {
            if (rb_all.IsChecked == true) return "All";
            else if (rb_audio.IsChecked == true) return "Audio";
            else if (rb_video.IsChecked == true) return "Video";
            else  return "Image";
        }

        private void SetDirListName(string str)
        {
            if (str == "All") rb_all.IsChecked = true;
            else if (str == "Audio") rb_audio.IsChecked = true;
            else if (str == "Video") rb_video.IsChecked = true;
            else rb_img.IsChecked = true;
        }

        private void ok_Click(object sender, RoutedEventArgs e)
        {
            string str, dir_str = null;
            if (newList.Text == "") str = "Плейлист";
            else str = newList.Text;
            newList.Text = "";
            dir_str = GetDirListName();
            if (File.Exists(USERDIR + "\\MediaPlayer\\Playlists\\" + dir_str + "\\" + str + ".dat"))
            {
                int i = 1;
                while (true)
                {
                    if (!File.Exists(USERDIR + "\\MediaPlayer\\Playlists\\" + dir_str + "\\" + str + "(" + Convert.ToString(i) + ").dat"))
                    {
                        str = str + "(" + i.ToString() + ")";
                        break;
                    }
                    i++;
                }
            }
            List<PlayList> temp = new List<PlayList>();
            XmlSerializer xml = new XmlSerializer(typeof(List<PlayList>));
            FileStream fs = new FileStream(USERDIR + "\\MediaPlayer\\Playlists\\" + dir_str + "\\" + str + ".dat"
                , FileMode.OpenOrCreate, FileAccess.ReadWrite);
            xml.Serialize(fs, temp);
            fs.Close();
            fs = null;
            xml = null;
            GC.Collect();
            if (File.Exists(USERDIR + "\\MediaPlayer\\Playlists\\" + dir_str + "\\" + str + ".dat"))
            {
                int num = listPlay.SelectedIndex;
                create_Click(sender, e);
                list.Add(new PlayList() { name = str,
                                          longname = USERDIR + "\\MediaPlayer\\Playlists\\" + dir_str + "\\" + str + ".dat"
                });
                listPlay.ItemsSource = null;
                listPlay.Items.Remove(listPlay.SelectedItem);
                listPlay.ItemsSource = list;
                listPlay.ScrollIntoView(listPlay.Items[listPlay.Items.Count - 1]);
                listPlay.SelectedIndex = num;
            }
        }

        private void listPlay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            double dur = 0.5;
            pllistContent.Visibility = System.Windows.Visibility.Visible;
            DoubleAnimation da = new DoubleAnimation();
            if (pllistContent.Width != 0 && listPlay.SelectedItem != null)
            {
                da.AutoReverse = true;
                dur = 0.3;
            }
            Anime_Open_Playlist(da, dur);
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(150);
            timer.Tick += new EventHandler(SetListPlaylist);
            counter = 0;
            timer.Start();
            EnabledCurrentFile();
        }

        private void EnabledCurrentFile()
        {
            DoubleAnimation da = new DoubleAnimation();
            int from, to;
            if ((media.HasVideo == true && media.HasAudio == true && rb_video.IsChecked == true ||
               media.HasVideo == false && media.HasAudio == true && rb_audio.IsChecked == true ||
                media.HasAudio == false && media.HasVideo == true && rb_img.IsChecked == true ||
                rb_all.IsChecked == true) && (media.Source != null &&
                (playstate.Visibility == Visibility.Visible || pause.Visibility == Visibility.Visible)))
            {
                from = 0;
                to = 22;
            }
            else
            {
                from = 22;
                to = 0;
            }
            da.From = from;
            da.To = to;
            da.Duration = new Duration(TimeSpan.FromSeconds(0.2));
            if(from == cur_file.Height) cur_file.BeginAnimation(Button.HeightProperty, da);
        }

        public void SetListPlaylist(object sender, EventArgs e)
        {
            if (counter == 1)
            {
                SetViewPl();
                ((DispatcherTimer)sender).Stop();
            }
            counter++;
        }

        private void Anime_Open_Playlist(DoubleAnimation da, double dur)
        {
            da.From = pllistContent.Width == 0 ? 0 : 240;
            da.To = pllistContent.Width == 0 ? 240 : 0;
            da.Duration = new Duration(TimeSpan.FromSeconds(dur));
            pllistContent.BeginAnimation(Label.WidthProperty, da);
        }

        private void SetViewPl()
        {
            list_pl_content.Clear();
            FileStream fs = null;
            XmlSerializer xml;
            try
            {
                fs = new FileStream(USERDIR + "\\MediaPlayer\\Playlists\\" +
                GetDirListName() + "\\" + ((PlayList)listPlay.SelectedItem).name + ".dat"
                , FileMode.Open, FileAccess.ReadWrite);
                xml = new XmlSerializer(typeof(List<PlayList>));
                list_pl_content = (List<PlayList>)xml.Deserialize(fs);
                fs.Close();
                playlistSel.ItemsSource = null;
                playlistSel.Items.Remove(playlistSel.SelectedItem);
                playlistSel.ItemsSource = list_pl_content;
                if (listname == ((PlayList)listPlay.SelectedItem).longname &&
                    listtype == GetDirListName())
                {
                    playlistSel.SelectedIndex = numlistelem;
                    playlistSel.ScrollIntoView(playlistSel.Items[playlistSel.SelectedIndex]);
                }
            }
            catch (Exception) { }
            fs = null;
            xml = null;
            GC.Collect();
        }

        private void rb_Checked(object sender, RoutedEventArgs e)
        {
            if (win.IsLoaded == true)
            {
                GetPl(list, listPlay);
                Save();
            }
        }

        private void GetPl(List<PlayList> l, ListView lP)
        {
            l.Clear();
            foreach (var item in Directory.GetFiles(USERDIR + "\\MediaPlayer\\Playlists\\" + GetDirListName()))
                l.Add(new PlayList() { name = System.IO.Path.GetFileNameWithoutExtension(item),
                                       longname = USERDIR + "\\MediaPlayer\\Playlists\\" + 
                                       GetDirListName() + "\\" + System.IO.Path.GetFileName(item)
                });
            lP.ItemsSource = null;
            lP.Items.Remove(lP.SelectedItem);
            lP.ItemsSource = l;
        }

        private void add_Click(object sender, RoutedEventArgs e)
        {
            string video = "*.wmv; *.3g2; *.3gp; *.3gp2; *.3gpp; *.amv; *.asf;  *.avi;" +
                " *.cue; *.divx; *.dv; *.flv; *.gxf; *.m1v; *.m2v; *.m2t; *.m2ts; *.m4v; " +
                " *.mkv; *.mov; *.mp2; *.mp2v; *.mp4; *.mp4v; *.mpa; *.mpe; *.mpeg; *.mpeg1; *.mpeg2;" +
                "*.mpeg4; *.mpg; *.mpv2; *.mts; *.nsv; *.nuv; *.ogg; *.ogm; *.ogv; *.ogx; *.ps; *.rec;" +
                "*.rm; *.rmvb; *.tod; *.ts; *.tts; *.vob; *.vro; *.webm;";
            string audio = "*.aa; *.ac3; *.aac; *.adx; *.asf; *.ahx; *.aiff;" +
                "*.ape; *.aud; *.dmf; *.dts; *.flac; *.midi; *.mod; *.mp1; *.mp2; *.mp3; *.mpc; *.ogg;  " +
                "*.tta; *.vof; *.vox; *.voc;*.wav; *.wma; ";
            string images = "*.jpg; *.jpeg; *.gif; *.jpe; *.jfif; *.tif;";
            OpenFileDialog dlg = new OpenFileDialog();
            if (rb_video.IsChecked == true) dlg.Filter = "Video files | " + video;
            else if (rb_img.IsChecked == true) dlg.Filter = "Audio files | " + images;
            else if (rb_audio.IsChecked == true) dlg.Filter = "Img files | " + audio;
            else dlg.Filter = "All |" + video + audio + images;
            dlg.Multiselect = true;
            dlg.ShowDialog();
            string[] strmas = dlg.FileNames;
            foreach (var str in strmas)
            if (File.Exists(str))
            {
                int num = playlistSel.SelectedIndex;
                list_pl_content.Add(new PlayList() { name = System.IO.Path.GetFileName(str), longname = str });
                playlistSel.ItemsSource = null;
                playlistSel.Items.Remove(playlistSel.SelectedItem);
                playlistSel.ItemsSource = list_pl_content;
                playlistSel.ScrollIntoView(playlistSel.Items[playlistSel.Items.Count - 1]);
                Elem_Add_Del();
                playlistSel.SelectedIndex = num;
            }
            GC.Collect();
        }

        private void del_elem_Click(object sender, RoutedEventArgs e)
        {
            if (playlistSel.SelectedItem != null)
            {
                foreach (PlayList item in playlistSel.SelectedItems)
                    list_pl_content.Remove(item);
                playlistSel.ItemsSource = null;
                playlistSel.Items.Remove(playlistSel.SelectedItem);
                playlistSel.ItemsSource = list_pl_content;
                Elem_Add_Del();
            }
        }

        private void Elem_Add_Del()
        {
            //int temp = list_run.Count;
            XmlSerializer xml = new XmlSerializer(typeof(List<PlayList>));
            FileStream fs = new FileStream(USERDIR + "\\MediaPlayer\\Playlists\\" +
                GetDirListName() + "\\" + ((PlayList)listPlay.SelectedItem).name + ".dat"
                , FileMode.Create, FileAccess.ReadWrite);
            xml.Serialize(fs, list_pl_content);
            fs.Close();
            fs = null;
            xml = null;
            list_run.Clear();
            list_run = list_pl_content.GetRange(0, list_pl_content.Count);
            //if(temp > list_run.Count) numlistelem--;
        }

        private void playlistSel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (playlistSel.SelectedItem != null && media.Source == null)
            {
                listname = ((PlayList)listPlay.SelectedItem).longname;
                numlistelem = playlistSel.SelectedIndex;
                listtype = GetDirListName();
                list_run.Clear();
                list_run = list_pl_content.GetRange(0, list_pl_content.Count);
            }
        }

        private void pllistContent_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (playlistSel.SelectedItem != null && File.Exists(((PlayList)playlistSel.SelectedItem).longname))
            {
                media.Source = null;
                media.Source = new Uri(((PlayList)playlistSel.SelectedItem).longname.ToString());
                media.Play();
                playstate.Visibility = Visibility.Visible;
                EnabledCurrentFile();
                listname = ((PlayList)listPlay.SelectedItem).longname;
                numlistelem = playlistSel.SelectedIndex;
                listtype = GetDirListName();
                list_run.Clear();
                list_run = list_pl_content.GetRange(0, list_pl_content.Count);
            }
        }

        private void media_MediaEnded(object sender, RoutedEventArgs e)
        {
            titleplay.Text = "";
            if (list_run.Count - 1 >= numlistelem && File.Exists(listname))
            {
                if (numlistelem == list_run.Count - 1) numlistelem = 0;
                else
                    numlistelem++;
                if (listPlay.SelectedIndex != -1)
                    if (listname == ((PlayList)listPlay.SelectedItem).longname && listtype == GetDirListName())
                    {
                        playlistSel.SelectedIndex = numlistelem;
                        playlistSel.ScrollIntoView(playlistSel.Items[numlistelem]);
                    }
                if (File.Exists(list_run[numlistelem].longname))
                    media.Source = new Uri(list_run[numlistelem].longname);
                else media_MediaEnded(sender, e);
            }
            else
            { 
                stop_Click(sender, e);
            }
            EnabledCurrentFile();
        }

        private void media_MediaOpened(object sender, RoutedEventArgs e)
        {
            titleplay.Text = System.IO.Path.GetFileName(media.Source.LocalPath);
            if (media.HasAudio && !media.HasVideo)
            {
                var mp3File = TagLib.File.Create(media.Source.LocalPath);
                artist.Text = mp3File.Tag.JoinedPerformers;
                title.Text = mp3File.Tag.Title;
                album.Text = mp3File.Tag.Album;
                genre.Text = mp3File.Tag.FirstGenre;
                if (mp3File.Tag.Year != 0) year.Text = mp3File.Tag.Year.ToString();
                else year.Text = "";
                if (mp3File.Tag.Pictures.Length >= 1)
                {
                    MemoryStream ms = new MemoryStream(mp3File.Tag.Pictures[0].Data.Data);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.DecodePixelWidth = 500;
                    bi.DecodePixelHeight = 500;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    imgTag.Source = bi;
                    bi = null;
                    Storyboard s = new Storyboard();
                    DoubleAnimation animation = new DoubleAnimation();
                    animation.From = 0;
                    animation.To = 0;
                    animation.Duration = new Duration(TimeSpan.FromSeconds(1));
                    s.Children.Add(animation);
                    Storyboard.SetTarget(animation, imgTag);
                    Storyboard.SetTargetProperty(animation, new PropertyPath("(Rectangle.RenderTransform).(RotateTransform.Angle)"));
                    s.Begin();
                    Storyboard s1 = new Storyboard();
                    DoubleAnimation animation1 = new DoubleAnimation();
                    animation1.From = 315;
                    animation1.To = 315;
                    animation1.Duration = new Duration(TimeSpan.FromSeconds(60));
                    s1.Children.Add(animation1);
                    Storyboard.SetTarget(animation1, imgTag);
                    Storyboard.SetTargetProperty(animation1, new PropertyPath("Effect.(DropShadowEffect.Direction)"));
                    s1.Begin();
                }
                else
                {
                    imgTag.Source = (BitmapImage) Application.Current.Resources["CD"];
                    DropShadowEffect dropShadow = new DropShadowEffect();
                    dropShadow.Direction=315;
                    imgTag.Effect = dropShadow;
                    Storyboard s = new Storyboard();
                    RotateTransform rotation = imgTag.RenderTransform as RotateTransform;
                    DoubleAnimation animation = new DoubleAnimation();
                    animation.From = rotation.Angle;
                    animation.To = rotation.Angle + 360;
                    animation.Duration = new Duration(TimeSpan.FromSeconds(60));
                    animation.RepeatBehavior = RepeatBehavior.Forever;
                    s.Children.Add(animation);
                    Storyboard.SetTarget(animation, imgTag);
                    Storyboard.SetTargetProperty(animation, new PropertyPath("(Rectangle.RenderTransform).(RotateTransform.Angle)"));
                    s.Begin();
                    Storyboard s1 = new Storyboard();
                    DoubleAnimation animation1 = new DoubleAnimation();
                    animation1.From = rotation.Angle + 315;
                    animation1.To = rotation.Angle + 315 + 360;
                    animation1.Duration = new Duration(TimeSpan.FromSeconds(60));
                    animation1.RepeatBehavior = RepeatBehavior.Forever;
                    s1.Children.Add(animation1);
                    Storyboard.SetTarget(animation1, imgTag);
                    Storyboard.SetTargetProperty(animation1, new PropertyPath("Effect.(DropShadowEffect.Direction)"));
                    s1.Begin();
                }
                tags.Visibility = Visibility.Visible;
                mp3File = null;
            }
            else tags.Visibility = Visibility.Hidden;
            EnabledCurrentFile();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DoubleAnimation da = new DoubleAnimation();
            Anime_Open_Playlist(da, 0.5);
            listPlay.SelectedIndex = -1;  
        }

        private void cur_file_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(((PlayList)listPlay.SelectedItems[0]).longname))
            {
                int num = playlistSel.SelectedIndex;
                list_pl_content.Add(new PlayList()
                {
                    name = System.IO.Path.GetFileName(media.Source.LocalPath),
                    longname = media.Source.LocalPath });
                playlistSel.ItemsSource = null;
                playlistSel.Items.Remove(playlistSel.SelectedItem);
                playlistSel.ItemsSource = list_pl_content;
                playlistSel.ScrollIntoView(playlistSel.Items[playlistSel.Items.Count - 1]);
                playlistSel.SelectedIndex = num;
                Elem_Add_Del();
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            foreach (PlayList item in listPlay.SelectedItems)
            {
                File.Delete(item.longname);
                list.Remove(item);
            }
            listPlay.ItemsSource = null;
            listPlay.ItemsSource = list;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            foreach (PlayList item in list)
                File.Delete(item.longname);
            list.Clear();
            listPlay.ItemsSource = null;
            listPlay.ItemsSource = list;
        }

        private void left_Click(object sender, RoutedEventArgs e)
        {
            if (media.Source != null)
            {
                if (numlistelem >= 0 && File.Exists(listname))
                {
                    if (numlistelem == 0) numlistelem = list_run.Count - 1;
                    else  numlistelem--;
                    if (listPlay.SelectedIndex != -1)
                        if (listname == ((PlayList)listPlay.SelectedItem).longname && listtype == GetDirListName())
                        {
                            playlistSel.SelectedIndex = numlistelem;
                            playlistSel.ScrollIntoView(playlistSel.Items[numlistelem]);
                        }
                    if (File.Exists(((PlayList)playlistSel.SelectedItem).longname))
                    {
                        media.Source = new Uri(list_run[numlistelem].longname);
                        titleplay.Text = System.IO.Path.GetFileName(media.Source.LocalPath);
                    }
                    else left_Click(sender, e);
                }
                else
                {
                    stop_Click(sender, e);
                }
            }
        }

        private void right_Click(object sender, RoutedEventArgs e)
        {
            if (media.Source != null) media_MediaEnded(sender, e);
        }

        public static void SelectStyle(string styleNo)
        {     
            List<ResourceDictionary> dictionaryList = new List<ResourceDictionary>();
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {
                dictionaryList.Add(dictionary);
            }   
            string requestedCulture = string.Format("Style{0}.xaml", styleNo);
            ResourceDictionary resourceDictionary = dictionaryList.FirstOrDefault(d => d.Source.OriginalString == requestedCulture);
            if (resourceDictionary == null)
            {    
                requestedCulture = "StringResources.xaml";
                resourceDictionary = dictionaryList.FirstOrDefault(d => d.Source.OriginalString == requestedCulture);
            }   
            if (resourceDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(resourceDictionary);
                Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
            }
        }

        private void gray_Click(object sender, RoutedEventArgs e)
        {
            SelectStyle(((MenuItem)sender).Tag.ToString());
            save.skin = ((MenuItem)sender).Tag.ToString();
            Save();
        }

        private void openfile_Click(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.ShowDialog();
            string str = dlg.FileName;
            try
            {
                if (str != "")
                {
                    media.Stop();
                    SetMedia(str);
                    media.Play();
                    playstate.Visibility = Visibility.Visible;
                }
            }
            catch (Exception) { }
            dlg = null;
            GC.Collect();
        }

        private void openlists_Click(object sender, RoutedEventArgs e)
        {
            Playlist_Click(sender,e);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void Save()
        {
            save.cat = GetDirListName();
            save.winStat = win.WindowState;
            if (win.WindowState == System.Windows.WindowState.Normal)
            {
                save.winWidth = win.ActualWidth;
                save.winHeight = win.ActualHeight;
            }
            save.sellist1 = listPlay.SelectedIndex;
            save.sellist2 = playlistSel.SelectedIndex;
            save.pllistWidth = pllist.Width;
            if (media.Source != null)
            {
                save.mediaUri = media.Source.LocalPath;
            }
            else save.mediaUri = "";
            XmlSerializer xml = new XmlSerializer(typeof(SaveProg));
            FileStream fs = new FileStream(USERDIR + "\\MediaPlayer\\save.dat", FileMode.Create, FileAccess.ReadWrite);
            xml.Serialize(fs, save);
            fs.Close();
            fs = null;
        }

        private void LoadProg()
        {
            try
            {
                FileStream f = new FileStream(USERDIR + "\\MediaPlayer\\save.dat", FileMode.Open, FileAccess.ReadWrite);
                XmlSerializer xml = new XmlSerializer(typeof(SaveProg));
                save = (SaveProg)xml.Deserialize(f);
                f.Close();
                f = null;
                SelectStyle(save.skin);
                SetDirListName(save.cat);
                win.WindowState = save.winStat;
                win.Width = save.winWidth;
                win.Height = save.winHeight;
                listPlay.SelectedIndex = save.sellist1;
                playlistSel.SelectedIndex = save.sellist2;
                pllist.Width = save.pllistWidth;
                //playlistSel.ScrollIntoView(playlistSel.Items[save.sellist2]);
                if (argspath != null && System.IO.Path.GetExtension(argspath) != ".exe")
                {
                    pllist.Width = 0;
                    media.Source = new Uri(argspath);
                    media.Play();
                    playstate.Visibility = Visibility.Visible;
                }
                else
                {
                    
                    media.Source = null;
                    if (save.mediaUri != "")
                    {
                        try
                        {
                            media.Source = new Uri(save.mediaUri);                   
                        }
                        catch (Exception) { }
                    }
                    listname = ((PlayList)listPlay.SelectedItem).longname;
                    numlistelem = playlistSel.SelectedIndex;
                    listtype = GetDirListName();
                    list_run.Clear();
                    list_run = list_pl_content.GetRange(0, list_pl_content.Count);
                } 
            }
            catch (Exception)
            {
                SelectStyle("3");
                save.skin = ("3");
            } 
            GetPl(list, listPlay);
            //playlistSel.ItemsSource = list_pl_content;
            //list_run = list_pl_content.GetRange(0, list_pl_content.Count); 
            
        }

        private void win_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Save();
        }

    }

    public class SaveProg
    {
        public string skin { get; set; }
        public string cat { get; set; }
        public double winWidth { get; set; }
        public double winHeight { get; set; }
        public WindowState winStat { get; set; }
        public int sellist1 { get; set; }
        public int sellist2 { get; set; }
        public double pllistWidth { get; set; }
        public string mediaUri { get; set; }
    }

    public class PlayList
    {
        public string name { get; set; }
        public string longname { get; set; }
    }

    class Helper
    {
        [DllImport("User32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, ref COPYDATASTRUCT lParam);

        public const int WM_COPYDATA = 0x4A;

        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DATASTRUCT
        {
            public int x;
            public int y;
            [MarshalAs(UnmanagedType.LPStr, SizeConst = 300)]
            public string s;
            public double d;
            public char c;
        };

        public static int SendWindowsStringMessage(IntPtr hWnd, string message)
        {
            int result = 0;
            if (hWnd != IntPtr.Zero)
            {
                COPYDATASTRUCT lParam = new COPYDATASTRUCT();
                lParam.cbData = message.Length * Marshal.SystemDefaultCharSize;
                lParam.dwData = IntPtr.Zero;
                lParam.lpData = Marshal.StringToHGlobalAnsi(message);
                SendMessage(hWnd, WM_COPYDATA, 0, ref lParam);
                Marshal.FreeHGlobal(lParam.lpData);
            }
            return result;
        }
    }
}
