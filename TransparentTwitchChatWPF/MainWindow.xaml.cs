﻿using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using CefSharp;
using CefSharp.Wpf;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Interop;
using System.Collections.Specialized;
using Jot;
using Jot.DefaultInitializer;

/*
 * v0.6
 * - Removed the system tray icon control
 * - Settings reworked under the hood (you'll lose settings from previous versions, sorry!)
 * - You can right-click the icon in the taskbar now to toggle borders/show settings
 * - Context menu added to the border (right-click on the black border at top)
 * - Settings window added (click cogwheel button on border, right click border and choose "Settings", or
 *      right-click on the application in the taskbar and choose "Show Settings" from there)
 * - Can change themes for chat now
 * - Also custom css (for theme "None" or use Custom URL)
 * - Zoom increase/decrease buttons won't close menu now
 * - Added "New Window" under context menu. You can add alerts widgets now (like from streamlabs)
 * 
 * TODO:
 * Easier/better size grip, kinda hard to see it, or click on it right now
 * Hotkey and/or menu item to hide the chat and make it visible again
 * Show current viewers and other stats from twitch?
 * Allowing you to chat from the app?
 *
 * 
 * Done:
 x Chat fading option
 x Click-through the application
 x Custom css
 x More options for kapchat, like setting the themes or other things
 * 
 */

