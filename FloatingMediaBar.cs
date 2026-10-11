using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace FloatingMediaBar
{
    public class App : Application
    {
        private static Mutex _appMutex;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const uint WM_WAKEUP_INSTANCE = 0x0400 + 777;

        [STAThread]
        public static void Main()
        {
            bool createdNew;
            _appMutex = new Mutex(true, "Local\\FloatingMediaStudio_SingleInstance_Mutex_UniqueKey_9988", out createdNew);

            if (!createdNew)
            {
                // Instance already exists: wake it up and exit immediately
                IntPtr hWnd = FindWindow(null, "Floating Media Studio");
                if (hWnd != IntPtr.Zero)
                {
                    PostMessage(hWnd, WM_WAKEUP_INSTANCE, IntPtr.Zero, IntPtr.Zero);
                    SetForegroundWindow(hWnd);
                }
                return;
            }

            App app = new App();
            app.Run(new MainWindow());

            GC.KeepAlive(_appMutex);
        }
    }

    public enum LayoutState
    {
        Card,
        Bar,
        Micro
    }

    public enum ResizeDirection
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public class MainWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;

        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xCD;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static T Await<T>(IAsyncOperation<T> op)
        {
            if (op == null) return default(T);
            var ev = new ManualResetEvent(false);
            op.Completed = new AsyncOperationCompletedHandler<T>((info, status) => ev.Set());
            ev.WaitOne();
            return op.GetResults();
        }

        private Border _rootBorder;

        // Settings & State
        private bool _ambientEnabled = true;
        private bool _isLocked = false;
        private LayoutState _currentLayout = (LayoutState)(-1);

        // Dynamic Accent Colors (Dynamic Style)
        private Color _accentColor = Color.FromRgb(255, 255, 255);
        private Color _bgTintColor = Color.FromRgb(20, 21, 28);

        // Ambient Elements
        private Border _cardAmbientBackdrop;
        private Border _cardAmbientHalo;
        private Border _barAmbientHalo;
        private Border _microAmbientHalo;

        // Layout Containers
        private Grid _cardLayout;
        private Grid _barLayout;
        private Grid _microLayout;

        // Card Components
        private Border _cardArtBorder;
        private TextBlock _cardArtPlaceholder;
        private TextBlock _cardTitleText;
        private TextBlock _cardArtistText;
        private Button _cardPlayPauseBtn;
        private Border _cardPlayBorder;
        private Grid _cardProgressContainer;
        private Border _cardProgressTrack;
        private Border _cardProgressFill;
        private Border _cardProgressThumb;
        private TextBlock _cardTimeText;

        // Bar Components
        private Border _barArtBorder;
        private TextBlock _barArtPlaceholder;
        private TextBlock _barTitleText;
        private TextBlock _barArtistText;
        private Button _barPlayPauseBtn;
        private Border _barPlayBorder;
        private Grid _barProgressContainer;
        private Border _barProgressTrack;
        private Border _barProgressFill;
        private Border _barProgressThumb;

        // Micro Components (Artwork + Play Button)
        private Border _microArtBorder;
        private TextBlock _microArtPlaceholder;
        private Button _microPlayPauseBtn;
        private Border _microPlayBorder;
        private Button _microPrevBtn;
        private Button _microNextBtn;

        // Resizing & Dragging
        private ResizeDirection _activeResizeDir = ResizeDirection.None;
        private Point _resizeStartScreen;
        private Rect _resizeStartWindowRect;

        private bool _isDragging = false;
        private bool _hasDragged = false;
        private Point _dragStartPos;

        // Realtime Timeline & Scrubbing
        private DispatcherTimer _pollTimer;
        private DispatcherTimer _realtimeTickTimer;
        private bool _isUpdatingMedia = false;
        private bool _isPlaying = false;
        private bool _isScrubbing = false;
        private TimeSpan _scrubbedTime = TimeSpan.Zero;
        private TimeSpan _basePosition = TimeSpan.Zero;
        private DateTimeOffset _baseTime = DateTimeOffset.Now;
        private TimeSpan _duration = TimeSpan.Zero;

        public MainWindow()
        {
            Title = "Floating Media Studio";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol, Arial");
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            // Default Startup Dimensions: Pristine Card Mode
            Width = 276;
            Height = 380;
            Left = (SystemParameters.PrimaryScreenWidth - 276) / 2;
            Top = 30;

            SourceInitialized += MainWindow_SourceInitialized;

            BuildUI();

            // Set explicitly clean default layout
            SetLayoutState(LayoutState.Card, true);

            StartTimers();
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);

            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            if (source != null)
            {
                source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)msg == App.WM_WAKEUP_INSTANCE)
            {
                if (this.Visibility != Visibility.Visible)
                    this.Visibility = Visibility.Visible;
                if (this.WindowState == WindowState.Minimized)
                    this.WindowState = WindowState.Normal;
                this.Topmost = true;
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void SendKey(byte vk)
        {
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void BuildUI()
        {
            _rootBorder = new Border
            {
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1.2),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 26,
                    Opacity = 0.65
                }
            };

            UpdateRootBrushes();

            // Mouse wheel volume control
            _rootBorder.PreviewMouseWheel += (s, e) =>
            {
                if (e.Delta > 0)
                    SendKey(VK_VOLUME_UP);
                else if (e.Delta < 0)
                    SendKey(VK_VOLUME_DOWN);
                e.Handled = true;
            };

            // Drag window handling
            _rootBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (_isLocked) return;
                DependencyObject dep = e.OriginalSource as DependencyObject;
                if (e.OriginalSource is Button || (dep != null && FindParentButton(dep) != null))
                    return;

                _isDragging = true;
                _hasDragged = false;
                _dragStartPos = e.GetPosition(this);
                _rootBorder.CaptureMouse();
            };

            _rootBorder.MouseMove += (s, e) =>
            {
                if (_isDragging)
                {
                    Point cur = e.GetPosition(this);
                    double dx = cur.X - _dragStartPos.X;
                    double dy = cur.Y - _dragStartPos.Y;
                    if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
                        _hasDragged = true;

                    if (_hasDragged)
                    {
                        Left += dx;
                        Top += dy;
                    }
                }
            };

            _rootBorder.MouseLeftButtonUp += (s, e) =>
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    _rootBorder.ReleaseMouseCapture();

                    if (_hasDragged)
                        ApplyEdgeSnapping();
                }
            };

            _rootBorder.ContextMenu = CreateContextMenu();

            Grid masterGrid = new Grid();

            Grid contentContainer = new Grid();
            BuildCardLayout();
            BuildBarLayout();
            BuildMicroLayout();

            contentContainer.Children.Add(_cardLayout);
            contentContainer.Children.Add(_barLayout);
            contentContainer.Children.Add(_microLayout);

            masterGrid.Children.Add(contentContainer);

            // Add 8 native resize strips
            BuildResizeBorders(masterGrid);

            _rootBorder.Child = masterGrid;
            Content = _rootBorder;
        }

        private void UpdateRootBrushes()
        {
            // Dynamic Dynamic glass background tinted with album accent color
            LinearGradientBrush bgBrush = new LinearGradientBrush();
            bgBrush.StartPoint = new Point(0, 0);
            bgBrush.EndPoint = new Point(1, 1);
            bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(242, _bgTintColor.R, _bgTintColor.G, _bgTintColor.B), 0.0));
            bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(248, 12, 13, 17), 1.0));
            _rootBorder.Background = bgBrush;

            // Glass rim border tinted with accent
            LinearGradientBrush borderBrush = new LinearGradientBrush();
            borderBrush.StartPoint = new Point(0, 0);
            borderBrush.EndPoint = new Point(0, 1);
            borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(120, _accentColor.R, _accentColor.G, _accentColor.B), 0.0));
            borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(25, 255, 255, 255), 1.0));
            _rootBorder.BorderBrush = borderBrush;
        }

        // ================= BUILD 8 NATIVE RESIZE ZONES =================
        private void BuildResizeBorders(Grid grid)
        {
            const double THICK = 6.0;
            const double CORNER_SIZE = 14.0;

            grid.Children.Add(CreateResizeZone(ResizeDirection.Top, Cursors.SizeNS, HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, THICK, new Thickness(CORNER_SIZE, 0, CORNER_SIZE, 0)));
            grid.Children.Add(CreateResizeZone(ResizeDirection.Bottom, Cursors.SizeNS, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, THICK, new Thickness(CORNER_SIZE, 0, CORNER_SIZE, 0)));
            grid.Children.Add(CreateResizeZone(ResizeDirection.Left, Cursors.SizeWE, HorizontalAlignment.Left, VerticalAlignment.Stretch, THICK, double.NaN, new Thickness(0, CORNER_SIZE, 0, CORNER_SIZE)));
            grid.Children.Add(CreateResizeZone(ResizeDirection.Right, Cursors.SizeWE, HorizontalAlignment.Right, VerticalAlignment.Stretch, THICK, double.NaN, new Thickness(0, CORNER_SIZE, 0, CORNER_SIZE)));

            grid.Children.Add(CreateResizeZone(ResizeDirection.TopLeft, Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Top, CORNER_SIZE, CORNER_SIZE, new Thickness(0)));
            grid.Children.Add(CreateResizeZone(ResizeDirection.TopRight, Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Top, CORNER_SIZE, CORNER_SIZE, new Thickness(0)));
            grid.Children.Add(CreateResizeZone(ResizeDirection.BottomLeft, Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Bottom, CORNER_SIZE, CORNER_SIZE, new Thickness(0)));
            grid.Children.Add(CreateResizeZone(ResizeDirection.BottomRight, Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Bottom, CORNER_SIZE, CORNER_SIZE, new Thickness(0)));
        }

        private Border CreateResizeZone(ResizeDirection dir, Cursor cursor, HorizontalAlignment hAlign, VerticalAlignment vAlign, double width, double height, Thickness margin)
        {
            Border b = new Border
            {
                Background = Brushes.Transparent,
                Cursor = cursor,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Width = width,
                Height = height,
                Margin = margin
            };

            b.MouseLeftButtonDown += (s, e) => StartResize(dir, e, b);
            b.MouseMove += (s, e) => HandleResizeMove(e);
            b.MouseLeftButtonUp += (s, e) => EndResize(b);
            return b;
        }

        private void StartResize(ResizeDirection dir, MouseButtonEventArgs e, UIElement sender)
        {
            if (_isLocked) return;
            _activeResizeDir = dir;
            _resizeStartScreen = PointToScreen(e.GetPosition(this));
            _resizeStartWindowRect = new Rect(this.Left, this.Top, this.Width, this.Height);
            sender.CaptureMouse();
            e.Handled = true;
        }

        private void HandleResizeMove(MouseEventArgs e)
        {
            if (_activeResizeDir == ResizeDirection.None) return;

            Point cur = PointToScreen(e.GetPosition(this));
            double dx = cur.X - _resizeStartScreen.X;
            double dy = cur.Y - _resizeStartScreen.Y;

            double newLeft = _resizeStartWindowRect.X;
            double newTop = _resizeStartWindowRect.Y;
            double newWidth = _resizeStartWindowRect.Width;
            double newHeight = _resizeStartWindowRect.Height;

            const double MIN_W = 76;
            const double MIN_H = 76;

            switch (_activeResizeDir)
            {
                case ResizeDirection.Right:
                    newWidth = Math.Max(MIN_W, _resizeStartWindowRect.Width + dx);
                    break;
                case ResizeDirection.Bottom:
                    newHeight = Math.Max(MIN_H, _resizeStartWindowRect.Height + dy);
                    break;
                case ResizeDirection.Left:
                    double wL = _resizeStartWindowRect.Width - dx;
                    if (wL >= MIN_W) { newLeft = _resizeStartWindowRect.X + dx; newWidth = wL; }
                    else { newLeft = _resizeStartWindowRect.X + (_resizeStartWindowRect.Width - MIN_W); newWidth = MIN_W; }
                    break;
                case ResizeDirection.Top:
                    double hT = _resizeStartWindowRect.Height - dy;
                    if (hT >= MIN_H) { newTop = _resizeStartWindowRect.Y + dy; newHeight = hT; }
                    else { newTop = _resizeStartWindowRect.Y + (_resizeStartWindowRect.Height - MIN_H); newHeight = MIN_H; }
                    break;
                case ResizeDirection.BottomRight:
                    newWidth = Math.Max(MIN_W, _resizeStartWindowRect.Width + dx);
                    newHeight = Math.Max(MIN_H, _resizeStartWindowRect.Height + dy);
                    break;
                case ResizeDirection.BottomLeft:
                    newHeight = Math.Max(MIN_H, _resizeStartWindowRect.Height + dy);
                    double wBL = _resizeStartWindowRect.Width - dx;
                    if (wBL >= MIN_W) { newLeft = _resizeStartWindowRect.X + dx; newWidth = wBL; }
                    else { newLeft = _resizeStartWindowRect.X + (_resizeStartWindowRect.Width - MIN_W); newWidth = MIN_W; }
                    break;
                case ResizeDirection.TopRight:
                    newWidth = Math.Max(MIN_W, _resizeStartWindowRect.Width + dx);
                    double hTR = _resizeStartWindowRect.Height - dy;
                    if (hTR >= MIN_H) { newTop = _resizeStartWindowRect.Y + dy; newHeight = hTR; }
                    else { newTop = _resizeStartWindowRect.Y + (_resizeStartWindowRect.Height - MIN_H); newHeight = MIN_H; }
                    break;
                case ResizeDirection.TopLeft:
                    double wTL = _resizeStartWindowRect.Width - dx;
                    double hTL = _resizeStartWindowRect.Height - dy;
                    if (wTL >= MIN_W) { newLeft = _resizeStartWindowRect.X + dx; newWidth = wTL; }
                    else { newLeft = _resizeStartWindowRect.X + (_resizeStartWindowRect.Width - MIN_W); newWidth = MIN_W; }
                    if (hTL >= MIN_H) { newTop = _resizeStartWindowRect.Y + dy; newHeight = hTL; }
                    else { newTop = _resizeStartWindowRect.Y + (_resizeStartWindowRect.Height - MIN_H); newHeight = MIN_H; }
                    break;
            }

            this.Left = newLeft;
            this.Top = newTop;
            this.Width = newWidth;
            this.Height = newHeight;

            CheckLayoutBreakpoint();
        }

        private void EndResize(UIElement sender)
        {
            if (_activeResizeDir != ResizeDirection.None)
            {
                _activeResizeDir = ResizeDirection.None;
                sender.ReleaseMouseCapture();
                ApplyEdgeSnapping();
            }
        }

        // ================= DYNAMIC BREAKPOINTS =================
        private void CheckLayoutBreakpoint()
        {
            if (Width <= 135 && Height <= 135)
            {
                SetLayoutState(LayoutState.Micro);
            }
            else if (Height <= 130 && Width > 135)
            {
                SetLayoutState(LayoutState.Bar);
            }
            else
            {
                SetLayoutState(LayoutState.Card);
            }
        }

        private void SetLayoutState(LayoutState state, bool force = false)
        {
            if (!force && _currentLayout == state) return;
            _currentLayout = state;

            _cardLayout.Visibility = (state == LayoutState.Card) ? Visibility.Visible : Visibility.Collapsed;
            _barLayout.Visibility = (state == LayoutState.Bar) ? Visibility.Visible : Visibility.Collapsed;
            _microLayout.Visibility = (state == LayoutState.Micro) ? Visibility.Visible : Visibility.Collapsed;

            if (state == LayoutState.Micro)
            {
                _rootBorder.CornerRadius = new CornerRadius(Math.Min(Width, Height) / 2);
                _rootBorder.Padding = new Thickness(4);
            }
            else if (state == LayoutState.Bar)
            {
                _rootBorder.CornerRadius = new CornerRadius(16);
                _rootBorder.Padding = new Thickness(0);
            }
            else // Card
            {
                _rootBorder.CornerRadius = new CornerRadius(20);
                _rootBorder.Padding = new Thickness(0);
            }
        }

        private void ApplyEdgeSnapping()
        {
            const double SNAP = 18.0;
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            if (Math.Abs(this.Left) < SNAP) this.Left = 10;
            if (Math.Abs(this.Left + this.Width - screenW) < SNAP) this.Left = screenW - this.Width - 10;
            if (Math.Abs(this.Top) < SNAP) this.Top = 10;
            if (Math.Abs(this.Top + this.Height - screenH) < SNAP) this.Top = screenH - this.Height - 10;
        }

        // ================= BUILD CARD LAYOUT =================
        private void BuildCardLayout()
        {
            _cardLayout = new Grid
            {
                Margin = new Thickness(12, 10, 12, 10),
                Visibility = Visibility.Collapsed
            };
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Hero Artwork
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Meta
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Dynamic Seek Bar
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Volume

            // Ambient Backdrop
            _cardAmbientBackdrop = new Border
            {
                CornerRadius = new CornerRadius(18),
                Effect = new BlurEffect { Radius = 55, RenderingBias = RenderingBias.Performance },
                Opacity = 0.22,
                Margin = new Thickness(-10),
                Visibility = _ambientEnabled ? Visibility.Visible : Visibility.Collapsed
            };
            Grid.SetRowSpan(_cardAmbientBackdrop, 6);
            _cardLayout.Children.Add(_cardAmbientBackdrop);

            // 1. Header
            DockPanel header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 6) };
            TextBlock tag = new TextBlock
            {
                Text = "✦ ĐANG PHÁT",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(tag, Dock.Left);
            header.Children.Add(tag);

            StackPanel winBtns = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(winBtns, Dock.Right);

            Button lockBtn = CreateIconButton("📌", "Khóa / Mở khóa vị trí", (s, e) => ToggleLock(), 22, 10);
            Button minBtn = CreateIconButton("−", "Thu nhỏ thành bong bóng mini", (s, e) => ShrinkToMicro(), 22, 12);
            Button closeBtn = CreateIconButton("✕", "Đóng ứng dụng", (s, e) => Close(), 22, 11, true);

            winBtns.Children.Add(lockBtn);
            winBtns.Children.Add(minBtn);
            winBtns.Children.Add(closeBtn);
            header.Children.Add(winBtns);
            Grid.SetRow(header, 0);
            _cardLayout.Children.Add(header);

            // 2. Hero Artwork Box
            Grid heroArtContainer = new Grid { Margin = new Thickness(0, 0, 0, 8) };

            _cardAmbientHalo = new Border
            {
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(-6),
                Effect = new BlurEffect { Radius = 32, RenderingBias = RenderingBias.Performance },
                Opacity = 0.65,
                Visibility = _ambientEnabled ? Visibility.Visible : Visibility.Collapsed
            };
            heroArtContainer.Children.Add(_cardAmbientHalo);

            _cardArtBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 3,
                    BlurRadius = 14,
                    Opacity = 0.45
                }
            };
            _cardArtPlaceholder = new TextBlock
            {
                Text = "🎵",
                FontSize = 42,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255))
            };
            _cardArtBorder.Child = _cardArtPlaceholder;
            heroArtContainer.Children.Add(_cardArtBorder);

            Grid.SetRow(heroArtContainer, 1);
            _cardLayout.Children.Add(heroArtContainer);

            // 3. Meta Text
            StackPanel metaPanel = new StackPanel { Margin = new Thickness(2, 0, 2, 6), HorizontalAlignment = HorizontalAlignment.Center };
            _cardTitleText = new TextBlock
            {
                Text = "Đang tải bài hát...",
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 230
            };
            _cardArtistText = new TextBlock
            {
                Text = "Trình phát nhạc hệ thống",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 165, 175)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                MaxWidth = 230
            };
            metaPanel.Children.Add(_cardTitleText);
            metaPanel.Children.Add(_cardArtistText);
            Grid.SetRow(metaPanel, 2);
            _cardLayout.Children.Add(metaPanel);

            // 4. Dynamic Signature Fluid Seek Bar
            Grid progContainer = new Grid { Margin = new Thickness(4, 0, 4, 6) };
            progContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            progContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _cardProgressContainer = new Grid
            {
                Height = 20,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };

            _cardProgressTrack = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            };

            _cardProgressFill = new Border
            {
                Height = 6,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Dynamic Scrubber Capsule Thumb
            _cardProgressThumb = new Border
            {
                Width = 6,
                Height = 16,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.White,
                Margin = new Thickness(-3, 0, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 8,
                    Opacity = 0.5,
                    ShadowDepth = 0
                }
            };

            _cardProgressContainer.Children.Add(_cardProgressTrack);
            _cardProgressContainer.Children.Add(_cardProgressFill);
            _cardProgressContainer.Children.Add(_cardProgressThumb);

            // Interactive dragging / scrubbing on Dynamic bar
            _cardProgressContainer.PreviewMouseLeftButtonDown += (s, e) => StartScrubbing(e, _cardProgressContainer, _cardProgressTrack);
            _cardProgressContainer.PreviewMouseMove += (s, e) => HandleScrubbing(e, _cardProgressContainer, _cardProgressTrack, _cardProgressFill, _cardProgressThumb, _cardTimeText);
            _cardProgressContainer.PreviewMouseLeftButtonUp += (s, e) => EndScrubbing(_cardProgressContainer);

            // Hover expansion effect
            _cardProgressContainer.MouseEnter += (s, e) =>
            {
                _cardProgressTrack.Height = 8;
                _cardProgressFill.Height = 8;
                _cardProgressThumb.Width = 8;
                _cardProgressThumb.Height = 18;
            };
            _cardProgressContainer.MouseLeave += (s, e) =>
            {
                if (!_isScrubbing)
                {
                    _cardProgressTrack.Height = 6;
                    _cardProgressFill.Height = 6;
                    _cardProgressThumb.Width = 6;
                    _cardProgressThumb.Height = 16;
                }
            };

            Grid.SetRow(_cardProgressContainer, 0);
            progContainer.Children.Add(_cardProgressContainer);

            _cardTimeText = new TextBlock
            {
                Text = "00:00 / 00:00",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetRow(_cardTimeText, 1);
            progContainer.Children.Add(_cardTimeText);

            Grid.SetRow(progContainer, 3);
            _cardLayout.Children.Add(progContainer);

            // 5. Controls
            StackPanel ctrlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Button prevBtn = CreateIconButton("⏮", "Bài trước", (s, e) => TriggerSkipPrev(), 34, 14);
            _cardPlayPauseBtn = CreateLuxuryPlayButton((s, e) => TriggerPlayPause(), out _cardPlayBorder);
            Button nextBtn = CreateIconButton("⏭", "Bài tiếp theo", (s, e) => TriggerSkipNext(), 34, 14);

            ctrlPanel.Children.Add(prevBtn);
            ctrlPanel.Children.Add(_cardPlayPauseBtn);
            ctrlPanel.Children.Add(nextBtn);
            Grid.SetRow(ctrlPanel, 4);
            _cardLayout.Children.Add(ctrlPanel);

            // 6. Volume Row
            StackPanel volPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            volPanel.Children.Add(CreateIconButton("🔉", "Giảm âm lượng", (s, e) => SendKey(VK_VOLUME_DOWN), 24, 10));
            volPanel.Children.Add(CreateIconButton("🔊", "Tăng âm lượng", (s, e) => SendKey(VK_VOLUME_UP), 24, 10));
            volPanel.Children.Add(CreateIconButton("🔇", "Tắt tiếng", (s, e) => SendKey(VK_VOLUME_MUTE), 24, 10));

            Grid.SetRow(volPanel, 5);
            _cardLayout.Children.Add(volPanel);
        }

        // ================= BUILD BAR LAYOUT =================
        private void BuildBarLayout()
        {
            _barLayout = new Grid
            {
                Margin = new Thickness(8, 6, 8, 6),
                Visibility = Visibility.Collapsed
            };
            _barLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Art
            _barLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info & Seek
            _barLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Controls
            _barLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Vol & Window

            // Thumbnail
            Grid barThumbContainer = new Grid { Margin = new Thickness(0, 0, 10, 0) };
            _barAmbientHalo = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(12),
                Effect = new BlurEffect { Radius = 18, RenderingBias = RenderingBias.Performance },
                Opacity = 0.55,
                Visibility = _ambientEnabled ? Visibility.Visible : Visibility.Collapsed
            };
            barThumbContainer.Children.Add(_barAmbientHalo);

            _barArtBorder = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                Cursor = Cursors.Hand
            };
            _barArtPlaceholder = new TextBlock
            {
                Text = "🎵",
                FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255))
            };
            _barArtBorder.Child = _barArtPlaceholder;
            barThumbContainer.Children.Add(_barArtBorder);
            Grid.SetColumn(barThumbContainer, 0);
            _barLayout.Children.Add(barThumbContainer);

            // Info & Dynamic Mini Seek
            StackPanel textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _barTitleText = new TextBlock
            {
                Text = "Đang phát media...",
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _barArtistText = new TextBlock
            {
                Text = "Trình phát nhạc hệ thống",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 165, 175)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 2)
            };

            // Bar Progress Container
            _barProgressContainer = new Grid
            {
                Height = 12,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };

            _barProgressTrack = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            };

            _barProgressFill = new Border
            {
                Height = 4,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(2),
                Background = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            _barProgressThumb = new Border
            {
                Width = 5,
                Height = 10,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.White,
                Margin = new Thickness(-2.5, 0, 0, 0)
            };

            _barProgressContainer.Children.Add(_barProgressTrack);
            _barProgressContainer.Children.Add(_barProgressFill);
            _barProgressContainer.Children.Add(_barProgressThumb);

            _barProgressContainer.PreviewMouseLeftButtonDown += (s, e) => StartScrubbing(e, _barProgressContainer, _barProgressTrack);
            _barProgressContainer.PreviewMouseMove += (s, e) => HandleScrubbing(e, _barProgressContainer, _barProgressTrack, _barProgressFill, _barProgressThumb, null);
            _barProgressContainer.PreviewMouseLeftButtonUp += (s, e) => EndScrubbing(_barProgressContainer);

            textPanel.Children.Add(_barTitleText);
            textPanel.Children.Add(_barArtistText);
            textPanel.Children.Add(_barProgressContainer);
            Grid.SetColumn(textPanel, 1);
            _barLayout.Children.Add(textPanel);

            // Media Controls
            StackPanel mediaPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            mediaPanel.Children.Add(CreateIconButton("⏮", "Bài trước", (s, e) => TriggerSkipPrev(), 30, 13));
            _barPlayPauseBtn = CreateLuxuryPlayButton((s, e) => TriggerPlayPause(), out _barPlayBorder, 34, 13);
            mediaPanel.Children.Add(_barPlayPauseBtn);
            mediaPanel.Children.Add(CreateIconButton("⏭", "Bài sau", (s, e) => TriggerSkipNext(), 30, 13));
            Grid.SetColumn(mediaPanel, 2);
            _barLayout.Children.Add(mediaPanel);

            // Volume & Tools
            StackPanel winPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            winPanel.Children.Add(CreateDivider());
            winPanel.Children.Add(CreateIconButton("🔉", "Giảm âm lượng", (s, e) => SendKey(VK_VOLUME_DOWN), 24, 10));
            winPanel.Children.Add(CreateIconButton("🔊", "Tăng âm lượng", (s, e) => SendKey(VK_VOLUME_UP), 24, 10));
            winPanel.Children.Add(CreateIconButton("📌", "Khóa / Mở khóa", (s, e) => ToggleLock(), 24, 10));
            winPanel.Children.Add(CreateIconButton("✕", "Đóng ứng dụng", (s, e) => Close(), 24, 11, true));
            Grid.SetColumn(winPanel, 3);
            _barLayout.Children.Add(winPanel);
        }

        // ================= BUILD MICRO LAYOUT =================
        private void BuildMicroLayout()
        {
            _microLayout = new Grid
            {
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };

            // Ambient Halo
            _microAmbientHalo = new Border
            {
                CornerRadius = new CornerRadius(16),
                Effect = new BlurEffect { Radius = 20, RenderingBias = RenderingBias.Performance },
                Opacity = 0.65,
                Margin = new Thickness(-4),
                Visibility = _ambientEnabled ? Visibility.Visible : Visibility.Collapsed
            };
            _microLayout.Children.Add(_microAmbientHalo);

            // Artwork Box
            _microArtBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                ClipToBounds = true
            };
            _microArtPlaceholder = new TextBlock
            {
                Text = "🎵",
                FontSize = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            _microArtBorder.Child = _microArtPlaceholder;
            _microLayout.Children.Add(_microArtBorder);

            // Hover Controls
            Grid hoverOverlay = new Grid();

            _microPrevBtn = CreateIconButton("⏮", "Bài trước", (s, e) => TriggerSkipPrev(), 22, 10);
            _microPrevBtn.HorizontalAlignment = HorizontalAlignment.Left;
            _microPrevBtn.VerticalAlignment = VerticalAlignment.Center;
            _microPrevBtn.Margin = new Thickness(2, 0, 0, 0);
            _microPrevBtn.Opacity = 0;

            _microPlayPauseBtn = CreateLuxuryPlayButton((s, e) => TriggerPlayPause(), out _microPlayBorder, 38, 14);
            _microPlayPauseBtn.HorizontalAlignment = HorizontalAlignment.Center;
            _microPlayPauseBtn.VerticalAlignment = VerticalAlignment.Center;

            _microNextBtn = CreateIconButton("⏭", "Bài sau", (s, e) => TriggerSkipNext(), 22, 10);
            _microNextBtn.HorizontalAlignment = HorizontalAlignment.Right;
            _microNextBtn.VerticalAlignment = VerticalAlignment.Center;
            _microNextBtn.Margin = new Thickness(0, 0, 2, 0);
            _microNextBtn.Opacity = 0;

            _microLayout.MouseEnter += (s, e) =>
            {
                _microPrevBtn.Opacity = 0.9;
                _microNextBtn.Opacity = 0.9;
            };
            _microLayout.MouseLeave += (s, e) =>
            {
                _microPrevBtn.Opacity = 0;
                _microNextBtn.Opacity = 0;
            };

            hoverOverlay.Children.Add(_microPrevBtn);
            hoverOverlay.Children.Add(_microPlayPauseBtn);
            hoverOverlay.Children.Add(_microNextBtn);

            _microLayout.Children.Add(hoverOverlay);
        }

        private void ShrinkToMicro()
        {
            Width = 96;
            Height = 96;
            CheckLayoutBreakpoint();
        }

        private void ToggleLock()
        {
            _isLocked = !_isLocked;
            string msg = _isLocked ? "Đã KHÓA vị trí & kích thước (Tránh bấm nhầm khi chơi game)" : "Đã MỞ KHÓA (Có thể di chuyển & kéo giãn)";
            _rootBorder.ToolTip = msg;
        }

        // ================= INTERACTIVE DRAG / SCRUBBING =================
        private void StartScrubbing(MouseButtonEventArgs e, UIElement container, Border track)
        {
            if (_duration.TotalSeconds <= 0) return;
            _isScrubbing = true;
            container.CaptureMouse();

            Point p = e.GetPosition(track);
            double ratio = Math.Max(0.0, Math.Min(1.0, p.X / track.ActualWidth));
            _scrubbedTime = TimeSpan.FromTicks((long)(_duration.Ticks * ratio));
            e.Handled = true;
        }

        private void HandleScrubbing(MouseEventArgs e, UIElement container, Border track, Border fill, Border thumb, TextBlock timeText)
        {
            if (!_isScrubbing || _duration.TotalSeconds <= 0) return;

            Point p = e.GetPosition(track);
            double ratio = Math.Max(0.0, Math.Min(1.0, p.X / track.ActualWidth));
            _scrubbedTime = TimeSpan.FromTicks((long)(_duration.Ticks * ratio));

            double w = ratio * track.ActualWidth;
            fill.Width = Math.Max(0, w);
            thumb.Margin = new Thickness(w - (thumb.Width / 2), 0, 0, 0);

            if (timeText != null)
            {
                string posStr = string.Format("{0:D2}:{1:D2}", (int)_scrubbedTime.TotalMinutes, _scrubbedTime.Seconds);
                string durStr = string.Format("{0:D2}:{1:D2}", (int)_duration.TotalMinutes, _duration.Seconds);
                timeText.Text = posStr + " / " + durStr;
            }
        }

        private void EndScrubbing(UIElement container)
        {
            if (_isScrubbing)
            {
                _isScrubbing = false;
                container.ReleaseMouseCapture();

                long targetTicks = _scrubbedTime.Ticks;
                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        var mgr = Await(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                        var session = mgr != null ? mgr.GetCurrentSession() : null;
                        if (session != null)
                        {
                            Await(session.TryChangePlaybackPositionAsync(targetTicks));
                        }
                    }
                    catch {}
                });

                _basePosition = _scrubbedTime;
                _baseTime = DateTimeOffset.Now;
            }
        }

        // ================= MEDIA TIMERS & MONITORING =================
        private void StartTimers()
        {
            QueryMediaInfo();

            // Background polling: sync track info & baseline position every 1.5s
            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromSeconds(1.5);
            _pollTimer.Tick += (s, e) => QueryMediaInfo();
            _pollTimer.Start();

            // High-precision smooth animation timer: runs every 100ms in true real-time!
            _realtimeTickTimer = new DispatcherTimer();
            _realtimeTickTimer.Interval = TimeSpan.FromMilliseconds(100);
            _realtimeTickTimer.Tick += RealtimeTick;
            _realtimeTickTimer.Start();
        }

        private void RealtimeTick(object sender, EventArgs e)
        {
            if (_isScrubbing || _duration.TotalSeconds <= 0) return;

            TimeSpan current = _basePosition;
            if (_isPlaying)
            {
                TimeSpan elapsed = DateTimeOffset.Now - _baseTime;
                current = _basePosition + elapsed;
                if (current > _duration) current = _duration;
            }

            UpdateTimelineUI(current, _duration);
        }

        private void UpdateTimelineUI(TimeSpan pos, TimeSpan dur)
        {
            if (dur.TotalSeconds <= 0)
            {
                _cardProgressFill.Width = 0;
                _cardProgressThumb.Margin = new Thickness(-3, 0, 0, 0);
                _barProgressFill.Width = 0;
                _barProgressThumb.Margin = new Thickness(-2.5, 0, 0, 0);
                _cardTimeText.Text = "--:-- / --:--";
                return;
            }

            double ratio = Math.Max(0.0, Math.Min(1.0, pos.TotalSeconds / dur.TotalSeconds));

            // Card Bar Update
            double cardTrackW = _cardProgressTrack.ActualWidth;
            if (cardTrackW > 0)
            {
                double cardW = ratio * cardTrackW;
                _cardProgressFill.Width = Math.Max(0, cardW);
                _cardProgressThumb.Margin = new Thickness(cardW - (_cardProgressThumb.Width / 2), 0, 0, 0);
            }

            // Bar Update
            double barTrackW = _barProgressTrack.ActualWidth;
            if (barTrackW > 0)
            {
                double barW = ratio * barTrackW;
                _barProgressFill.Width = Math.Max(0, barW);
                _barProgressThumb.Margin = new Thickness(barW - (_barProgressThumb.Width / 2), 0, 0, 0);
            }

            string posStr = string.Format("{0:D2}:{1:D2}", (int)pos.TotalMinutes, pos.Seconds);
            string durStr = string.Format("{0:D2}:{1:D2}", (int)dur.TotalMinutes, dur.Seconds);
            _cardTimeText.Text = posStr + " / " + durStr;
        }

        private void QueryMediaInfo()
        {
            if (_isUpdatingMedia) return;
            _isUpdatingMedia = true;

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    var mgr = Await(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                    var session = mgr != null ? mgr.GetCurrentSession() : null;

                    string title = "";
                    string artist = "";
                    byte[] thumbBytes = null;
                    bool isPlaying = false;
                    TimeSpan rawPos = TimeSpan.Zero;
                    DateTimeOffset updatedTime = DateTimeOffset.Now;
                    TimeSpan dur = TimeSpan.Zero;

                    if (session != null)
                    {
                        var playback = session.GetPlaybackInfo();
                        if (playback != null)
                        {
                            isPlaying = (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
                        }

                        var timeline = session.GetTimelineProperties();
                        if (timeline != null)
                        {
                            rawPos = timeline.Position;
                            updatedTime = timeline.LastUpdatedTime;
                            dur = timeline.EndTime;
                        }

                        var props = Await(session.TryGetMediaPropertiesAsync());
                        if (props != null)
                        {
                            title = props.Title ?? "";
                            artist = props.Artist ?? "";

                            if (props.Thumbnail != null)
                            {
                                try
                                {
                                    var stream = Await(props.Thumbnail.OpenReadAsync());
                                    if (stream != null && stream.Size > 0)
                                    {
                                        var reader = new DataReader(stream.GetInputStreamAt(0));
                                        Await(reader.LoadAsync((uint)stream.Size));
                                        thumbBytes = new byte[stream.Size];
                                        reader.ReadBytes(thumbBytes);
                                    }
                                }
                                catch {}
                            }
                        }
                    }

                    // Extract color on background thread for silky smooth 60fps UI
                    Color extractedAccent = Color.FromRgb(255, 255, 255);
                    Color extractedBg = Color.FromRgb(20, 21, 28);
                    if (thumbBytes != null && thumbBytes.Length > 0)
                    {
                        extractedAccent = ExtractVibrantColor(thumbBytes);
                        extractedBg = DeriveDarkBg(extractedAccent);
                    }

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ApplyMediaUpdate(title, artist, thumbBytes, isPlaying, session != null, rawPos, updatedTime, dur, extractedAccent, extractedBg);
                    }));
                }
                catch {}
                finally
                {
                    _isUpdatingMedia = false;
                }
            });
        }

        private void ApplyMediaUpdate(string title, string artist, byte[] thumbBytes, bool isPlaying, bool hasSession, TimeSpan rawPos, DateTimeOffset updatedTime, TimeSpan dur, Color accent, Color bgTint)
        {
            _isPlaying = isPlaying;
            _duration = dur;

            // Accurate extrapolation: baseline position + time elapsed since Windows SMTC snapshot!
            if (isPlaying && updatedTime > DateTimeOffset.MinValue)
            {
                TimeSpan elapsed = DateTimeOffset.Now - updatedTime;
                if (elapsed.TotalSeconds >= 0 && elapsed.TotalSeconds < 3600)
                {
                    _basePosition = rawPos + elapsed;
                }
                else
                {
                    _basePosition = rawPos;
                }
            }
            else
            {
                _basePosition = rawPos;
            }
            _baseTime = DateTimeOffset.Now;

            string playIcon = isPlaying ? "⏸" : "▶";
            string playTip = isPlaying ? "Tạm dừng" : "Phát";

            _cardPlayPauseBtn.Content = playIcon;
            _cardPlayPauseBtn.ToolTip = playTip;
            _barPlayPauseBtn.Content = playIcon;
            _barPlayPauseBtn.ToolTip = playTip;
            _microPlayPauseBtn.Content = playIcon;
            _microPlayPauseBtn.ToolTip = playTip;

            // Apply Dynamic Dynamic Theme
            ApplyThemeColors(accent, bgTint);

            if (!hasSession || (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist)))
            {
                string noMediaTitle = "Không có media đang phát";
                string noMediaSub = "Sẵn sàng (Chrome, Spotify, VLC...)";

                _cardTitleText.Text = noMediaTitle;
                _cardArtistText.Text = noMediaSub;
                _barTitleText.Text = noMediaTitle;
                _barArtistText.Text = noMediaSub;

                SetThumbnailSource(null);
                UpdateTimelineUI(TimeSpan.Zero, TimeSpan.Zero);
                return;
            }

            string displayTitle = string.IsNullOrEmpty(title) ? "Đang phát..." : title;
            string displayArtist = string.IsNullOrEmpty(artist) ? "Âm thanh máy tính" : artist;

            _cardTitleText.Text = displayTitle;
            _cardArtistText.Text = displayArtist;
            _barTitleText.Text = displayTitle;
            _barArtistText.Text = displayArtist;

            string fullTip = displayTitle + "\n" + displayArtist;
            _cardTitleText.ToolTip = fullTip;
            _barTitleText.ToolTip = fullTip;
            _microArtBorder.ToolTip = fullTip;

            if (thumbBytes != null && thumbBytes.Length > 0)
            {
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(thumbBytes);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    SetThumbnailSource(bmp);
                }
                catch
                {
                    SetThumbnailSource(null);
                }
            }
            else
            {
                SetThumbnailSource(null);
            }
        }

        private void SetThumbnailSource(BitmapImage bmp)
        {
            if (bmp != null)
            {
                ImageBrush brush = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };

                _cardArtBorder.Background = brush;
                _cardArtPlaceholder.Visibility = Visibility.Collapsed;
                _cardAmbientHalo.Background = brush;
                _cardAmbientBackdrop.Background = brush;

                _barArtBorder.Background = brush;
                _barArtPlaceholder.Visibility = Visibility.Collapsed;
                _barAmbientHalo.Background = brush;

                _microArtBorder.Background = brush;
                _microArtPlaceholder.Visibility = Visibility.Collapsed;
                _microAmbientHalo.Background = brush;
            }
            else
            {
                SolidColorBrush placeholderBg = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));

                _cardArtBorder.Background = placeholderBg;
                _cardArtPlaceholder.Visibility = Visibility.Visible;
                _cardAmbientHalo.Background = Brushes.Transparent;
                _cardAmbientBackdrop.Background = Brushes.Transparent;

                _barArtBorder.Background = placeholderBg;
                _barArtPlaceholder.Visibility = Visibility.Visible;
                _barAmbientHalo.Background = Brushes.Transparent;

                _microArtBorder.Background = placeholderBg;
                _microArtPlaceholder.Visibility = Visibility.Visible;
                _microAmbientHalo.Background = Brushes.Transparent;
            }
        }

        // ================= ONE UI 9 COLOR THEME LOGIC =================
        private void ApplyThemeColors(Color accent, Color bgTint)
        {
            _accentColor = accent;
            _bgTintColor = bgTint;

            UpdateRootBrushes();

            SolidColorBrush accentBrush = new SolidColorBrush(accent);
            SolidColorBrush trackBgBrush = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B));

            // Dynamic Progress Fill (Vibrant Gradient)
            LinearGradientBrush fillBrush = new LinearGradientBrush();
            fillBrush.StartPoint = new Point(0, 0);
            fillBrush.EndPoint = new Point(1, 0);
            fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(220, accent.R, accent.G, accent.B), 0.0));
            fillBrush.GradientStops.Add(new GradientStop(accent, 1.0));

            _cardProgressFill.Background = fillBrush;
            _cardProgressTrack.Background = trackBgBrush;
            _cardProgressThumb.Background = accentBrush;
            _cardProgressThumb.Effect = new DropShadowEffect
            {
                Color = accent,
                BlurRadius = 10,
                Opacity = 0.6,
                ShadowDepth = 0
            };

            _barProgressFill.Background = fillBrush;
            _barProgressTrack.Background = trackBgBrush;
            _barProgressThumb.Background = accentBrush;

            // Tint the Play button with album accent
            if (_cardPlayBorder != null)
            {
                _cardPlayBorder.Background = accentBrush;
                _cardPlayBorder.Effect = new DropShadowEffect
                {
                    Color = accent,
                    BlurRadius = 14,
                    Opacity = 0.4,
                    ShadowDepth = 0
                };
            }
            if (_barPlayBorder != null)
                _barPlayBorder.Background = accentBrush;
            if (_microPlayBorder != null)
                _microPlayBorder.Background = accentBrush;

            // Invert play button icon color for high contrast
            double lum = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B);
            Brush iconBrush = (lum > 140) ? new SolidColorBrush(Color.FromRgb(18, 18, 22)) : Brushes.White;
            _cardPlayPauseBtn.Foreground = iconBrush;
            _barPlayPauseBtn.Foreground = iconBrush;
            _microPlayPauseBtn.Foreground = iconBrush;
        }

        private static Color ExtractVibrantColor(byte[] imageBytes)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    BitmapFrame frame = decoder.Frames[0];
                    FormatConvertedBitmap converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                    int width = converted.PixelWidth;
                    int height = converted.PixelHeight;

                    int stride = width * 4;
                    byte[] pixels = new byte[height * stride];
                    converted.CopyPixels(pixels, stride, 0);

                    long bestScore = -1;
                    Color bestColor = Color.FromRgb(255, 255, 255);

                    int stepX = Math.Max(1, width / 18);
                    int stepY = Math.Max(1, height / 18);

                    for (int y = 0; y < height; y += stepY)
                    {
                        for (int x = 0; x < width; x += stepX)
                        {
                            int idx = y * stride + x * 4;
                            byte b = pixels[idx];
                            byte g = pixels[idx + 1];
                            byte r = pixels[idx + 2];
                            byte a = pixels[idx + 3];

                            if (a < 128) continue;

                            int max = Math.Max(r, Math.Max(g, b));
                            int min = Math.Min(r, Math.Min(g, b));
                            int delta = max - min;

                            if (max > 60 && max < 250 && delta > 25)
                            {
                                long score = (long)delta * max;
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestColor = Color.FromRgb(r, g, b);
                                }
                            }
                        }
                    }

                    if (bestScore > 0)
                        return bestColor;
                }
            }
            catch {}
            return Color.FromRgb(255, 255, 255);
        }

        private static Color DeriveDarkBg(Color accent)
        {
            byte r = (byte)(accent.R * 0.12 + 16);
            byte g = (byte)(accent.G * 0.12 + 16);
            byte b = (byte)(accent.B * 0.12 + 22);
            return Color.FromRgb(r, g, b);
        }

        // ================= MEDIA LOGIC =================
        private void TriggerPlayPause()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                bool handled = false;
                try
                {
                    var mgr = Await(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                    var session = mgr != null ? mgr.GetCurrentSession() : null;
                    if (session != null)
                        handled = Await(session.TryTogglePlayPauseAsync());
                }
                catch {}

                if (!handled)
                    SendKey(VK_MEDIA_PLAY_PAUSE);

                Thread.Sleep(200);
                QueryMediaInfo();
            });
        }

        private void TriggerSkipNext()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                bool handled = false;
                try
                {
                    var mgr = Await(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                    var session = mgr != null ? mgr.GetCurrentSession() : null;
                    if (session != null)
                        handled = Await(session.TrySkipNextAsync());
                }
                catch {}

                if (!handled)
                    SendKey(VK_MEDIA_NEXT_TRACK);

                Thread.Sleep(300);
                QueryMediaInfo();
            });
        }

        private void TriggerSkipPrev()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                bool handled = false;
                try
                {
                    var mgr = Await(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                    var session = mgr != null ? mgr.GetCurrentSession() : null;
                    if (session != null)
                        handled = Await(session.TrySkipPreviousAsync());
                }
                catch {}

                if (!handled)
                    SendKey(VK_MEDIA_PREV_TRACK);

                Thread.Sleep(300);
                QueryMediaInfo();
            });
        }

        private void SetAmbientMode(bool enabled)
        {
            _ambientEnabled = enabled;
            Visibility v = enabled ? Visibility.Visible : Visibility.Collapsed;

            _cardAmbientHalo.Visibility = v;
            _cardAmbientBackdrop.Visibility = v;
            _barAmbientHalo.Visibility = v;
            _microAmbientHalo.Visibility = v;
        }

        // ================= LUXURY BUTTON CREATION =================
        private static Button FindParentButton(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                Button btn = parent as Button;
                if (btn != null) return btn;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private UIElement CreateDivider()
        {
            return new Rectangle
            {
                Width = 1,
                Height = 18,
                Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private Button CreateLuxuryPlayButton(RoutedEventHandler onClick, out Border borderRef, double size = 42, double fontSize = 15)
        {
            Button btn = new Button
            {
                Content = "▶",
                Width = size,
                Height = size,
                Margin = new Thickness(6, 0, 6, 0),
                Focusable = false,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                Cursor = Cursors.Hand,
                ToolTip = "Phát / Tạm dừng"
            };

            Border b = new Border
            {
                CornerRadius = new CornerRadius(size / 2),
                Background = Brushes.White,
                Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 12,
                    Opacity = 0.25,
                    ShadowDepth = 0
                }
            };
            borderRef = b;

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "playBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(size / 2));
            border.SetValue(Border.BackgroundProperty, Brushes.White);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            template.VisualTree = border;

            Trigger mouseOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(Border.OpacityProperty, 0.85, "playBorder"));
            template.Triggers.Add(mouseOver);

            Trigger isPressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            isPressed.Setters.Add(new Setter(Border.OpacityProperty, 0.7, "playBorder"));
            template.Triggers.Add(isPressed);

            btn.Template = template;
            btn.Click += onClick;
            return btn;
        }

        private Button CreateIconButton(string text, string tooltip, RoutedEventHandler onClick, double size = 28, double fontSize = 12, bool isClose = false)
        {
            Button btn = new Button
            {
                Content = text,
                Width = size,
                Height = size,
                Margin = new Thickness(1.5, 0, 1.5, 0),
                Focusable = false,
                FontSize = fontSize,
                Foreground = Brushes.White,
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "btnBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(size / 2));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            template.VisualTree = border;

            Trigger mouseOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            if (isClose)
            {
                mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(190, 225, 29, 72)), "btnBorder"));
            }
            else
            {
                mouseOver.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), "btnBorder"));
            }
            template.Triggers.Add(mouseOver);

            Trigger isPressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            isPressed.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(85, 255, 255, 255)), "btnBorder"));
            template.Triggers.Add(isPressed);

            btn.Template = template;
            btn.Click += onClick;
            return btn;
        }

        private ContextMenu CreateContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem modeCard = new MenuItem { Header = "✦ Chế độ: Thẻ lớn (Card)" };
            modeCard.Click += (s, e) => { Width = 276; Height = 380; SetLayoutState(LayoutState.Card, true); };

            MenuItem modeBar = new MenuItem { Header = "✦ Chế độ: Thanh ngang (Bar)" };
            modeBar.Click += (s, e) => { Width = 390; Height = 64; SetLayoutState(LayoutState.Bar, true); };

            MenuItem modeMicro = new MenuItem { Header = "✦ Chế độ: Bong bóng mini (Micro)" };
            modeMicro.Click += (s, e) => ShrinkToMicro();

            MenuItem lockItem = new MenuItem { Header = _isLocked ? "✦ Khóa vị trí: ĐANG BẬT 📌" : "✦ Khóa vị trí: ĐANG TẮT" };
            lockItem.Click += (s, e) =>
            {
                ToggleLock();
                lockItem.Header = _isLocked ? "✦ Khóa vị trí: ĐANG BẬT 📌" : "✦ Khóa vị trí: ĐANG TẮT";
            };

            MenuItem ambientToggle = new MenuItem { Header = _ambientEnabled ? "✦ Ánh sáng tỏa (Ambient): ĐANG BẬT" : "✦ Ánh sáng tỏa (Ambient): ĐANG TẮT" };
            ambientToggle.Click += (s, e) =>
            {
                SetAmbientMode(!_ambientEnabled);
                ambientToggle.Header = _ambientEnabled ? "✦ Ánh sáng tỏa (Ambient): ĐANG BẬT" : "✦ Ánh sáng tỏa (Ambient): ĐANG TẮT";
            };

            MenuItem op100 = new MenuItem { Header = "Độ mờ: 100% (Rõ hoàn toàn)" };
            op100.Click += (s, e) => this.Opacity = 1.0;

            MenuItem op85 = new MenuItem { Header = "Độ mờ: 85% (Mặc định)" };
            op85.Click += (s, e) => this.Opacity = 0.85;

            MenuItem op65 = new MenuItem { Header = "Độ mờ: 65% (Trong suốt nhẹ)" };
            op65.Click += (s, e) => this.Opacity = 0.65;

            MenuItem op45 = new MenuItem { Header = "Độ mờ: 45% (Mờ ảo)" };
            op45.Click += (s, e) => this.Opacity = 0.45;

            MenuItem resetPos = new MenuItem { Header = "Đưa về vị trí mặc định" };
            resetPos.Click += (s, e) =>
            {
                Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
                Top = 30;
            };

            MenuItem exit = new MenuItem { Header = "Thoát ứng dụng" };
            exit.Click += (s, e) => Close();

            menu.Items.Add(modeCard);
            menu.Items.Add(modeBar);
            menu.Items.Add(modeMicro);
            menu.Items.Add(new Separator());
            menu.Items.Add(lockItem);
            menu.Items.Add(ambientToggle);
            menu.Items.Add(new Separator());
            menu.Items.Add(op100);
            menu.Items.Add(op85);
            menu.Items.Add(op65);
            menu.Items.Add(op45);
            menu.Items.Add(new Separator());
            menu.Items.Add(resetPos);
            menu.Items.Add(new Separator());
            menu.Items.Add(exit);

            return menu;
        }
    }
}
