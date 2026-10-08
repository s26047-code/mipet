using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace mipet
{
    public enum PetState
    {
        Sitting,   // 앉아 있음
        Petting,   // 쓰다듬는 중
        Grabbed,   // 잡힘
        Falling,   // 떨어지는 중
        Landed     // 주저앉음
    }

    public partial class MainWindow : Window
    {
        private PetState _state = PetState.Sitting;

        private PetSettings _settings = PetSettings.Load();
        private SettingsWindow? _settingsWindow;
        private CharacterWindow? _characterWindow;
        private const double BaseSize = 300;

        private Point _grabOffset;

        // grabbed 그림에서 옷자락 끝 위치 (비율)
        private const double GrabPointX = 0.47;
        private const double GrabPointY = 0.11;

        // 낙하 물리
        private double _velocityY = 0;
        private const double Gravity = 2500;
        private double _landedTime = 0;
        private const double LandedDuration = 0.5;   // 착지 후 주저앉아 있는 시간(초)

        // 진자 물리
        private double _angle = 0;
        private double _angularVelocity = 0;
        private double _wiggleTime = 0;
        private double _lastMouseX = 0;
        private double _mouseVelocityX = 0;

        private RotateTransform _bodyRotate = new RotateTransform(0);

        // 앉아 있을 때: 10초 가만히 → 3초 웃기 반복
        private const double OpenDuration = 10;     // 눈 뜨고 가만히 있는 시간(초)
        private const double LaughDuration = 3;     // 웃는 얼굴(눈 감음) 시간(초)
        private double _blinkTimer = OpenDuration;
        private double _blinkHold = 0;              // 0보다 크면 웃는 중

        // ---------- 쓰다듬기 ----------
        // 머리 영역 (창 크기 대비 비율)
        private const double HeadLeft = 0.25, HeadRight = 0.75;
        private const double HeadTop = 0.30, HeadBottom = 0.74;
        // 머리가 돌아가는 중심 = 목 위치 (비율)
        private const double NeckX = 0.48;
        private const double NeckY = 0.73;
        private const double MaxHeadAngleRight = 10;  // 오른쪽 최대 기울기(도)
        private const double MaxHeadAngleLeft = 6;    // 왼쪽 최대 기울기(도), 오른쪽보다 작게
        private const double PetStartRatio = 0.3;    // 이만큼(창 너비 대비) 좌우로 움직이면 쓰다듬기 시작
        private const double PetIdleLimit = 0.7;     // 이 시간(초) 안 움직이면 쓰다듬기 끝

        private RotateTransform _headRotate = new RotateTransform(0);
        private double _headAngle = 0;               // 현재 머리 각도
        private double _petScore = 0;                // 좌우로 움직인 누적 거리
        private double _petIdle = 0;                 // 마지막 움직임 이후 시간
        private double _petMouseX = 0;               // 창 안에서의 마우스 X
        private double _lastPetX = 0;
        private bool _hasLastPetX = false;

        private TimeSpan _lastRenderTime = TimeSpan.Zero;

        private BitmapImage _sittingBody;
        private BitmapImage _headOpen;
        private BitmapImage _headClosed;
        private BitmapImage _grabbedImage;
        private BitmapImage _fallingImage;
        private BitmapImage _landedImage;

        private string _imageKey = "";

        public MainWindow()
        {
            InitializeComponent();

            _sittingBody = LoadResourceImage("Assets/body_sitting_original.png");
            _headOpen = LoadResourceImage("Assets/head_open_original.png");
            _headClosed = LoadResourceImage("Assets/head_closed_original.png");
            _grabbedImage = LoadResourceImage("Assets/body_grabbed_original.png");
            _fallingImage = LoadResourceImage("Assets/body_falling_original.png");
            _landedImage = LoadResourceImage("Assets/body_landed_original.png");

            BodyImage.RenderTransform = _bodyRotate;
            BodyImage.RenderTransformOrigin = new Point(GrabPointX, GrabPointY);

            // 머리는 목 위치를 중심으로 회전
            HeadImage.RenderTransform = _headRotate;
            HeadImage.RenderTransformOrigin = new Point(NeckX, NeckY);

            // 마우스가 캐릭터 밖으로 나가면 쓰다듬기 종료
            MouseLeave += (s, e) =>
            {
                _hasLastPetX = false;
                if (_state == PetState.Petting) SetState(PetState.Sitting);
            };

            CompositionTarget.Rendering += OnRendering;
        }

        // ---------- 이미지 불러오기 ----------

        private BitmapImage LoadResourceImage(string path)
        {
            return new BitmapImage(new Uri("pack://application:,,,/" + path));
        }

        private BitmapImage? LoadUserImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private void ReloadImages()
        {
            string key = string.Join("|",
                _settings.SittingBodyPath, _settings.HeadOpenPath, _settings.HeadClosedPath,
                _settings.GrabbedImagePath, _settings.FallingImagePath, _settings.LandedImagePath);
            if (key == _imageKey) return;
            _imageKey = key;

            _sittingBody = LoadUserImage(_settings.SittingBodyPath) ?? LoadResourceImage("Assets/body_sitting_original.png");
            _headOpen = LoadUserImage(_settings.HeadOpenPath) ?? LoadResourceImage("Assets/head_open_original.png");
            _headClosed = LoadUserImage(_settings.HeadClosedPath) ?? LoadResourceImage("Assets/head_closed_original.png");
            _grabbedImage = LoadUserImage(_settings.GrabbedImagePath) ?? LoadResourceImage("Assets/body_grabbed_original.png");
            _fallingImage = LoadUserImage(_settings.FallingImagePath) ?? LoadResourceImage("Assets/body_falling_original.png");
            _landedImage = LoadUserImage(_settings.LandedImagePath) ?? LoadResourceImage("Assets/body_landed_original.png");

            RefreshView();
        }

        // ---------- 설정 반영 ----------

        private void ApplySettings()
        {
            ReloadImages();

            double size = BaseSize * _settings.Scale;
            Root.Width = size;
            Root.Height = size;

            if (_state == PetState.Sitting || _state == PetState.Petting || _state == PetState.Landed)
            {
                UpdateLayout();
                Top = GroundTop();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            PlaceAtBottom();
            RefreshView();
        }

        private const double BottomPaddingRatio = 0.12;

        private double GroundTop()
        {
            return SystemParameters.WorkArea.Bottom - ActualHeight * (1 - BottomPaddingRatio);
        }

        private void PlaceAtBottom()
        {
            Rect area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - ActualWidth) / 2;
            Top = GroundTop();
        }

        // ---------- 상태 ----------

        private void SetState(PetState newState)
        {
            _state = newState;

            switch (newState)
            {
                case PetState.Sitting:
                    _bodyRotate.Angle = 0;
                    _blinkTimer = OpenDuration;
                    _blinkHold = 0;
                    _petScore = 0;
                    break;
                case PetState.Petting:
                    _bodyRotate.Angle = 0;
                    _petIdle = 0;
                    break;
                case PetState.Landed:
                    _bodyRotate.Angle = 0;
                    _headAngle = 0;
                    _headRotate.Angle = 0;
                    break;
                case PetState.Grabbed:
                    _headAngle = 0;
                    _headRotate.Angle = 0;
                    _angle = 0;
                    _angularVelocity = 0;
                    _wiggleTime = 0;
                    _mouseVelocityX = 0;
                    _lastMouseX = GetMouseScreenX();
                    break;
                case PetState.Falling:
                    break;
            }

            RefreshView();
        }

        // 현재 상태에 맞는 그림을 화면에 표시
        private void RefreshView()
        {
            switch (_state)
            {
                case PetState.Sitting:
                case PetState.Petting:
                    BodyImage.Source = _sittingBody;
                    // 쓰다듬는 중이거나 웃는 시간이면 눈 감은 머리
                    bool closed = _state == PetState.Petting || _blinkHold > 0;
                    HeadImage.Source = closed ? _headClosed : _headOpen;
                    HeadImage.Visibility = Visibility.Visible;
                    break;
                case PetState.Landed:
                    BodyImage.Source = _landedImage;
                    HeadImage.Visibility = Visibility.Collapsed;
                    break;
                case PetState.Grabbed:
                    BodyImage.Source = _grabbedImage;
                    HeadImage.Visibility = Visibility.Collapsed;
                    break;
                case PetState.Falling:
                    BodyImage.Source = _fallingImage;
                    HeadImage.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        // ---------- 마우스 입력 ----------

        private double GetMouseScreenX()
        {
            Point p = PointToScreen(Mouse.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            return source.CompositionTarget.TransformFromDevice.Transform(p).X;
        }

        // 마우스가 머리 영역 위에 있는가?
        private bool IsOnHead(Point p)
        {
            double rx = p.X / ActualWidth;
            double ry = p.Y / ActualHeight;
            return rx >= HeadLeft && rx <= HeadRight && ry >= HeadTop && ry <= HeadBottom;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _grabOffset = new Point(ActualWidth * GrabPointX, ActualHeight * GrabPointY);

            CaptureMouse();
            SetState(PetState.Grabbed);   // 버튼을 누르면 쓰다듬기보다 잡기가 우선
            MoveWindowToMouse(e);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // 잡힌 상태: 캐릭터를 마우스로 끌고 감
            if (_state == PetState.Grabbed)
            {
                MoveWindowToMouse(e);
                return;
            }

            // 쓰다듬기는 앉아 있거나 쓰다듬는 중일 때만, 버튼을 누르지 않은 상태에서만
            if (_state != PetState.Sitting && _state != PetState.Petting) return;
            if (e.LeftButton == MouseButtonState.Pressed) return;

            Point p = e.GetPosition(this);

            // 머리 밖이면 쓰다듬기 중단
            if (!IsOnHead(p))
            {
                _hasLastPetX = false;
                if (_state == PetState.Petting) SetState(PetState.Sitting);
                return;
            }

            // 좌우로 움직인 거리를 누적
            if (_hasLastPetX)
            {
                _petScore += Math.Abs(p.X - _lastPetX);
            }
            _lastPetX = p.X;
            _hasLastPetX = true;
            _petMouseX = p.X;
            _petIdle = 0;

            // 충분히 문질렀으면 쓰다듬기 시작
            if (_state == PetState.Sitting && _petScore > ActualWidth * PetStartRatio)
            {
                SetState(PetState.Petting);
            }
        }

        private void MoveWindowToMouse(MouseEventArgs e)
        {
            Point mouseOnScreen = PointToScreen(e.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            Point wpfPoint = source.CompositionTarget.TransformFromDevice.Transform(mouseOnScreen);

            Left = wpfPoint.X - _grabOffset.X;
            Top = wpfPoint.Y - _grabOffset.Y;
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_state != PetState.Grabbed) return;

            ReleaseMouseCapture();
            _velocityY = 0;
            SetState(PetState.Falling);
        }

        // ---------- 우클릭 메뉴 ----------

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_settings, ApplySettings);
            _settingsWindow.Closed += (s, args) =>
            {
                _settingsWindow = null;
                _settings.Save();
            };
            _settingsWindow.Show();
        }

        private void MenuCharacter_Click(object sender, RoutedEventArgs e)
        {
            if (_characterWindow != null)
            {
                _characterWindow.Activate();
                return;
            }

            _characterWindow = new CharacterWindow(_settings, ApplySettings);
            _characterWindow.Closed += (s, args) =>
            {
                _characterWindow = null;
                _settings.Save();
            };
            _characterWindow.Show();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            _settings.Save();
            Application.Current.Shutdown();
        }

        // ---------- 매 프레임 실행 ----------

        private void OnRendering(object? sender, EventArgs e)
        {
            TimeSpan now = ((RenderingEventArgs)e).RenderingTime;

            if (now == _lastRenderTime) return;

            double dt = _lastRenderTime == TimeSpan.Zero ? 0 : (now - _lastRenderTime).TotalSeconds;
            _lastRenderTime = now;

            if (dt > 0.05) dt = 0.05;
            if (dt <= 0) return;

            switch (_state)
            {
                case PetState.Sitting:
                    UpdateLaugh(dt);
                    UpdateHeadAngle(dt, 0);
                    // 쓰다듬기 누적 점수는 시간이 지나면 서서히 줄어듦
                    _petScore = Math.Max(0, _petScore - 60 * dt);
                    break;

                case PetState.Petting:
                    UpdatePetting(dt);
                    break;

                case PetState.Grabbed:
                    UpdatePendulum(dt);
                    break;

                case PetState.Falling:
                    _angle += (0 - _angle) * Math.Min(1, 8 * dt);
                    _bodyRotate.Angle = _angle;

                    _velocityY += Gravity * dt;
                    Top += _velocityY * dt;

                    if (Top >= GroundTop())
                    {
                        Top = GroundTop();
                        _landedTime = 0;
                        SetState(PetState.Landed);
                    }
                    break;

                case PetState.Landed:
                    _landedTime += dt;
                    if (_landedTime >= LandedDuration)
                    {
                        SetState(PetState.Sitting);
                    }
                    break;
            }
        }

        // 앉아 있을 때: 10초 가만히 → 3초 웃기 → 반복
        private void UpdateLaugh(double dt)
        {
            if (_blinkHold > 0)
            {
                _blinkHold -= dt;
                if (_blinkHold <= 0)
                {
                    _blinkHold = 0;
                    _blinkTimer = OpenDuration;
                    RefreshView();
                }
            }
            else
            {
                _blinkTimer -= dt;
                if (_blinkTimer <= 0)
                {
                    _blinkHold = LaughDuration;
                    RefreshView();
                }
            }
        }

        // 쓰다듬는 중: 머리가 마우스를 따라 살짝 기울어짐
        private void UpdatePetting(double dt)
        {
            // 마우스가 멈춰 있으면 쓰다듬기 종료
            _petIdle += dt;
            if (_petIdle > PetIdleLimit)
            {
                SetState(PetState.Sitting);
                return;
            }

            // 머리 중심 기준으로 마우스가 얼마나 좌우에 있는지 (-1 ~ +1)
            double headCenterX = ActualWidth * (HeadLeft + HeadRight) / 2;
            double halfWidth = ActualWidth * (HeadRight - HeadLeft) / 2;
            double relative = (_petMouseX - headCenterX) / halfWidth;
            relative = Math.Max(-1, Math.Min(1, relative));

            // 최대 ±10도로 제한
            // 왼쪽(음수)은 6도, 오른쪽(양수)은 10도까지만
            double target = relative < 0
                ? relative * MaxHeadAngleLeft
                : relative * MaxHeadAngleRight;
            UpdateHeadAngle(dt, target);
        }

        // 머리 각도를 목표 각도로 부드럽게 이동
        private void UpdateHeadAngle(double dt, double target)
        {
            _headAngle += (target - _headAngle) * Math.Min(1, 8 * dt);
            _headRotate.Angle = _headAngle;
        }

        private void UpdatePendulum(double dt)
        {
            double mouseX = GetMouseScreenX();
            double rawVelocity = (mouseX - _lastMouseX) / dt;
            _lastMouseX = mouseX;

            _mouseVelocityX += (rawVelocity - _mouseVelocityX) * Math.Min(1, 20 * dt);

            double direction = _settings.ReverseFling ? -1 : 1;
            double force = _mouseVelocityX * _settings.FlingPower * direction;

            double acceleration = force
                                  - PetSettings.SpringStrength * _angle
                                  - PetSettings.Damping * _angularVelocity;

            _angularVelocity += acceleration * dt;
            _angle += _angularVelocity * dt;

            double max = _settings.MaxAngle;
            if (_angle > max) { _angle = max; _angularVelocity *= -0.3; }
            if (_angle < -max) { _angle = -max; _angularVelocity *= -0.3; }

            _wiggleTime += dt;
            double wiggle = Math.Sin(_wiggleTime * 9) * PetSettings.IdleWiggle;

            _bodyRotate.Angle = _angle + wiggle;
        }
    }
}