namespace TransparentTwitchChatWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, BrowserWindow
    {
        bool hiddenBorders = false;
        GeneralSettings genSettings;

        //StringCollection custom_windows = new StringCollection();
        List<BrowserWindow> windows = new List<BrowserWindow>();

        public bool BotActivity
        {
            get
            {
                return this.genSettings.ShowBotActivity;
            }
            set
            {
                this.genSettings.ShowBotActivity = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            this.genSettings = new GeneralSettings
            {
                CustomWindows = new StringCollection(),
                Username = string.Empty,
                FadeChat = false,
                FadeTime = "120",
                ShowBotActivity = false,
                ThemeIndex = 1,
                isCustomURL = false,
                CustomURL = string.Empty,
                ZoomLevel = 0
            };

            var browserSettings = new BrowserSettings
            {
                FileAccessFromFileUrls = CefState.Enabled,
                UniversalAccessFromFileUrls = CefState.Enabled
            };
            Browser1.BrowserSettings = browserSettings;
        }

        public bool ProcessCommandLineArgs(IList<string> args)
        {
            if (args == null || args.Count == 0)
                return true;
            if ((args.Count > 1))
            {
                //the first index always contains the location of the exe so we need to check the second index
                if ((args[1].ToLowerInvariant() == "/toggleborders"))
                {
                    ToggleBorderVisibility();
                }
                else if ((args[1].ToLowerInvariant() == "/settings"))
                {
                    ShowSettingsWindow();
                }
            }

            return true;
        }

        public void drawBorders()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            WindowHelper.SetWindowExDefault(hwnd);

            btnClose.Visibility = System.Windows.Visibility.Visible;
            btnMin.Visibility = System.Windows.Visibility.Visible;
            btnHide.Visibility = System.Windows.Visibility.Visible;
            btnSettings.Visibility = System.Windows.Visibility.Visible;

            headerBorder.Background = Brushes.Black;
            this.BorderBrush = Brushes.Black;
            this.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;

            hiddenBorders = false;

            this.Topmost = false;
            this.Activate();
            this.Topmost = true;
        }

        public void hideBorders()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            WindowHelper.SetWindowExTransparent(hwnd);

            btnClose.Visibility = System.Windows.Visibility.Hidden;
            btnMin.Visibility = System.Windows.Visibility.Hidden;
            btnHide.Visibility = System.Windows.Visibility.Hidden;
            btnSettings.Visibility = System.Windows.Visibility.Hidden;

            headerBorder.Background = Brushes.Transparent;
            this.BorderBrush = Brushes.Transparent;
            this.ResizeMode = System.Windows.ResizeMode.NoResize;

            hiddenBorders = true;

            this.Topmost = false;
            this.Activate();
            this.Topmost = true;
        }

        public void ToggleBorderVisibility()
        {
            if (hiddenBorders)
            {
                drawBorders();
                foreach (BrowserWindow win in this.windows)
                    win.drawBorders();
            }
            else
            {
                hideBorders();
                foreach (BrowserWindow win in this.windows)
                    win.hideBorders();
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F9)
            {
                ToggleBorderVisibility();
            }
            else if (e.Key == Key.F8)
            {
                ShowInputDialogBox();

                if (!string.IsNullOrEmpty(this.genSettings.Username))
                {
                    SetChatAddress(this.genSettings.Username);
                }
            }
        }

        private void headerBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // Begin dragging the window
            this.DragMove();
        }

        private void CommandBinding_CanExecute_1(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CommandBinding_Executed_1(object sender, ExecutedRoutedEventArgs e)
        {
            if (MessageBox.Show("Sure you want to exit the application?", "Exit", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                System.Windows.Application.Current.Shutdown();
            }
            //SystemCommands.CloseWindow(this);
        }

        private void CommandBinding_Executed_3(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void SetCustomChatAddress(string url)
        {
            Browser1.Load(url);
        }

        private void SetChatAddress(string chatChannel)
        {
            string username = chatChannel;
            if (chatChannel.Contains("/"))
                username = chatChannel.Split('/').Last();

            string fade = this.genSettings.FadeTime;
            if (!this.genSettings.FadeChat) { fade = "false"; }

            string theme = string.Empty;
            if ((genSettings.ThemeIndex >= 0) && (genSettings.ThemeIndex < KapChat.Themes.Count))
                theme = KapChat.Themes[genSettings.ThemeIndex];

            string url = @"https://www.nightdev.com/hosted/obschat/?";
            url += @"theme=" + theme;
            url += @"&channel=" + username;
            url += @"&fade=" + fade;
            url += @"&bot_activity=" + this.genSettings.ShowBotActivity.ToString();
            url += @"&prevent_clipping=false";
            Browser1.Load(url);
        }

        public void ShowInputDialogBox()
        {
            Input inputDialog = new Input();
            if (inputDialog.ShowDialog() == true)
            {
                // reset custom url
                this.genSettings.isCustomURL = false;
                this.genSettings.CustomURL = string.Empty;

                this.genSettings.Username = inputDialog.Channel;
                if (this.genSettings.Username.Contains("/"))
                    this.genSettings.Username = this.genSettings.Username.Split('/').Last();

                if ((string.IsNullOrEmpty(this.genSettings.Username)) || (string.IsNullOrWhiteSpace(this.genSettings.Username)))
                {
                    MessageBox.Show("Invalid channel! Please enter your Twitch username only.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void ShowInputFadeDialogBox()
        {
            Input_Fade inputDialog = new Input_Fade();
            if (inputDialog.ShowDialog() == true)
            {
                string fadeTime = inputDialog.Channel;
                int fadeTimeInt = 0;

                int.TryParse(fadeTime, out fadeTimeInt);

                this.genSettings.FadeChat = (fadeTimeInt > 0);
                this.genSettings.FadeTime = fadeTime;
            }
        }

        public void ShowInputCustomChatDialogBox()
        {
            Input_Custom inputDialog = new Input_Custom();
            if (inputDialog.ShowDialog() == true)
            {
                this.genSettings.isCustomURL = true;
                this.genSettings.CustomURL = inputDialog.Url;
            }
        }

        public void ToggleBotActivitySetting()
        {
            this.genSettings.ShowBotActivity = !this.genSettings.ShowBotActivity;
            SetChatAddress(this.genSettings.Username);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void MenuItem_ToggleBorderVisible(object sender, RoutedEventArgs e)
        {
            foreach (BrowserWindow window in this.windows)
            {
                if (window != null)
                {
                    if (this.hiddenBorders)
                        window.drawBorders();
                    else
                        window.hideBorders();
                }
            }
            ToggleBorderVisibility();
        }

        private void MenuItem_ShowSettings(object sender, RoutedEventArgs e)
        {
            ShowSettingsWindow();
        }

        private void MenuItem_VisitWebsite(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/baffler/Transparent-Twitch-Chat-Overlay/releases");
        }

        private void btnHide_Click(object sender, RoutedEventArgs e)
        {
            hideBorders();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            //this.contextMenu.IsOpen = false;
            //this.contextMenu.Placement = PlacementMode.MousePoint;
            //this.contextMenu.HorizontalOffset = 0;
            //this.contextMenu.VerticalContentAlignment = 0;
            //this.contextMenu.IsOpen = true;
        }

        private void MenuItem_ZoomIn(object sender, RoutedEventArgs e)
        {
            if (this.Browser1.ZoomInCommand.CanExecute(null))
            {
                if (this.genSettings.ZoomLevel < 4.0)
                {
                    this.Browser1.ZoomInCommand.Execute(null);
                    this.genSettings.ZoomLevel = this.Browser1.ZoomLevel;
                }
            }
        }

        private void MenuItem_ZoomOut(object sender, RoutedEventArgs e)
        {
            if (this.Browser1.ZoomOutCommand.CanExecute(null))
            {
                if (this.genSettings.ZoomLevel > -4.0)
                {
                    this.Browser1.ZoomOutCommand.Execute(null);
                    this.genSettings.ZoomLevel = this.Browser1.ZoomLevel;
                }
            }
        }

        private void MenuItem_ZoomReset(object sender, RoutedEventArgs e)
        {
            if (this.Browser1.ZoomResetCommand.CanExecute(null))
            {
                this.Browser1.ZoomResetCommand.Execute(null);
                this.genSettings.ZoomLevel = this.Browser1.ZoomLevel;
            }
        }

        private void Browser1_LoadingStateChanged(object sender, CefSharp.LoadingStateChangedEventArgs e)
        {
            if (!e.IsLoading)
            {
                //if ((this.genSettings.ZoomLevel >= -4.0) && (this.genSettings.ZoomLevel <= 4.0))
                this.Browser1.Dispatcher.Invoke(new Action(() => { this.Browser1.ZoomLevel = this.genSettings.ZoomLevel; }));

                if (!string.IsNullOrEmpty(this.genSettings.CustomCSS))
                    InjectCSS(this.genSettings.CustomCSS);
            }
        }

        private void InjectCSS(string CSS)
        {
            string base64CSS = Utilities.Base64Encode(CSS.Replace("\r\n", "").Replace("\t", ""));

            string href = "data:text/css;charset=utf-8;base64," + base64CSS;

            string script = "var link = document.createElement('link');";
            script += "link.setAttribute('rel', 'stylesheet');";
            script += "link.setAttribute('type', 'text/css');";
            script += "link.setAttribute('href', '" + href + "');";
            script += "document.getElementsByTagName('head')[0].appendChild(link);";

            this.Browser1.ExecuteScriptAsync(script);
        }

        private void MenuItem_ClickNewWindow(object sender, RoutedEventArgs e)
        {
            Input_Custom inputDialog = new Input_Custom();
            if (inputDialog.ShowDialog() == true)
            {
                if (this.genSettings.CustomWindows.Contains(inputDialog.Url))
                {
                    MessageBox.Show("That URL already exists as a window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    this.genSettings.CustomWindows.Add(inputDialog.Url);
                    OpenNewCustomWindow(inputDialog.Url);
                }
            }
        }

        private void ShowSettingsWindow()
        {
            WindowSettings config = new WindowSettings {
                Title = "Main Window",
                isCustomURL = this.genSettings.isCustomURL,
                URL = this.genSettings.CustomURL,
                Username = this.genSettings.Username,
                ChatFade = this.genSettings.FadeChat,
                FadeTime = this.genSettings.FadeTime,
                ShowBotActivity = this.genSettings.ShowBotActivity,
                Theme = this.genSettings.ThemeIndex,
                CustomCSS = this.genSettings.CustomCSS
            };

            SettingsWindow settingsWindow = new SettingsWindow(config);

            if (settingsWindow.ShowDialog() == true)
            {
                if (config.isCustomURL)
                {
                    this.genSettings.isCustomURL = config.isCustomURL;
                    this.genSettings.CustomURL = config.URL;
                    this.genSettings.CustomCSS = config.CustomCSS;

                    if (!string.IsNullOrEmpty(this.genSettings.CustomURL))
                        SetCustomChatAddress(this.genSettings.CustomURL);
                }
                else
                {
                    this.genSettings.isCustomURL = false;
                    this.genSettings.Username = config.Username;
                    this.genSettings.FadeChat = config.ChatFade;
                    this.genSettings.FadeTime = config.FadeTime;
                    this.genSettings.ShowBotActivity = config.ShowBotActivity;
                    this.genSettings.ThemeIndex = config.Theme;

                    if (this.genSettings.ThemeIndex == 0)
                        this.genSettings.CustomCSS = config.CustomCSS;
                    else
                        this.genSettings.CustomCSS = string.Empty;


                    if (!string.IsNullOrEmpty(this.genSettings.Username))
                        SetChatAddress(this.genSettings.Username);
                }
            }
        }

        private void OpenNewCustomWindow(string url)
        {
            CustomWindow newWindow = new CustomWindow(this, url);
            windows.Add(newWindow);
            newWindow.Show();
        }

        public void RemoveCustomWindow(string url)
        {
            if (this.genSettings.CustomWindows.Contains(url))
            {
                this.genSettings.CustomWindows.Remove(url);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = true;
        }

        private void Window_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = true;
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsWindow();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            Services.Tracker.Configure(this).IdentifyAs("State").Apply();
            Services.Tracker.Configure(this.genSettings).IdentifyAs("MainWindow").Apply();

            SetupBrowser();
        }

        private void SetupBrowser()
        {
            drawBorders();

            if (this.genSettings.CustomWindows != null)
            {
                foreach (string url in this.genSettings.CustomWindows)
                    OpenNewCustomWindow(url);
            }

            this.Browser1.ZoomLevelIncrement = 0.25;

            if (this.genSettings.isCustomURL && !string.IsNullOrWhiteSpace(this.genSettings.CustomURL))
            {
                SetCustomChatAddress(this.genSettings.CustomURL);
            }
            else if (!string.IsNullOrWhiteSpace(this.genSettings.Username))
            {
                SetChatAddress(this.genSettings.Username);
            }
            else
            {
                Uri startupPath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
                string address = System.IO.Path.GetDirectoryName(startupPath.LocalPath) + "\\index.html";
                Browser1.Load(address);

                //CefSharp.WebBrowserExtensions.LoadHtml(Browser1,
                //"<html><body style=\"font-size: x-large; color: white; text-shadow: -1px -1px 0 #000, 1px -1px 0 #000, -1px 1px 0 #000, 1px 1px 0 #000; \">Load a channel to connect to by right-clicking the tray icon.<br /><br />You can move and resize the window, then press the [o] button to hide borders, or use the tray icon menu.</body></html>");
            }
        }
    }

    public class GeneralSettings
    {
        [Trackable]
        public StringCollection CustomWindows { get; set; }
        [Trackable]
        public string Username { get; set; }
        [Trackable]
        public bool FadeChat { get; set; }
        [Trackable]
        public string FadeTime { get; set; } // fade time in seconds or "false"
        [Trackable]
        public bool ShowBotActivity { get; set; }
        [Trackable]
        public int ThemeIndex { get; set; }
        [Trackable]
        public string CustomCSS { get; set; }
        [Trackable]
        public bool isCustomURL { get; set; }
        [Trackable]
        public string CustomURL { get; set; }
        [Trackable]
        public double ZoomLevel { get; set; }
    }
}
