/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SafeExamBrowser.UserInterface.Desktop.Windows
{
	public partial class VocabularyPopup : Window
	{
		private readonly Dictionary<string, string> vocabulary;
		
		// Global hotkey registration
		private const int HOTKEY_ID = 9000;
		private const int MOD_CONTROL = 0x0002;
		private const int MOD_SHIFT = 0x0004;
		private const int VK_V = 0x56;
		private const int WM_HOTKEY = 0x0312;
		
		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
		
		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
		
		[DllImport("user32.dll")]
		private static extern bool GetCursorPos(out POINT lpPoint);
		
		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int X;
			public int Y;
		}
		
		private HwndSource hwndSource;
		private static VocabularyPopup instance;

		public VocabularyPopup()
		{
			InitializeComponent();
			instance = this;
			
			// Load vocabulary from file
			vocabulary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			LoadVocabularyFromFile();

			InputBox.TextChanged += InputBox_TextChanged;
			InputBox.KeyDown += InputBox_KeyDown;
			Deactivated += (s, e) => HidePopup();
			Loaded += VocabularyPopup_Loaded;
			Closed += VocabularyPopup_Closed;
		}
		
		private void LoadVocabularyFromFile()
		{
			// Look for vocabulary.txt in the application directory
			var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			var exeDir = Path.GetDirectoryName(exePath);
			var vocabPath = Path.Combine(exeDir, "vocabulary.txt");
			
			if (!File.Exists(vocabPath))
			{
				// Fallback: add some defaults
				vocabulary["apfel"] = "Apple";
				vocabulary["apple"] = "Apfel";
				return;
			}
			
			try
			{
				var lines = File.ReadAllLines(vocabPath);
				foreach (var line in lines)
				{
					// Skip comments and empty lines
					var trimmed = line.Trim();
					if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
						continue;
					
					// Parse "word1 = word2"
					var parts = trimmed.Split(new[] { '=' }, 2);
					if (parts.Length == 2)
					{
						var word1 = parts[0].Trim();
						var word2 = parts[1].Trim();
						
						if (!string.IsNullOrEmpty(word1) && !string.IsNullOrEmpty(word2))
						{
							// Add both directions
							vocabulary[word1] = word2;
							vocabulary[word2] = word1;
						}
					}
				}
			}
			catch
			{
				// If file read fails, just use empty dictionary
			}
		}
		
		private void VocabularyPopup_Loaded(object sender, RoutedEventArgs e)
		{
			// Register global hotkey (Ctrl+Shift+V)
			var helper = new WindowInteropHelper(this);
			hwndSource = HwndSource.FromHwnd(helper.Handle);
			hwndSource?.AddHook(HwndHook);
			RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_V);
			InputBox.Focus();
		}
		
		private void VocabularyPopup_Closed(object sender, EventArgs e)
		{
			// Unregister global hotkey
			var helper = new WindowInteropHelper(this);
			UnregisterHotKey(helper.Handle, HOTKEY_ID);
			hwndSource?.RemoveHook(HwndHook);
		}
		
		private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
			{
				TogglePopup();
				handled = true;
			}
			return IntPtr.Zero;
		}

		private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			var input = InputBox.Text.Trim();
			
			if (string.IsNullOrEmpty(input))
			{
				ResultText.Text = "";
				return;
			}

			// Try exact match first
			if (vocabulary.TryGetValue(input, out var exactMatch))
			{
				ResultText.Text = "→ " + exactMatch;
				return;
			}

			// Try fuzzy match
			var fuzzyMatch = FindFuzzyMatch(input);
			if (fuzzyMatch != null)
			{
				ResultText.Text = "→ " + fuzzyMatch;
			}
			else
			{
				ResultText.Text = "";
			}
		}

		private string FindFuzzyMatch(string input)
		{
			input = NormalizeForFuzzy(input);
			
			foreach (var kvp in vocabulary)
			{
				var key = NormalizeForFuzzy(kvp.Key);
				
				// Check if similar enough (allows small typos)
				if (IsSimilar(input, key))
				{
					return kvp.Value;
				}
			}
			
			return null;
		}

		private string NormalizeForFuzzy(string text)
		{
			// Remove accents and special chars for fuzzy matching
			return text.ToLowerInvariant()
				.Replace("'", "")
				.Replace("'", "")
				.Replace("`", "")
				.Replace("´", "")
				.Replace("ä", "a")
				.Replace("ö", "o")
				.Replace("ü", "u")
				.Replace("ß", "ss")
				.Replace("é", "e")
				.Replace("è", "e")
				.Replace("ê", "e")
				.Replace("ë", "e")
				.Replace("à", "a")
				.Replace("â", "a")
				.Replace("î", "i")
				.Replace("ï", "i")
				.Replace("ô", "o")
				.Replace("û", "u")
				.Replace("ù", "u")
				.Replace("ç", "c")
				.Replace("œ", "oe")
				.Replace("æ", "ae");
		}

		private bool IsSimilar(string input, string target)
		{
			// Exact match after normalization
			if (input == target) return true;
			
			// If lengths differ by more than 2, not similar
			if (Math.Abs(input.Length - target.Length) > 2) return false;
			
			// Calculate Levenshtein distance
			int distance = LevenshteinDistance(input, target);
			
			// Allow up to 2 character mistakes for words >= 4 chars
			// Allow 1 mistake for shorter words
			int maxDistance = input.Length >= 4 ? 2 : 1;
			
			return distance <= maxDistance;
		}

		private int LevenshteinDistance(string s, string t)
		{
			int n = s.Length;
			int m = t.Length;
			int[,] d = new int[n + 1, m + 1];

			if (n == 0) return m;
			if (m == 0) return n;

			for (int i = 0; i <= n; i++) d[i, 0] = i;
			for (int j = 0; j <= m; j++) d[0, j] = j;

			for (int i = 1; i <= n; i++)
			{
				for (int j = 1; j <= m; j++)
				{
					int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost);
				}
			}

			return d[n, m];
		}

		private void InputBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				HidePopup();
				e.Handled = true;
			}
			else if (e.Key == Key.Enter)
			{
				// Clear input for next word
				InputBox.Text = "";
				e.Handled = true;
			}
		}

		public void ShowPopup()
		{
			InputBox.Text = "";
			ResultText.Text = "";
			
			// Position at cursor
			if (GetCursorPos(out POINT cursorPos))
			{
				// Convert from screen pixels to WPF units (DPI aware)
				var source = PresentationSource.FromVisual(this);
				if (source != null)
				{
					var transform = source.CompositionTarget.TransformFromDevice;
					var wpfPoint = transform.Transform(new System.Windows.Point(cursorPos.X, cursorPos.Y));
					Left = wpfPoint.X;
					Top = wpfPoint.Y;
				}
				else
				{
					Left = cursorPos.X;
					Top = cursorPos.Y;
				}
			}
			
			Show();
			Activate();
			InputBox.Focus();
		}

		public void HidePopup()
		{
			Hide();
		}

		public void TogglePopup()
		{
			if (IsVisible)
			{
				HidePopup();
			}
			else
			{
				ShowPopup();
			}
		}
	}
}

