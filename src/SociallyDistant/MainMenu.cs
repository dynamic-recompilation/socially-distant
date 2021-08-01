﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gtk;
using SociallyDistant.Connectivity;
using SociallyDistant.Core;
using SociallyDistant.Core.Config;
using SociallyDistant.Core.ContentEditors;
using SociallyDistant.Core.Gui.Elements;
using SociallyDistant.Core.SaveData;
using SociallyDistant.Core.Windowing;
using Thundershock;
using Thundershock.Audio;
using Thundershock.Core;
using Thundershock.Core.Input;
using Thundershock.Core.Rendering;
using Thundershock.Gui;
using Thundershock.Gui.Elements;
using Action = System.Action;
using Button = Thundershock.Gui.Elements.Button;

namespace SociallyDistant
{
    public class MainMenu : Scene
    {
        private static bool _isFirstDisplay = false;

        #region State

        private Stack<int> _history = new();
        private List<SaveSlot> _saves = new();
        private int _menuState = 0;
        private InstalledContentPack _pack;
        private SettingsWindow _settingsWindow;
        
        #endregion
        
        #region App References

        private AnnouncementManager _announcement;
        private ContentManager _packManager;
        private SaveManager _saveManager;
        
        #endregion
        
        #region UI elements

        private Button _menuBack = new();
        private Panel _sidebarOverlay = new();
        private Stacker _mainMenuInterface = new();
        private Stacker _packStacker = new();
        private Stacker _packInfoStacker = new();
        private TextBlock _menuTitle = new();
        private TextBlock _menuDescription = new();
        private Picture _mainLogo = new();
        private Picture _packLogo = new();
        private Stacker _menuStack = new();
        private ScrollPanel _menuScroller = new();
        private Picture _announcementFeaturedImage = new();
        private TextBlock _announcementTitle = new();
        private TextBlock _announcementText = new();
        private Button _announcementReadMore = new();
        private Stacker _announcementStacker = new();
        private Stacker _masterStacker = new();

        #endregion

        #region Systems

        private WindowManager _wm;

        #endregion
        
        protected override void OnLoad()
        {
            _announcement = Game.GetComponent<AnnouncementManager>();
            _packManager = Game.GetComponent<ContentManager>();
            _saveManager = Game.GetComponent<SaveManager>();
            
            BuildGui();
            
            _wm = RegisterSystem<WindowManager>();

            StyleGui();
            BindEvents();
            
            UpdateMenuState();
            
            if (_isFirstDisplay)
            {
                _isFirstDisplay = false;
            }

            if (_saveManager.PreloadException != null)
            {
                var ex = _saveManager.PreloadException;
                _saveManager.DisarmPreloaderCrash();

                #if DEBUG
                _wm.ShowMessage("Corrupt Data",
                    "An error prevented the career save file from loading. This is due to possibly corrupt world information." +
                    Environment.NewLine + Environment.NewLine + "You have been returned to the main menu." +
                    Environment.NewLine + Environment.NewLine + ex.ToString());
#else
                _wm.ShowMessage("Corrupt Data",
                    "An error prevented the career save file from loading. This is due to possibly corrupt world information. (403)" + Environment.NewLine + Environment.NewLine + "You have been returned to the main menu.");
#endif
            }
            
            base.OnLoad();
        }

        protected override void OnUpdate(GameTime gameTime)
        {
            if (_announcement.IsReady)
            {
                _announcementStacker.Visibility = Visibility.Visible;

                _announcementTitle.Text = _announcement.Announcement.Title;
                _announcementText.Text = _announcement.Announcement.Excerpt;
            }
            else
            {
                _announcementStacker.Visibility = Visibility.Hidden;
            }
            
            base.OnUpdate(gameTime);
        }

        #region Private Methods

        private void BuildGui()
        {
            _sidebarOverlay.FixedWidth = 404;
            
            _packInfoStacker.Children.Add(_menuTitle);
            _packInfoStacker.Children.Add(_menuDescription);
            
            _packStacker.Children.Add(_packLogo);
            _packStacker.Children.Add(_packInfoStacker);
            
            _packStacker.Direction = StackDirection.Horizontal;

            _menuScroller.Children.Add(_menuStack);
            
            _menuScroller.Properties.SetValue(Stacker.FillProperty, StackFill.Fill);
            
            _mainMenuInterface.Children.Add(_mainLogo);
            _mainMenuInterface.Children.Add(_packStacker);
            _mainMenuInterface.Children.Add(_menuScroller);

            _sidebarOverlay.Children.Add(_mainMenuInterface);
            
            _announcementStacker.Children.Add(_announcementTitle);
            _announcementStacker.Children.Add(_announcementText);
            _announcementStacker.Children.Add(_announcementFeaturedImage);
            _announcementStacker.Children.Add(_announcementReadMore);
            
            _announcementStacker.Properties.SetValue(Stacker.FillProperty, StackFill.Fill);
            
            _masterStacker.Direction = StackDirection.Horizontal;

            _masterStacker.Children.Add(_sidebarOverlay);
            _masterStacker.Children.Add(_announcementStacker);
            
            Gui.AddToViewport(_masterStacker);
        }

