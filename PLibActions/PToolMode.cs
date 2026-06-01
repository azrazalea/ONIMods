/*
 * Copyright 2026 Peter Han
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or
 * substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 * BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using PeterHan.PLib.Core;
using System;
using System.Collections;
using System.Collections.Generic;

namespace PeterHan.PLib.Actions {
	/// <summary>
	/// A tool mode used in custom tool menus. Shown in the options in the bottom right.
	/// </summary>
	public sealed class PToolMode {
		/// <summary>
		/// Sets up tool options in the tool parameter menu.
		/// </summary>
		/// <param name="menu">The menu to configure.</param>
		/// <param name="options">The available modes.</param>
		/// <returns>A dictionary which is updated in real time to contain the actual state of each mode.</returns>
		public static IDictionary<string, ToolParameterMenu.ToggleState> PopulateMenu(
				ToolParameterMenu menu, ICollection<PToolMode> options) {
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			// U59 (Unity 6 / Mergedown tool refresh) changed ToolParameterMenu.PopulateMenu from
			// taking a live Dictionary<string, ToggleState> to a ToggleData[] whose elements the
			// menu mutates in place. Build that array, then hand back a thin dictionary view over
			// it so the long-standing "dictionary updated in real time" contract still holds.
			var toggles = new ToolParameterMenu.ToggleData[options.Count];
			int i = 0;
			foreach (var option in options) {
				string key = option.Key;
				if (!string.IsNullOrEmpty(option.Title))
					Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + key, option.Title);
				toggles[i++] = new ToolParameterMenu.ToggleData(key, option.State);
			}
			menu.PopulateMenu(toggles);
			return new ToggleStateView(toggles);
		}

		/// <summary>
		/// A live dictionary view over the ToggleData array used by the U59+ ToolParameterMenu.
		/// Reads and writes proxy directly to each ToggleData.state, preserving the previous
		/// "dictionary updated in real time" behavior of PopulateMenu. Structural mutation is
		/// unsupported because the toggle set is fixed once the menu is populated.
		/// </summary>
		private sealed class ToggleStateView : IDictionary<string, ToolParameterMenu.ToggleState> {
			private readonly ToolParameterMenu.ToggleData[] toggles;

			internal ToggleStateView(ToolParameterMenu.ToggleData[] toggles) {
				this.toggles = toggles;
			}

			private ToolParameterMenu.ToggleData Find(string key) {
				foreach (var t in toggles)
					if (t.name == key)
						return t;
				return null;
			}

			public ToolParameterMenu.ToggleState this[string key] {
				get {
					return (Find(key) ?? throw new KeyNotFoundException(key)).state;
				}
				set {
					(Find(key) ?? throw new KeyNotFoundException(key)).state = value;
				}
			}

			public ICollection<string> Keys {
				get {
					var k = new List<string>(toggles.Length);
					foreach (var t in toggles)
						k.Add(t.name);
					return k;
				}
			}

			public ICollection<ToolParameterMenu.ToggleState> Values {
				get {
					var v = new List<ToolParameterMenu.ToggleState>(toggles.Length);
					foreach (var t in toggles)
						v.Add(t.state);
					return v;
				}
			}

			public int Count => toggles.Length;

			public bool IsReadOnly => false;

			public bool ContainsKey(string key) => Find(key) != null;

			public bool TryGetValue(string key, out ToolParameterMenu.ToggleState value) {
				var t = Find(key);
				value = (t == null) ? ToolParameterMenu.ToggleState.Off : t.state;
				return t != null;
			}

			public bool Contains(KeyValuePair<string, ToolParameterMenu.ToggleState> item) {
				var t = Find(item.Key);
				return t != null && t.state == item.Value;
			}

			public void CopyTo(KeyValuePair<string, ToolParameterMenu.ToggleState>[] array,
					int arrayIndex) {
				foreach (var t in toggles)
					array[arrayIndex++] = new KeyValuePair<string, ToolParameterMenu.ToggleState>(
						t.name, t.state);
			}

			public IEnumerator<KeyValuePair<string, ToolParameterMenu.ToggleState>>
					GetEnumerator() {
				foreach (var t in toggles)
					yield return new KeyValuePair<string, ToolParameterMenu.ToggleState>(t.name,
						t.state);
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public void Add(string key, ToolParameterMenu.ToggleState value) =>
				throw new NotSupportedException();

			public void Add(KeyValuePair<string, ToolParameterMenu.ToggleState> item) =>
				throw new NotSupportedException();

			public bool Remove(string key) => throw new NotSupportedException();

			public bool Remove(KeyValuePair<string, ToolParameterMenu.ToggleState> item) =>
				throw new NotSupportedException();

			public void Clear() => throw new NotSupportedException();
		}

		/// <summary>
		/// Registers a tool with the game. It still must be added to a tool collection to be
		/// visible.
		/// </summary>
		/// <typeparam name="T">The tool type to register.</typeparam>
		/// <param name="controller">The player controller which will be its parent; consider
		/// using in a postfix on PlayerController.OnPrefabInit.</param>
		public static void RegisterTool<T>(PlayerController controller) where T : InterfaceTool
		{
			if (controller == null)
				throw new ArgumentNullException(nameof(controller));
			// Create list so that new tool can be appended at the end
			var interfaceTools = ListPool<InterfaceTool, PlayerController>.Allocate();
			interfaceTools.AddRange(controller.tools);
			var newTool = new UnityEngine.GameObject(typeof(T).Name);
			var tool = newTool.AddComponent<T>();
			// Reparent tool to the player controller, then enable/disable to load it
			newTool.transform.SetParent(controller.gameObject.transform);
			newTool.gameObject.SetActive(true);
			newTool.gameObject.SetActive(false);
			interfaceTools.Add(tool);
			controller.tools = interfaceTools.ToArray();
			interfaceTools.Recycle();
		}

		/// <summary>
		/// A unique key used to identify this mode.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// The current state of this tool mode.
		/// </summary>
		public ToolParameterMenu.ToggleState State { get; }

		/// <summary>
		/// The title displayed on-screen for this mode.
		/// </summary>
		public LocString Title { get; }

		/// <summary>
		/// Creates a new tool mode entry.
		/// </summary>
		/// <param name="key">The key which identifies this tool mode.</param>
		/// <param name="title">The title to be displayed. If null, the title will be taken
		/// from the default location in STRINGS.UI.TOOLS.FILTERLAYERS.</param>
		/// <param name="state">The initial state, default Off.</param>
		public PToolMode(string key, LocString title, ToolParameterMenu.ToggleState state =
				ToolParameterMenu.ToggleState.Off) {
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException(nameof(key));
			Key = key;
			State = state;
			Title = title;
		}

		public override bool Equals(object obj) {
			return obj is PToolMode other && other.Key == Key;
		}

		public override int GetHashCode() {
			return Key.GetHashCode();
		}

		public override string ToString() {
			return "{0} ({1})".F(Key, Title);
		}
	}
}
