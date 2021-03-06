using System;
using System.Linq;
using System.Numerics;
using SociallyDistant.Core;
using SociallyDistant.Core.Game;
using SociallyDistant.Core.Mail;
using SociallyDistant.Core.SaveData;
using SociallyDistant.Gui;
using SociallyDistant.Gui.Windowing;
using SociallyDistant.Online.CommunityAnnouncements;
using SociallyDistant.Shell;
using SociallyDistant.Shell.Displays;
using SociallyDistant.Shell.Notifications;
using SociallyDistant.Shell.Windows;
using Thundershock;
using Thundershock.Core;
using Thundershock.Core.Input;
using Thundershock.Gui;
using Thundershock.Gui.Elements;
using Thundershock.Gui.Elements.Console;

namespace SociallyDistant.Scenes
{
    public sealed class Workspace : Scene
    {
        #region APP REFERENCES

        private AnnouncementManager _announcement;
        private SaveManager _saveManager;

        #endregion

        #region SCENE COMPONENTS

        private DisplayManager _displayManager;
        private WindowManager _windowManager;
        private Shell.Shell _shell;

        #endregion
        
        #region USER INTERFACE

        // background.
        private Panel _wallpaperPanel = new();
        private Picture _wallpaperDisplay = new();
        
        // dynamic UI
        private Stacker _launcherList = new();
        
        // main UI
        private Stacker _infoLeft = new();
        private Panel _infoBanner = new();
        private Stacker _infoMaster = new();
        private Stacker _infoProfileCard = new();
        private Picture _playerAvatar = new();
        private TextBlock _playerName = new();
        private Stacker _playerInfoStacker = new();
        private Stacker _infoRight = new();
        private Button _settings = new();
        private ConsoleControl _console = new();
        private WindowFrame _terminalsPanel;
        private WindowFrame _sidePanel;

        // notification UI
        private Panel _notificationBanner = new();
        private TextBlock _noteTitle = new();
        private TextBlock _noteMessage = new();
        private WrapPanel _noteButtonWrapper = new();
        private Picture _noteIcon = new();
        private Stacker _noteStacker = new();
        private Stacker _noteInfoStacker = new();
        private Mailbox _playerMailbox;
        
        #endregion
        
        #region STATE

        private int _noteState = 0;
        private float _noteTransition = 0;
        private double _noteTimer = 0;
        private bool _noteAutoDismiss = false;
        private TimeSpan _uptime;
        private TimeSpan _frameTime;
        private IProgramContext _context;
        private ColorPalette _palette;
        
        #endregion

        #region WINDOWS

        private SettingsWindow _settingsWindow;

        #endregion
        
        #region PROPERTIES

        public TimeSpan Uptime => _uptime;
        public TimeSpan FrameTime => _frameTime;

        #endregion
        
        protected override void OnLoad()
        {
            // Turn off FXAA.
            PrimaryCameraSettings.EnableFXAA = false;
            
            // Grab app references.
            _announcement = AnnouncementManager.Instance;
            _saveManager = SaveManager.Instance;

            // Window manager.
            _windowManager = RegisterSystem<WindowManager>();

            // Build the workspace GUI.
            BuildGui();

            // Load the redconf state.
            LoadConfig();
            
            // Style the GUI.
            StyleGui();
            
            // Start the command shell.
            StartShell();
            
            // Refresh dynamic UI elements.
            this.RefreshDynamicGui();
            
            base.OnLoad();

            _settings.MouseUp += SettingsOnMouseUp;

            CheckForAnnouncement();
        }

