using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TagLib;

namespace CCMP3Player
{
    public class TrackInfo
    {
        public string FilePath { get; set; }
        public string DisplayName { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public BitmapImage AlbumArt { get; set; }
    }

    public partial class MainWindow : Window
    {
        private List<TrackInfo> playlist = new List<TrackInfo>();
        private DispatcherTimer timer;
        private bool isUserSeeking = false;
        private BitmapImage defaultAlbumArt;

        public MainWindow()
        {
            InitializeComponent();

            // 초기 볼륨 설정
            mediaElement.Volume = volumeSlider.Value;

            // 재생시간 업데이트
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.5);
            timer.Tick += Timer_Tick;
            timer.Start();

            // 기본 앨범 아트 로드
            try
            {
                defaultAlbumArt = new BitmapImage(new Uri("pack://application:,,,/CCMP3Player;component/Resources/default_album.png"));
            }
            catch
            {
                // 기본 이미지 로드 실패 시 처리
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isUserSeeking && mediaElement.Source != null && mediaElement.NaturalDuration.HasTimeSpan)
            {
                seekBar.Maximum = mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                seekBar.Value = mediaElement.Position.TotalSeconds;

                playbackTimeText.Text = $"{mediaElement.Position.ToString(@"mm\:ss")} / {mediaElement.NaturalDuration.TimeSpan.ToString(@"mm\:ss")}";
            }
        }

        private void seekBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isUserSeeking = true;
            timer.Stop(); // 슬라이더 조작 중에는 자동 업데이트 정지
        }

