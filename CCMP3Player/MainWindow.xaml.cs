using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public string FileSize { get; set; } // 새로 추가
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
                defaultAlbumArt = new BitmapImage(new Uri("pack://application:,,,/BaseRes/CMP_Icon.png"));
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

                    // 파일 크기 계산 (MB 단위)
                    FileInfo fileInfo = new FileInfo(filePath);
                    trackInfo.FileSize = $"{fileInfo.Length / 1024.0 / 1024.0:0.00} MB";
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
            try
            {
                if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                {
                    var picture = file.Tag.Pictures[0];
                    if (picture.Data != null && picture.Data.Data != null)
                    {
                        using (var stream = new MemoryStream(picture.Data.Data))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // 색상 프로필 무시
                            bitmap.EndInit();
                            bitmap.Freeze();
                            return bitmap;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"앨범 아트 로드 실패: {ex.Message}");
            }

            // 기본 이미지 로드 (안정화된 방식)
            try
            {
                var defaultImage = new BitmapImage();
                defaultImage.BeginInit();
                defaultImage.UriSource = new Uri("pack://application:,,,/BaseRes/CMP_Icon.png");
                defaultImage.CacheOption = BitmapCacheOption.OnLoad;
                defaultImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // 동일 옵션 적용
                defaultImage.EndInit();
                defaultImage.Freeze();
                return defaultImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"기본 이미지 로드 실패: {ex.Message}");
                return new BitmapImage(); // 빈 이미지 반환
            }
        }

        // 플레이리스트 ListBox에서 항목 선택 시 이벤트 핸들러
        private void playlistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (playlistListBox.SelectedItem != null)
            {
                var selectedTrack = playlistListBox.SelectedItem as TrackInfo;
                if (selectedTrack != null)
                {                    
                    // 선택한 노래 정보 영역 업데이트
                    selectedTitleText.Text = selectedTrack.Title;
                    selectedArtistText.Text = selectedTrack.Artist;
                    selectedAlbumText.Text = selectedTrack.Album;
                    selectedPathText.Text = selectedTrack.FilePath;
                    selectedFileSizeText.Text = selectedTrack.FileSize;
                    albumArtImage_selected.Source = selectedTrack.AlbumArt;
                }
            }
        }

        // 새로 추가: 더블 클릭 시 재생
        private void PlaylistListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // 이벤트 전파 중지

            if (playlistListBox.SelectedItem != null)
            {
                var selectedTrack = playlistListBox.SelectedItem as TrackInfo;
                if (selectedTrack != null)
                {
                    PlaySelectedTrack(selectedTrack); // 실제 재생 로직 호출
                }
            }
        }

        private void PlaySelectedTrack(TrackInfo track)
        {
            try
            {
                // 4. 현재 재생 중인 미디어 중지 및 새 소스 로드
                mediaElement.Stop();
                mediaElement.Source = new Uri(track.FilePath);
                mediaElement.Play();

                // 5. UI 실시간 동기화
                songTitleText.Text = track.Title;
                artistText.Text = track.Artist;
                albumText.Text = track.Album;
                albumArtImage.Source = track.AlbumArt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"재생 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            // 1. 선택된 항목이 있는지 확인
            if (playlistListBox.SelectedItem != null)
            {
                // 2. 선택된 트랙을 명시적으로 재생
                var selectedTrack = playlistListBox.SelectedItem as TrackInfo;
                PlaySelectedTrack(selectedTrack);
            }
            else
            {
                // 3. 선택된 항목이 없을 때 첫 번째 항목 자동 선택 및 재생
                if (playlistListBox.Items.Count > 0)
                {
                    playlistListBox.SelectedIndex = 0;
                    PlaySelectedTrack(playlist[0]);
                }
                else
                {
                    MessageBox.Show("플레이리스트가 비어 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
            if (playlistListBox.Items.Count == 0) return;

            int newIndex = playlistListBox.SelectedIndex - 1;
            if (newIndex < 0) newIndex = playlistListBox.Items.Count - 1; // 순환 재생
            playlistListBox.SelectedIndex = newIndex;
            PlaySelectedTrack(playlist[newIndex]); // 명시적으로 트랙 재생
        }

        // 다음 트랙 버튼 클릭 이벤트
        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (playlistListBox.Items.Count == 0) return;

            int newIndex = playlistListBox.SelectedIndex + 1;
            if (newIndex >= playlistListBox.Items.Count) newIndex = 0; // 순환 재생
            playlistListBox.SelectedIndex = newIndex;
            PlaySelectedTrack(playlist[newIndex]); // 명시적으로 트랙 재생
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