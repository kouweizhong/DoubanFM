﻿/*
 * Author : K.F.Storm
 * Email : yk000123 at sina.com
 * Website : http://www.kfstorm.com
 * */

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using DoubanFM.Core;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Globalization;
using DoubanFM.Interop;
using System.Windows.Threading;

namespace DoubanFM
{
	/// <summary>
	/// 歌词窗口
	/// </summary>
	public partial class LyricsWindow : Window
	{
		public static readonly DependencyProperty LyricsSettingProperty = DependencyProperty.Register("LyricsSetting", typeof(LyricsSetting), typeof(LyricsWindow));
		public LyricsSetting LyricsSetting
		{
			get { return (LyricsSetting)GetValue(LyricsSettingProperty); }
			set { SetValue(LyricsSettingProperty, value); }
		}

		private Lyrics _lyrics;
		/// <summary>
		/// 歌词分析器
		/// </summary>
		public Lyrics Lyrics
		{
			get { return _lyrics; }
			internal set
			{
				if (_lyrics != value)
				{
					_lyrics = value;
					_lyricsCurrentIndex = int.MinValue;
				}
			}
		}
		/// <summary>
		/// 当前歌词所在位置
		/// </summary>
		private int _lyricsCurrentIndex = int.MinValue;
		/// <summary>
		/// 更换歌词的Storyboard
		/// </summary>
		private Storyboard ChangeLyricsStoryboard, HideLyricsStoryboard;

		public LyricsWindow(LyricsSetting lyricsSetting = null)
		{
			this.InitializeComponent();

			// 在此点之下插入创建对象所需的代码。
			LyricsSetting = lyricsSetting;

			ChangeLyricsStoryboard = (Storyboard)FindResource("ChangeLyricsStoryboard");
			HideLyricsStoryboard = (Storyboard)FindResource("HideLyricsStoryboard");

			this.SourceInitialized += new EventHandler((o, e) =>
			{
				var hwnd = new WindowInteropHelper(this).Handle;
				int extendedStyle = NativeMethods.GetWindowLong(hwnd, GWL.EXSTYLE);
				NativeMethods.SetWindowLong(hwnd, GWL.EXSTYLE, extendedStyle
					//鼠标穿透
					| WS.EX.TRANSPARENT
					//在按下Alt+Tab时不显示
					| WS.EX.TOOLWINDOW);
			});

			//更新歌词前景色
			UpdateForegroundSetting();
			//更新窗口大小和位置
			UpdateSizeAndLocation();

			//监听Windows的显示设置，当分辨率改变或任务栏位置和大小改变时，能够调整歌词位置
			Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { UpdateSizeAndLocation(); };
			Microsoft.Win32.SystemEvents.UserPreferenceChanged += new Microsoft.Win32.UserPreferenceChangedEventHandler((sender, e) =>
				{
					if (e.Category == Microsoft.Win32.UserPreferenceCategory.Desktop)
						UpdateSizeAndLocation();
				});

			//将歌词窗口的属性与设置绑定

			Binding binding = new Binding();
			binding.Source = LyricsSetting;
			binding.Path = new PropertyPath(DoubanFM.LyricsSetting.FontFamilyProperty);
			this.SetBinding(LyricsFontFamilyProperty, binding);

			Binding binding2 = new Binding();
			binding2.Source = LyricsSetting;
			binding2.Path = new PropertyPath(DoubanFM.LyricsSetting.FontSizeProperty);
			this.SetBinding(LyricsFontSizeProperty, binding2);

			Binding binding3 = new Binding();
			binding3.Source = LyricsSetting;
			binding3.Path = new PropertyPath(DoubanFM.LyricsSetting.FontWeightProperty);
			binding3.Converter = new OpenTypeWeightToFontWeightConverter();
			this.SetBinding(LyricsFontWeightProperty, binding3);

			Binding binding4 = new Binding();
			binding4.Source = LyricsSetting;
			binding4.Path = new PropertyPath(DoubanFM.LyricsSetting.StrokeWeightProperty);
			this.SetBinding(LyricsStrokeWeightProperty, binding4);
		}

		/// <summary>
		/// 根据当前设置应用歌词颜色
		/// </summary>
		public void UpdateForegroundSetting()
		{
			if (LyricsSetting.AutoForeground)
			{
				SetAutoForeground();
			}
			else
			{
				SetManualForeground();
			}
		}

		/// <summary>
		/// 设置自动变换歌词颜色
		/// </summary>
		protected void SetAutoForeground()
		{
			Binding binding = new Binding();
			binding.Source = Application.Current.MainWindow;
			binding.Path = new PropertyPath(System.Windows.Window.BackgroundProperty);
			binding.Converter = new BackgroundToLyricsForegroundConverter();
			PathText1.SetBinding(Path.FillProperty, binding);
		}