        private void seekBar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mediaElement.NaturalDuration.HasTimeSpan)
            {
                mediaElement.Position = TimeSpan.FromSeconds(seekBar.Value);
            }
            isUserSeeking = false;
            timer.Start(); // 슬라이더 조작 후 다시 자동 업데이트 시작
        }

        // MP3 파일 추가 버튼 클릭 이벤트 핸들러
        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "MP3 files (*.mp3)|*.mp3",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var filename in openFileDialog.FileNames)
                {
                    AddTrackToPlaylist(filename);
                }
            }
        }

        private void AddTrackToPlaylist(string filePath)
        {
            try
            {
                var trackInfo = new TrackInfo { FilePath = filePath };

                // TagLib을 사용하여 MP3 태그 정보 읽기
                using (var file = TagLib.File.Create(filePath))
                {
                    // 제목 가져오기 (없으면 파일명 사용)
                    trackInfo.Title = !string.IsNullOrEmpty(file.Tag.Title)
                        ? file.Tag.Title
                        : System.IO.Path.GetFileNameWithoutExtension(filePath);

                    // 아티스트 가져오기
                    trackInfo.Artist = file.Tag.FirstPerformer ?? "Unknown Artist";

                    // 앨범 가져오기
                    trackInfo.Album = file.Tag.Album ?? "Unknown Album";

                    // 앨범 아트 가져오기
                    trackInfo.AlbumArt = GetAlbumArt(file);

                    // 표시 이름 설정
                    trackInfo.DisplayName = $"{trackInfo.Title} - {trackInfo.Artist}";
                }

                playlist.Add(trackInfo);
                playlistListBox.Items.Add(trackInfo);
            }
            catch (Exception ex)
            {
                // 에러 처리
                var trackInfo = new TrackInfo
                {
                    FilePath = filePath,
                    Title = System.IO.Path.GetFileNameWithoutExtension(filePath),
                    Artist = "Unknown Artist",
                    Album = "Unknown Album",
                    AlbumArt = defaultAlbumArt,
                    DisplayName = System.IO.Path.GetFileNameWithoutExtension(filePath)
                };

                playlist.Add(trackInfo);
                playlistListBox.Items.Add(trackInfo);

                MessageBox.Show($"Error reading track metadata: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private BitmapImage GetAlbumArt(TagLib.File file)
        {
            BitmapImage albumArt = null;
            try
            {
                if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                {
                    var picture = file.Tag.Pictures[0];
                    if (picture.Data != null && picture.Data.Data != null)
                    {
                        albumArt = new BitmapImage();
                        albumArt.BeginInit();
                        albumArt.StreamSource = new MemoryStream(picture.Data.Data);
                        albumArt.CacheOption = BitmapCacheOption.OnLoad;
                        albumArt.EndInit();
                        albumArt.Freeze();
                    }
                }
            }
            catch (Exception)
            {
                // 이미지 로드 실패 시 기본 이미지 사용
            }

            // 앨범 아트가 없거나 로드 실패 시 기본 이미지 사용
            return albumArt ?? defaultAlbumArt;
        }

        // 플레이리스트 ListBox에서 항목 선택 시 이벤트 핸들러
        private void playlistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (playlistListBox.SelectedItem != null)
            {
                var selectedTrack = playlistListBox.SelectedItem as TrackInfo;
                if (selectedTrack != null)
                {
                    PlaySelectedTrack(selectedTrack);
                }
            }
        }

        private void PlaySelectedTrack(TrackInfo track)
        {
            try
            {
                // 선택한 파일의 경로를 MediaElement의 Source로 지정하고 재생
                mediaElement.Source = new Uri(track.FilePath);
                mediaElement.Play();

                // 메타데이터 표시
                songTitleText.Text = track.Title;
                artistText.Text = track.Artist;
                albumText.Text = track.Album;
                albumArtImage.Source = track.AlbumArt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing track: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Play 버튼 클릭 이벤트 핸들러
        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.Source != null)
            {
                mediaElement.Play();
            }
            else if (playlistListBox.Items.Count > 0 && playlistListBox.SelectedIndex == -1)
            {
                // 재생 중인 트랙이 없고 플레이리스트에 항목이 있는 경우 첫 번째 트랙 재생
                playlistListBox.SelectedIndex = 0;
            }
        }

        // Pause 버튼 클릭 이벤트 핸들러
        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.Source != null)
            {
                mediaElement.Pause();
            }
        }

        // Stop 버튼 클릭 이벤트 핸들러
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.Source != null)
            {
                mediaElement.Stop();
                seekBar.Value = 0;
                playbackTimeText.Text = "00:00 / 00:00";
            }
        }

        // 볼륨 슬라이더 값 변경 이벤트 핸들러
        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaElement != null)
            {
                mediaElement.Volume = volumeSlider.Value;
            }
        }

        // 창 끌기 기능
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // 닫기 버튼 클릭 이벤트
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // 이전 트랙 버튼 클릭 이벤트
        private void btnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (playlistListBox.SelectedIndex > 0)
            {
                playlistListBox.SelectedIndex--;
            }
            else if (playlistListBox.Items.Count > 0)
            {
                // 첫 번째 트랙에서 이전 버튼 누르면 마지막 트랙으로 이동
                playlistListBox.SelectedIndex = playlistListBox.Items.Count - 1;
            }
        }

        // 다음 트랙 버튼 클릭 이벤트
        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (playlistListBox.SelectedIndex < playlistListBox.Items.Count - 1)
            {
                playlistListBox.SelectedIndex++;
            }
            else if (playlistListBox.Items.Count > 0)
            {
                // 마지막 트랙에서 다음 버튼 누르면 첫 번째 트랙으로 이동
                playlistListBox.SelectedIndex = 0;
            }
        }

        // 트랙 끝나면 다음 트랙 자동 재생
        private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            btnNext_Click(sender, e);
        }

        // 미디어 열렸을 때 이벤트
        private void mediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            // 미디어 파일이 로드되면 UI 업데이트
            if (mediaElement.NaturalDuration.HasTimeSpan)
            {
                seekBar.Maximum = mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                playbackTimeText.Text = $"00:00 / {mediaElement.NaturalDuration.TimeSpan.ToString(@"mm\:ss")}";
            }
        }

        // 선택된 트랙 제거 버튼 클릭 이벤트
        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (playlistListBox.SelectedIndex != -1)
            {
                int selectedIndex = playlistListBox.SelectedIndex;

                // 현재 재생 중인 트랙을 제거하는 경우
                if (mediaElement.Source != null && selectedIndex == playlistListBox.SelectedIndex)
                {
                    mediaElement.Stop();
                    mediaElement.Source = null;
                    ResetTrackInfo();
                }

                // 플레이리스트에서 제거
                playlist.RemoveAt(selectedIndex);
                playlistListBox.Items.RemoveAt(selectedIndex);

                // 새로운 항목 선택
                if (playlistListBox.Items.Count > 0)
                {
                    playlistListBox.SelectedIndex = Math.Min(selectedIndex, playlistListBox.Items.Count - 1);
                }
            }
        }

        // 플레이리스트 전체 제거 버튼 클릭 이벤트
        private void btnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear the entire playlist?",
                "Clear Playlist", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // 재생 중지 및 리소스 해제
                mediaElement.Stop();
                mediaElement.Source = null;

                // 플레이리스트 및 UI 초기화
                playlist.Clear();
                playlistListBox.Items.Clear();
                ResetTrackInfo();
            }
        }

        // 트랙 정보 초기화
        private void ResetTrackInfo()
        {
            songTitleText.Text = "No Track Selected";
            artistText.Text = "Unknown Artist";
            albumText.Text = "Unknown Album";
            albumArtImage.Source = defaultAlbumArt;
            playbackTimeText.Text = "00:00 / 00:00";
            seekBar.Value = 0;
        }
    }
}