        private void StyleGui()
        {
            _mainMenuInterface.Padding = 45;

            _menuTitle.Properties.SetValue(FontStyle.Heading2);

            _packInfoStacker.VerticalAlignment = VerticalAlignment.Center;
            _packLogo.VerticalAlignment = VerticalAlignment.Center;
            _packLogo.Padding = 2;
            _packInfoStacker.Padding = 2;
            
            _packLogo.FixedWidth = 48;
            _packLogo.FixedHeight = 48;

            _menuScroller.Padding = new Padding(0, 45, 0, 0);
            
            _menuBack.Text = " << Back";
            _menuBack.Padding = new Padding(0, 7.5f, 0, 0);
            _menuBack.HorizontalAlignment = HorizontalAlignment.Left;
            
            _announcementTitle.Properties.SetValue(FontStyle.Heading1);
            _announcementTitle.ForeColor = Color.Cyan;

            _announcementText.Padding = new Padding(0, 0, 0, 4);

            _announcementReadMore.Text = "Read More...";
            _announcementReadMore.HorizontalAlignment = HorizontalAlignment.Left;

            _announcementStacker.VerticalAlignment = VerticalAlignment.Center;
            _announcementStacker.Padding = 75;

            _mainLogo.Image = Texture2D.FromResource(Graphics, this.GetType().Assembly,
                "SociallyDistant.Resources.LogoText.png");
        }

        private void BindEvents()
        {
            _announcementReadMore.MouseUp += AnnouncementReadMoreOnMouseUp;
            _menuBack.MouseUp += MenuBackOnMouseUp;
        }
        
        private void ClearMenu()
        {
            _menuStack.Children.Clear();
        }

        private void AddMenuItem(string title, string description, Texture2D icon, Action action)
        {
            var button = new AdvancedButton();
            var hStacker = new Stacker();
            var vStacker = new Stacker();
            var nameText = new TextBlock();
            var descText = new TextBlock();
            var iconImage = new Picture();

            nameText.ForeColor = Color.Cyan;
            nameText.Properties.SetValue(FontStyle.Code);
            
            iconImage.Image = icon;
            iconImage.FixedWidth = 24;
            iconImage.FixedHeight = 24;
            
            nameText.Text = title.ToUpperInvariant();
            descText.Text = description;
            
            vStacker.Children.Add(nameText);
            vStacker.Children.Add(descText);

            hStacker.Padding = 3;
            vStacker.Padding = 2;
            iconImage.Padding = 2;
            
            iconImage.VerticalAlignment = VerticalAlignment.Center;
            vStacker.VerticalAlignment = VerticalAlignment.Center;
            
            vStacker.Properties.SetValue(Stacker.FillProperty, StackFill.Fill);
            hStacker.Direction = StackDirection.Horizontal;
            
            hStacker.Children.Add(iconImage);
            hStacker.Children.Add(vStacker);
            
            button.Children.Add(hStacker);

            button.MouseUp += (_, a) =>
            {
                if (a.Button == MouseButton.Primary)
                {
                    action?.Invoke();
                }
            };

            _menuStack.Children.Add(button);
        }