        private void SettingsOnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Primary)
            {
                if (_settingsWindow == null)
                {
                    _settingsWindow = _windowManager.OpenWindow<SettingsWindow>(WindowStyle.Dialog);
                    _settingsWindow.WindowClosed += SettingsWindowOnWindowClosed;
                }
            }
        }

        private void SettingsWindowOnWindowClosed(object sender, EventArgs e)
        {
            _settingsWindow = null;
        }

        protected override void OnUpdate(GameTime gameTime)
        {
            // Try to find the player mailbox if we lack it.
            if (_playerMailbox == null)
            {
                var playerEntities = Registry.View<PlayerState>();
                if (playerEntities.Any())
                {
                    var first = playerEntities.First();
                    var mailbox = Registry.GetComponent<Mailbox>(first);
                    _playerMailbox = mailbox;
                }
            }
            
            // Check for in-coming email messages.
            if (_playerMailbox != null)
            {
                ReadMail();
            }
            
            _frameTime = gameTime.ElapsedGameTime;
            _uptime = gameTime.TotalGameTime;

            _playerName.Text = _saveManager.CurrentGame.PlayerName;

            if (_noteState == 0)
            {
                if (NotificationManager.TryGetNotification(out var note))
                {
                    _noteTitle.Text = note.Title;
                    _noteMessage.Text = note.Message;
                    _noteIcon.Image = note.Icon;

                    _noteIcon.Visibility = note.Icon == null ? Visibility.Collapsed : Visibility.Visible;

                    _noteButtonWrapper.Children.Clear();

                    _noteTransition = 0;
                    _noteTimer = note.Time;
                    _noteAutoDismiss = _noteTimer > 0;

                    if (!_noteAutoDismiss && !note.Actions.Any())
                    {
                        note.AddButton("OK");
                    }
                    
                    foreach (var key in note.Actions.Keys)
                    {
                        var action = note.Actions[key];
                        
                        var btn = new Button();
                        btn.Text = key;
                        btn.MouseUp += (o, a) =>
                        {
                            if (_noteState == 2)
                            {
                                if (a.Button == MouseButton.Primary)
                                {
                                    action?.Invoke();

                                    _noteState = 3;
                                }
                            }
                        };

                        btn.Padding = new Padding(0, 1, 2, 1);

                        _noteButtonWrapper.Children.Add(btn);
                    }

                    _noteState = 1;
                    Gui.AddToViewport(_notificationBanner);
                }
            }
            
            switch (_noteState)
            {
                case 1:
                    _noteTransition =
                        MathHelper.Clamp(_noteTransition + (float) gameTime.ElapsedGameTime.TotalSeconds * 4, 0, 1);

                    _notificationBanner.Opacity = _noteTransition;
                    _notificationBanner.ViewportPosition = new Vector2(0, 0 - (200 * (1 - _noteTransition)));

                    if (_noteTransition >= 1)
                    {
                        _noteState++;
                    }
                    
                    break;
                case 2:
                    if (_noteAutoDismiss)
                    {
                        _noteTimer -= gameTime.ElapsedGameTime.TotalSeconds;
                        if (_noteTimer <= 0)
                        {
                            _noteState++;
                        }
                    }
                    break;
                case 3:
                    _noteTransition =
                        MathHelper.Clamp(_noteTransition - (float) gameTime.ElapsedGameTime.TotalSeconds * 4, 0, 1);

                    _notificationBanner.Opacity = _noteTransition;
                    _notificationBanner.ViewportPosition = new Vector2(0, 0 - (200 * (1 - _noteTransition)));

                    if (_noteTransition <= 0)
                    {
                        _noteState = 0;
                        _notificationBanner.RemoveFromParent();
                    }

                    break;
            }
            
            base.OnUpdate(gameTime);
        }

        private void ReadMail()
        {
            if (_playerMailbox.TryGetUnreadMessage(out var unread))
            {
                var note = NotificationManager.CreateNotification("Email received.", unread.Message.Subject, 5);
                    
                note.AddButton("View", () =>
                {
                    OpenMail(unread);
                });
                note.AddButton("Dismiss");
            }
        }

        
        private void RedConfOnConfigUpdated(object sender, EventArgs e)
        {
            LoadConfig();
            StyleGui();
        }

        private void LoadConfig()
        {
        }
        
        private void StartShell()
        {
            // Start the display manager.
            var h = Gui.GetScaledHeight(ViewportBounds.Height);
            _displayManager = RegisterSystem<DisplayManager>();
            _displayManager.DisplayAnchor = new FreePanel.CanvasAnchor(0.17f, 28f / h, 0.83f, 0.7f - (28f / h));
            
            // Start the game's simulation.
            var simulation = RegisterSystem<Simulation>();
            
            // With the simulation started, we can start Mailer.
            RegisterSystem<MailboxManager>();

            // Register the shell as a system.
            _shell = RegisterSystem<Shell.Shell>();

            // Attach a shell to the player entity.
            var playerEntity = simulation.GetPlayerEntity();
            Registry.AddComponent(playerEntity, (IConsole) _console);
            Registry.AddComponent(playerEntity, new ShellStateComponent
            {
                UserId = 1 // uses the player's  normal user account instead of root.
            });
        }
        
        private void BuildGui()
        {
            _wallpaperPanel.Children.Add(_wallpaperDisplay);
            Gui.AddToViewport(_wallpaperPanel);

            _terminalsPanel = _windowManager.CreateFloatingPane("Terminal", WindowStyle.Tile);
            _sidePanel = _windowManager.CreateFloatingPane("Untitled", WindowStyle.Tile);

            _noteInfoStacker.Children.Add(_noteTitle);
            _noteInfoStacker.Children.Add(_noteMessage);
            _noteInfoStacker.Children.Add(_noteButtonWrapper);
            _noteStacker.Children.Add(_noteIcon);
            _noteStacker.Children.Add(_noteInfoStacker);
            _notificationBanner.Children.Add(_noteStacker);
            
            _playerInfoStacker.Children.Add(_playerName);
            
            _infoProfileCard.Children.Add(_playerAvatar);
            _infoProfileCard.Children.Add(_playerInfoStacker);

            _infoLeft.Children.Add(_launcherList);
            
            _infoRight.Children.Add(_settings);
            _infoRight.Children.Add(_infoProfileCard);

            _infoMaster.Children.Add(_infoLeft);
            _infoMaster.Children.Add(_infoRight);

            _infoBanner.Children.Add(_infoMaster);

            _terminalsPanel.Content.Add(_console);
            
            Gui.AddToViewport(_infoBanner);
        }

        private void StyleGui()
        {
            // Wallpapers are weird.
            _wallpaperDisplay.ImageMode = ImageMode.Zoom;
            
            // Destroy the previous wallpaper if there's one loaded.
            if (_wallpaperDisplay.Image != null)
            {
                _wallpaperDisplay.Image.Dispose();
                _wallpaperDisplay.Image = null;
            }

            // Set the alignments of the tiles.
            _terminalsPanel.ViewportAlignment = Vector2.Zero;
            _sidePanel.ViewportAlignment = Vector2.Zero;

            // Set viewport anchors for the desktop UIs.
            var h = Gui.GetScaledHeight(ViewportBounds.Height);
            _infoBanner.ViewportAnchor = new FreePanel.CanvasAnchor(0, 0, 1, 0);
            _terminalsPanel.ViewportAnchor = new FreePanel.CanvasAnchor(0.17f, 0.7f, 0.83f, 0.3f);
            _sidePanel.ViewportAnchor = new FreePanel.CanvasAnchor(0, 28f / h, 0.17f, 1 - (28f / h));

            // If the display manager's been started, set its anchor.
            if (_displayManager != null)
            {
                _displayManager.DisplayAnchor = new FreePanel.CanvasAnchor(0.17f, 28f / h, 0.83f, 0.7f - (28f / h));
            }


            
            // Fixed height for the status panell.
            _infoBanner.FixedHeight = 28;
            
            _sidePanel.BackColor = Color.Green;
            _terminalsPanel.BackColor = Color.Transparent;

            _notificationBanner.ViewportAnchor = new FreePanel.CanvasAnchor(0.5f, 0, 0, 0);
            _notificationBanner.ViewportAlignment = new Vector2(0.5f, 0);
            _notificationBanner.FixedWidth = 460;
            
            _noteIcon.ImageMode = ImageMode.Rounded;
            _noteIcon.FixedHeight = 24;
            _noteIcon.FixedWidth = 24;
            _noteIcon.VerticalAlignment = VerticalAlignment.Top;

            _noteTitle.Properties.SetValue(FontStyle.Heading3);
            _noteTitle.ForeColor = Color.Cyan;

            _noteStacker.Direction = StackDirection.Horizontal;
            _noteButtonWrapper.Orientation = StackDirection.Horizontal;

            _noteStacker.Padding = 10;
            _noteIcon.Padding = 2;
            _noteInfoStacker.Padding = 2;
            _noteButtonWrapper.Padding = new Padding(0, 4, 0, 0);
            
            _settings.Text = "Settings";
            
            _playerAvatar.VerticalAlignment = VerticalAlignment.Center;
            _playerInfoStacker.VerticalAlignment = VerticalAlignment.Center;
            _settings.VerticalAlignment = VerticalAlignment.Center;

            _settings.Padding = new Padding(4, 0, 4, 0);
            
            _playerAvatar.FixedWidth = 24;
            _playerAvatar.FixedHeight = 24;
            _playerAvatar.ImageMode = ImageMode.Rounded;

            _infoMaster.Direction = StackDirection.Horizontal;
            _infoProfileCard.Direction = StackDirection.Horizontal;
            _infoRight.Direction = StackDirection.Horizontal;
            _infoLeft.Direction = StackDirection.Horizontal;

            _infoRight.HorizontalAlignment = HorizontalAlignment.Right;
            
            // Fills.
            _console.Properties.SetValue(Stacker.FillProperty, StackFill.Fill);
            _infoRight.Properties.SetValue(Stacker.FillProperty, StackFill.Fill);
            _infoLeft.Properties.SetValue(Stacker.FillProperty, StackFill.Fill);

            _playerInfoStacker.Padding = new Padding(3, 0, 0, 0);
            _infoMaster.Padding = new Padding(4, 2, 4, 2);

            _console.DrawBackgroundImage = false;

            _launcherList.Direction = StackDirection.Horizontal;
            _launcherList.VerticalAlignment = VerticalAlignment.Center;
            
        }

        private void OpenMail(UnreadEmail message)
        {
            var ctx = _shell.CreatePlayerContext();
            var mailViewer = _displayManager.OpenDisplay<MailViewer>(ctx);
        }
        
        private void TrySave(IConsole console)
        {
            _saveManager.Save();
            _console.WriteLine($"&b * save successful * &B");
        }

        private void RefreshDynamicGui()
        {
            _launcherList.Children.Clear();

            foreach (var launcher in _displayManager.GetLaunchers())
            {
                var icon = new Picture();
                icon.Image = launcher.Icon;
                icon.FixedWidth = 24;
                icon.FixedHeight = 24;
                icon.Padding = new Padding(0, 0, 2, 0);

                icon.Tint = Color.White * 0.8f;
                icon.MouseEnter += (o, a) =>
                {
                    icon.Tint = Color.White;
                };
                icon.MouseLeave += (o, a) =>
                {
                    icon.Tint = Color.White * 0.8f;
                };

                icon.ToolTip = launcher.Name;

                icon.MouseUp += (o, a) =>
                {
                    if (a.Button == MouseButton.Primary)
                    {
                        launcher.Open(_shell.CreatePlayerContext());
                    }
                };

                icon.IsInteractable = true;
                
                _launcherList.Children.Add(icon);
            }
        }

        private void CheckForAnnouncement()
        {
            if (_announcement.IsReady)
            {
                _displayManager.OpenDisplay<AnnouncementDisplay>(_shell.CreatePlayerContext());
            }
        }
    }
}