		/// <summary>
		/// 设置手动更换歌词颜色
		/// </summary>
		protected void SetManualForeground()
		{
			PathText1.Fill = new SolidColorBrush();
			Binding binding = new Binding();
			binding.Source = LyricsSetting;
			binding.Path = new PropertyPath(DoubanFM.LyricsSetting.ForegroundProperty);
			BindingExpressionBase expression = BindingOperations.SetBinding(PathText1.Fill, SolidColorBrush.ColorProperty, binding);
		}

		/// <summary>
		/// 更换歌词
		/// </summary>
		protected void ChangeLyrics(string newLyrics1, string newLyrics2, string newLyrics3)
		{
			((StringAnimationUsingKeyFrames)ChangeLyricsStoryboard.Children[2]).KeyFrames[0].Value = newLyrics1;
			((StringAnimationUsingKeyFrames)ChangeLyricsStoryboard.Children[3]).KeyFrames[0].Value = newLyrics2;
			((StringAnimationUsingKeyFrames)ChangeLyricsStoryboard.Children[4]).KeyFrames[0].Value = newLyrics3;
			ChangeLyricsStoryboard.Begin();
		}

		/// <summary>
		/// 按时间刷新歌词
		/// </summary>
		public void Refresh(TimeSpan time)
		{
			if (_lyrics != null)
			{
				_lyrics.Refresh(time + ((DoubleAnimationUsingKeyFrames)ChangeLyricsStoryboard.Children[0]).KeyFrames[0].KeyTime.TimeSpan);
				if (_lyrics.CurrentIndex != _lyricsCurrentIndex)
				{
					_lyricsCurrentIndex = _lyrics.CurrentIndex;
					string next2Lyrics = (_lyrics.CurrentIndex + 2 >= _lyrics.SortedTimes.Count) ? null : _lyrics.TimeAndLyrics[_lyrics.SortedTimes[_lyrics.CurrentIndex + 2]];
					ChangeLyrics(_lyrics.CurrentLyrics, _lyrics.NextLyrics, next2Lyrics);
				}
			}
			else
			{
				HideLyricsStoryboard.Begin();
			}
		}

		/// <summary>
		/// 更新窗口的位置和大小
		/// </summary>
		protected void UpdateSizeAndLocation()
		{
			this.Left = SystemParameters.WorkArea.Left;
			this.Top = SystemParameters.WorkArea.Top;
			this.Width = SystemParameters.WorkArea.Width;
			this.Height = SystemParameters.WorkArea.Height;
		}

		/// <summary>
		/// 显示边界
		/// </summary>
		public void ShowBoundary()
		{
			GrayPanel.Background = new SolidColorBrush(Color.FromArgb(0x7F, 0, 0, 0));
		}

		/// <summary>
		/// 隐藏边界
		/// </summary>
		public void HideBoundary()
		{
			GrayPanel.Background = null;
		}

		#region 绘制歌词

		#region 字体
		/// <summary>
		/// 字体
		/// </summary>
		public FontFamily LyricsFontFamily
		{
			get { return (FontFamily)GetValue(LyricsFontFamilyProperty); }
			set { SetValue(LyricsFontFamilyProperty, value); }
		}

		public static readonly DependencyProperty LyricsFontFamilyProperty =
			DependencyProperty.Register("LyricsFontFamily", typeof(FontFamily), typeof(LyricsWindow), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, new PropertyChangedCallback(OnLyricsFontFamilyChanged)));