        private void UpdateMenuState()
        {
            ClearMenu();

            if (_menuState == 0)
            {
                _mainLogo.Visibility = Visibility.Visible;
                _packStacker.Visibility = Visibility.Collapsed;
            }
            else
            {
                _mainLogo.Visibility = Visibility.Collapsed;
                _packStacker.Visibility = Visibility.Visible;
            }
            
            switch (_menuState)
            {
                case 0:

                    if (_packManager.HasCareerMode)
                    {
                        AddMenuItem("Career", _packManager.CareerPack.Description, _packManager.CareerPack.Icon, () =>
                        {
                            SelectPack(_packManager.CareerPack);
                        });
                    }

                    AddMenuItem("More Stories", "Play through custom stories made by the Socially Distant Community.",
                        null,
                        () =>
                        {
                            _history.Push(_menuState);
                            _menuState = 1;
                            UpdateMenuState();
                        });
                    AddMenuItem("Settings", "Adjust the game's settings.", null, OpenSettings);
                    AddMenuItem("Quit", "Exit the game", null, Game.Exit);
                    
                    break;
                case 1:
                    _menuTitle.Text = "More Stories";
                    _menuDescription.Text = "Play through custom stories made by the Socially Distant Community.";

                    _packLogo.Image = null;

                    foreach (var pack in _packManager.InstalledPacks)
                    {
                        AddMenuItem(pack.Name, pack.Author, pack.Icon, () =>
                        {
                            SelectPack(pack);
                        });
                    }
                    
                    break;
                case 2:
                    _packLogo.Image = _pack.Icon;
                    _menuTitle.Text = _pack.Name;
                    _menuDescription.Text = _pack.Description;

                    AddMenuItem("New Game", "Start a new career.", null, () =>
                    {
                        _saveManager.NewGame(_pack);
                        GoToScene<BootScreen>();
                    });

                    if (_saves.Any())
                    {
                        var first = _saves.First();
                        AddMenuItem($"Continue ({first.Title})",
                            "Last played: " + first.LastPlayed.ToShortDateString() + " " +
                            first.LastPlayed.ToShortTimeString(), null,
                            () =>
                            {
                                try
                                {
                                    _saveManager.LoadGame(first);
                                }
                                catch (Exception ex)
                                {
                                    #if DEBUG
                                    _wm.ShowMessage("Corrupt Data",
                                        "An error prevented the career save file from loading. This is possibly due to a corrupt, outdated, or unsupported save file." +
                                        Environment.NewLine + Environment.NewLine + ex.ToString());
                                    #else
                                    _wm.ShowMessage("Corrupt Data",
                                        "An error prevented the career save file from loading. This is possibly due to a corrupt, outdated, or unsupported save file." +
                                        Environment.NewLine + Environment.NewLine + ex.Message);
#endif
                                }

                                GoToScene<BootScreen>();
                            });
                        
                        AddMenuItem("Load Game", "Select a different save file.", null, () =>
                        {
                            _history.Push(_menuState);
                            _menuState = 3;
                            UpdateMenuState();
                        });
                    }

                    break;
                case 3:
                    _packLogo.Image = _pack.Icon;
                    _menuTitle.Text = _pack.Name;
                    _menuDescription.Text = _pack.Description;

                    foreach (var save in _saves.OrderByDescending(x=>x.LastPlayed))
                    {
                        AddMenuItem($"Continue ({save.Title})",
                            "Last played: " + save.LastPlayed.ToShortDateString() + " " +
                            save.LastPlayed.ToShortTimeString(), null,
                            () =>
                            {
                                _history.Pop();
                                _saves.Remove(save);
                                _saves.Insert(0, save);
                                _menuState = 2;
                                UpdateMenuState();
                            });
                    }
                    
                    break;
            }

            if (_menuState != 0)
            {
                _menuStack.Children.Add(_menuBack);
            }
        }

        private void SelectPack(InstalledContentPack pack)
        {
            _history.Push(_menuState);
            _pack = pack;

            if (_pack == _packManager.CareerPack)
            {
                _saves = _saveManager.SaveDatabase.CareerSlots.ToList();
            }
            else
            {
                _saves = _saveManager.SaveDatabase.GetExtensionSaves(_pack).ToList();
            }
            
            _menuState = 2;
            UpdateMenuState();
        }
        
        #endregion

        #region Event Handlers

        private void MenuBackOnMouseUp(object? sender, MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Primary)
            {
                if (_history.Any())
                {
                    _menuState = _history.Pop();
                }
                else
                {
                    _menuState = 0;
                }

                UpdateMenuState();
            }
        }

        
        private void AnnouncementReadMoreOnMouseUp(object? sender, MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Primary)
            {
                ThundershockPlatform.OpenFile(_announcement.Announcement.Link);
            }
        }

        private void OpenSettings()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = _wm.OpenWindow<SettingsWindow>();
                _settingsWindow.WindowClosed += (_, _) =>
                {
                    _settingsWindow = null;
                };
            }
        }
        
        #endregion
        
        
        #region Static Methods
        
        internal static void ArmFirstDisplay()
        {
            _isFirstDisplay = true;
        }
        
        #endregion
    }
}