		static void OnLyricsFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText1);
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText2);
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText3);
		}
		#endregion

		#region 字号
		/// <summary>
		/// 字号
		/// </summary>
		public double LyricsFontSize
		{
			get { return (double)GetValue(LyricsFontSizeProperty); }
			set { SetValue(LyricsFontSizeProperty, value); }
		}

		public static readonly DependencyProperty LyricsFontSizeProperty =
			DependencyProperty.Register("LyricsFontSize", typeof(double), typeof(LyricsWindow), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, new PropertyChangedCallback(OnLyricsFontSizeChanged)));

		static void OnLyricsFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText1);
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText2);
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText3);
			(d as LyricsWindow).UpdateStrokeAndShadow();
		}
		#endregion

		#region 粗细
		/// <summary>
		/// 粗细
		/// </summary>
		public FontWeight LyricsFontWeight
		{
			get { return (FontWeight)GetValue(LyricsFontWeightProperty); }
			set { SetValue(LyricsFontWeightProperty, value); }
		}

		public static readonly DependencyProperty LyricsFontWeightProperty =
			DependencyProperty.Register("LyricsFontWeight", typeof(FontWeight), typeof(LyricsWindow), new FrameworkPropertyMetadata(SystemFonts.MessageFontWeight, new PropertyChangedCallback(OnLyricsFontWeightChanged)));

		static void OnLyricsFontWeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText1);
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText2);
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText3);
		}
		#endregion

		#region 描边粗细
		/// <summary>
		/// 描边粗细
		/// </summary>
		public double LyricsStrokeWeight
		{
			get { return (double)GetValue(LyricsStrokeWeightProperty); }
			set { SetValue(LyricsStrokeWeightProperty, value); }
		}

		public static readonly DependencyProperty LyricsStrokeWeightProperty =
			DependencyProperty.Register("LyricsStrokeWeight", typeof(double), typeof(LyricsWindow), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnLyricsStrokeWeightChanged)));

		static void OnLyricsStrokeWeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as LyricsWindow).UpdateStrokeAndShadow();
		}
		#endregion

		#region 歌词文字1
		/// <summary>
		/// 歌词文字1
		/// </summary>
		public string LyricsText1
		{
			get { return (string)GetValue(LyricsText1Property); }
			set { SetValue(LyricsText1Property, value); }
		}

		public static readonly DependencyProperty LyricsText1Property =
			DependencyProperty.Register("LyricsText1", typeof(string), typeof(LyricsWindow), new FrameworkPropertyMetadata(string.Empty, new PropertyChangedCallback(OnLyricsText1Changed)));

		static void OnLyricsText1Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText1);
		}
		#endregion

		#region 歌词文字2
		/// <summary>
		/// 歌词文字2
		/// </summary>
		public string LyricsText2
		{
			get { return (string)GetValue(LyricsText2Property); }
			set { SetValue(LyricsText2Property, value); }
		}

		public static readonly DependencyProperty LyricsText2Property =
			DependencyProperty.Register("LyricsText2", typeof(string), typeof(LyricsWindow), new FrameworkPropertyMetadata(string.Empty, new PropertyChangedCallback(OnLyricsText2Changed)));

		static void OnLyricsText2Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText2);
		}
		#endregion

		#region 歌词文字3
		/// <summary>
		/// 歌词文字3
		/// </summary>
		public string LyricsText3
		{
			get { return (string)GetValue(LyricsText3Property); }
			set { SetValue(LyricsText3Property, value); }
		}

		public static readonly DependencyProperty LyricsText3Property =
			DependencyProperty.Register("LyricsText3", typeof(string), typeof(LyricsWindow), new FrameworkPropertyMetadata(string.Empty, new PropertyChangedCallback(OnLyricsText3Changed)));

		static void OnLyricsText3Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as LyricsWindow).UpdateText((d as LyricsWindow).PathText3);
		}
		#endregion

		/// <summary>
		/// 重绘歌词
		/// </summary>
		public void UpdateStrokeAndShadow()
		{
			PathText1.StrokeThickness = LyricsStrokeWeight / 48.0 * LyricsFontSize;
			ShadowEffect.ShadowDepth = PathText1.StrokeThickness;
			if (ShadowEffect.ShadowDepth == 0)
				ShadowEffect.Opacity = 0;
			else
				ShadowEffect.Opacity = 1;
		}

		/// <summary>
		/// 更新文字外形
		/// </summary>
		/// <param name="text">要更新的文字外形</param>
		public void UpdateText(Path text)
		{
			if (text == PathText1)
			{
				text.Data = CreateText(LyricsText1);
				SingleLinePathText1.Data = text.Data.Clone();
			}
			else if (text == PathText2)
			{
				text.Data = CreateText(LyricsText2);
			}
			else if (text == PathText3)
			{
				text.Data = CreateText(LyricsText3);
			}
		}

		/// <summary>
		/// 根据字符串内容创建几何形状
		/// </summary>
		public Geometry CreateText(string text)
		{
			// Create the formatted text based on the properties set.
			Geometry geometry = new FormattedText(
				text == null ? "" : text,
				CultureInfo.GetCultureInfo("zh-cn"),
				FlowDirection.LeftToRight,
				new Typeface(LyricsFontFamily == null ? SystemFonts.MessageFontFamily : LyricsFontFamily, FontStyles.Normal, LyricsFontWeight, FontStretches.Normal),
				LyricsFontSize,
				System.Windows.Media.Brushes.Black // This brush does not matter since we use the geometry of the text. 
				).BuildGeometry(new System.Windows.Point(0, 0));
			if (geometry.CanFreeze) geometry.Freeze();
			return geometry;
		}

		#endregion

		/// <summary>
		/// 强力置顶所使用的计时器
		/// </summary>
		DispatcherTimer timer = new DispatcherTimer();

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			//强力置顶
			timer.Interval = TimeSpan.FromSeconds(1);
			timer.Tick += delegate
			{
				if (LyricsSetting.ForceTopMost)
				{
					NativeMethods.SetWindowPos(new WindowInteropHelper(this).EnsureHandle(), HWND.TOPMOST, 0, 0, 0, 0, SWP.NOMOVE | SWP.NOSIZE | SWP.NOACTIVATE | SWP.NOREPOSITION | SWP.NOREDRAW);
				}
			};
			timer.Start();
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			timer.Stop();
		}
	}
}