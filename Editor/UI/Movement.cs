using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UIBlueprintEditor.Editor.Misc;

namespace UIBlueprintEditor.Editor.UI
{
    public class Movement
    {
        private Canvas _uiCanvas;
        private Button _refreshButton;
        private Button _preciseButton;
        private Button _unhideButton;
        private TextBlock _uiSizeText;
        private TextBlock _uiElementInfo;
        private FrostyTabItem _tabProperties;
        private FrostyPropertyGrid _tabPropertiesContent;

        public Canvas SelectedCanvas;
        public dynamic SelectedElement;

        private bool _dragging = false;
        private Point _startPosition;
        private Action<dynamic, bool, Canvas> _loadUI;
        private bool _showHitboxes = Config.Get("ShowHitboxes", true);

        private EbxAssetEntry _ebxEntry;
        private EbxAsset _ebxAsset;
        private dynamic _rootObject;

        public Movement(Action<dynamic, bool, Canvas> LoadUI, Canvas _uiCanvas, Button _refreshButton, Button _preciseButton, Button _unhideButton, TextBlock _uiSizeText, TextBlock _uiElementInfo, FrostyTabItem _tabProperties, FrostyPropertyGrid _tabPropertiesContent)
        {
            _loadUI = LoadUI;

            this._uiCanvas = _uiCanvas;
            this._refreshButton = _refreshButton;
            this._preciseButton = _preciseButton;
            this._unhideButton = _unhideButton;
            this._uiSizeText = _uiSizeText;
            this._uiElementInfo = _uiElementInfo;
            this._tabProperties = _tabProperties;
            this._tabPropertiesContent = _tabPropertiesContent;

            _ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
            _ebxAsset = App.AssetManager.GetEbx(_ebxEntry);
            _rootObject = _ebxAsset.RootObject;

            _tabPropertiesContent.OnModified += (sender, e) =>
            {
                if (SelectedCanvas != null && SelectedElement != null)
                {
                    Canvas parent = SelectedCanvas.Parent as Canvas;

                    Canvas newCanvas = ElementLoader.LoadElement(SelectedElement, false, this, _rootObject, parent, LoadUI);

                    if (newCanvas != null)
                    {
                        int zindex = parent.Children.IndexOf(SelectedCanvas);

                        parent.Children.Remove(SelectedCanvas);

                        // ElementLoader.LoadElement already adds the canvas to the parent, but we want the layering
                        // to be the same so it's removed then added back at the right zindex
                        parent.Children.Remove(newCanvas);
                        parent.Children.Insert(zindex, newCanvas);

                        SelectedCanvas = newCanvas;

                        _uiElementInfo.Text =
                            string.Format(
                            "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}",
                            SelectedElement.Internal.InstanceName,
                            SelectedElement.Internal.Offset.X,
                            SelectedElement.Internal.Offset.Y,
                            SelectedElement.Internal.Anchor.X,
                            SelectedElement.Internal.Anchor.Y,
                            SelectedElement.Internal.__InstanceGuid.ToString());
                    }
                }
            };
        }

		private void SetPosition(float movedX, float movedY)
		{
			bool useAnchor = Config.Get<bool>("UseAnchor", false);

			if (!useAnchor)
			{
				SelectedElement.Internal.Offset.X = movedX;
				SelectedElement.Internal.Offset.Y = movedY;

				SelectedElement.Internal.Anchor.X = 0;
				SelectedElement.Internal.Anchor.Y = 0;
			}
			else
			{
				var width = SelectedElement.Internal.Size.X;
				var height = SelectedElement.Internal.Size.Y;

				try
				{
					if (!SelectedElement.Internal.UseElementSize)
					{
						var widgetGuid = ((PointerRef)SelectedElement.Internal.Blueprint).External.FileGuid;
						var widgetEbx = App.AssetManager.GetEbxEntry(widgetGuid);

						EbxAsset widgetAsset = App.AssetManager.GetEbx(widgetEbx);
						dynamic rootObjectWidget = widgetAsset.RootObject;

						width = rootObjectWidget.Object.Internal.Size.X;
						height = rootObjectWidget.Object.Internal.Size.Y;
					}
				}
				catch (RuntimeBinderException) { } // if it's not a widget reference
				catch (NullReferenceException) { } // if there's no blueprint references

				float x = _rootObject.Object.Internal.Size.X - width;
				float y = _rootObject.Object.Internal.Size.Y - height;

				// this is so we don't divide by 0 if the size of the element is equal to the ui size
				if (x == 0 || y == 0)
				{
					App.Logger.LogError($"({SelectedElement.Internal.InstanceName}) Can't use anchor, moving by offset instead. Sorry!");

					SelectedElement.Internal.Offset.X = movedX;
					SelectedElement.Internal.Offset.Y = movedY;

					SelectedElement.Internal.Anchor.X = 0;
					SelectedElement.Internal.Anchor.Y = 0;
				}
				else
				{
					SelectedElement.Internal.Anchor.X = movedX / x;
					SelectedElement.Internal.Anchor.Y = movedY / y;

					SelectedElement.Internal.Offset.X = 0;
					SelectedElement.Internal.Offset.Y = 0;
				}
			}
		}


        // this method exists because if we always used the same rootObject,
        // any new elements added by the toolbox wouldn't exist in that rootObject
        private void UpdateRootObject()
        {
			_ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
			_ebxAsset = App.AssetManager.GetEbx(_ebxEntry);
			_rootObject = _ebxAsset.RootObject;
		}

		#region Dragging

		public void ControlUI(Canvas canvas)
        {
            canvas.MouseMove += CanvasMouseMove;
            canvas.MouseLeftButtonDown += CanvasMouseDown;
            canvas.MouseLeftButtonUp += CanvasMouseUp;

            canvas.MouseEnter += CanvasMouseEnter;
            canvas.MouseLeave += CanvasMouseLeave;

            canvas.MouseRightButtonDown += CanvasHideUI;
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = sender as Canvas;
            SelectedCanvas = canvas;

            _dragging = true;

            _startPosition = Mouse.GetPosition(_uiCanvas);
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;

            Canvas canvas = sender as Canvas;
            SelectedCanvas = canvas;

            if (_showHitboxes)
            {
                canvas.Background = new SolidColorBrush(Color.FromArgb(15, 157, 198, 252));
            }

            Panel.SetZIndex(canvas, 0);

            double roundedX = Math.Round(Canvas.GetLeft(canvas) / UIEditor.RoundTo) * UIEditor.RoundTo;
            double roundedY = Math.Round(Canvas.GetTop(canvas) / UIEditor.RoundTo) * UIEditor.RoundTo;

            float movedX = (float)roundedX;
            float movedY = (float)roundedY;

            Canvas.SetLeft(canvas, roundedX);
            Canvas.SetTop(canvas, roundedY);

            var canvasGuid = canvas.Tag;

            _refreshButton.Visibility = Visibility.Visible;
            _preciseButton.Visibility = Visibility.Visible;
            _unhideButton.Visibility = Visibility.Visible;
            _uiSizeText.Visibility = Visibility.Visible;
            _uiElementInfo.Visibility = Visibility.Visible;

            dynamic guid = null;

			UpdateRootObject();
			foreach (var layer in _rootObject.Object.Internal.Layers)
            {
                foreach (var uiElement in layer.Internal.Elements)
                {
                    guid = uiElement.Internal.__InstanceGuid;
                    
                    if (guid.ToString() == canvasGuid.ToString())
                    {
						SelectedElement = uiElement;
                        break;
                    }
                }
            }

            if (SelectedElement == null)
                return;

            SetPosition(movedX, movedY);

            App.AssetManager.ModifyEbx(_ebxEntry.Name, _ebxAsset);
            App.EditorWindow.DataExplorer.RefreshItems();

            _uiElementInfo.Text =
                string.Format(
                "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}",
                SelectedElement.Internal.InstanceName,
                SelectedElement.Internal.Offset.X,
                SelectedElement.Internal.Offset.Y,
                SelectedElement.Internal.Anchor.X,
                SelectedElement.Internal.Anchor.Y,
                guid.ToString());

            _tabPropertiesContent.Object = _rootObject;
            _tabPropertiesContent.Object = SelectedElement.Internal;

            _tabProperties.IsEnabled = true;
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Canvas canvas = sender as Canvas;

                // if the wrong canvas is being selected it can be buggy so we will just cancel the movement
                if (canvas != SelectedCanvas)
                {
                    _dragging = false;

                    _refreshButton.Visibility = Visibility.Visible;
                    _preciseButton.Visibility = Visibility.Visible;
                    _unhideButton.Visibility = Visibility.Visible;
                    _uiSizeText.Visibility = Visibility.Visible;
                    _uiElementInfo.Visibility = Visibility.Visible;

                    return;
                }

                if (_showHitboxes)
                {
                    canvas.Background = new SolidColorBrush(Color.FromArgb(0, 157, 198, 252));
                }

                // sets the ZIndex above everything else so it doesn't glitch when moving near other ui elements
                Panel.SetZIndex(canvas, 9999);

                Point newPosition = Mouse.GetPosition(_uiCanvas);

                double left = Canvas.GetLeft(canvas);
                double top = Canvas.GetTop(canvas);

                Canvas.SetLeft(canvas, left + (newPosition.X - _startPosition.X));
                Canvas.SetTop(canvas, top + (newPosition.Y - _startPosition.Y));
                _startPosition = newPosition;

                _refreshButton.Visibility = Visibility.Hidden;
                _preciseButton.Visibility = Visibility.Hidden;
                _unhideButton.Visibility = Visibility.Hidden;
                _uiSizeText.Visibility = Visibility.Hidden;
                _uiElementInfo.Visibility = Visibility.Hidden;
            }
        }
        #endregion

        public void UICanvasKeyDown(object sender, KeyEventArgs e)
        {
            if (SelectedCanvas == null)
                return;

            if (Keyboard.FocusedElement is TextBox)
                return;

            bool movementKey = false;

            double left = Canvas.GetLeft(SelectedCanvas);
            double top = Canvas.GetTop(SelectedCanvas);

            int move = Config.Get("ArrowKeyMovementSetting", 5);

            // this stops the arrow keys from navigating to some random place, otherwise arrow keys would just break
            if (e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Down || e.Key == Key.Right)
            {
                e.Handled = true;
            }

            if (e.Key == Key.W || e.Key == Key.Up)
            {
                Canvas.SetTop(SelectedCanvas, top + -move);
                movementKey = true;
            }
            else if (e.Key == Key.A || e.Key == Key.Left)
            {
                Canvas.SetLeft(SelectedCanvas, left + -move);
                movementKey = true;
            }
            else if (e.Key == Key.S || e.Key == Key.Down)
            {
                Canvas.SetTop(SelectedCanvas, top + move);
                movementKey = true;
            }
            else if (e.Key == Key.D || e.Key == Key.Right)
            {
                Canvas.SetLeft(SelectedCanvas, left + move);
                movementKey = true;
            }

            if (movementKey && SelectedElement != null && SelectedCanvas != null)
            {
                float movedX = (float)Canvas.GetLeft(SelectedCanvas);
                float movedY = (float)Canvas.GetTop(SelectedCanvas);

				SetPosition(movedX, movedY);

				App.AssetManager.ModifyEbx(_ebxEntry.Name, _ebxAsset);
                App.EditorWindow.DataExplorer.RefreshItems();

                var guid = SelectedElement.Internal.__InstanceGuid;

                _uiElementInfo.Text =
                    string.Format(
                    "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}",
                    SelectedElement.Internal.InstanceName,
                    SelectedElement.Internal.Offset.X,
                    SelectedElement.Internal.Offset.Y,
                    SelectedElement.Internal.Anchor.X,
                    SelectedElement.Internal.Anchor.Y,
                    guid.ToString());
            }

            if ((e.Key == Key.X || e.Key == Key.Delete) && SelectedElement != null && SelectedCanvas != null)
            {
                MessageBoxResult result = FrostyMessageBox.Show($"Would you like to delete the element '{SelectedElement.Internal.InstanceName}'?", "UI Blueprint Editor", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.No)
                    return;

                int canvasIndex = _uiCanvas.Children.IndexOf(SelectedCanvas);
                _uiCanvas.Children.RemoveAt(canvasIndex);

				UpdateRootObject();
				foreach (var layer in _rootObject.Object.Internal.Layers)
                {
                    foreach (var uiElement in layer.Internal.Elements)
                    {
                        if (uiElement.Internal.__InstanceGuid == SelectedElement.Internal.__InstanceGuid)
                        {
							layer.Internal.Elements.Remove(uiElement);
                            break;
                        }
                    }
                }

                SelectedElement = null;
                SelectedCanvas = null;

                _uiElementInfo.Text = "InstanceName: ''\nOffset: 0, 0\nAnchor: 0, 0\n00000000-0000-0000-0000-000000000000";
                ((FrostyTabControl)_tabProperties.Parent).SelectedIndex = 0;
                _tabProperties.IsEnabled = false;

                App.AssetManager.ModifyEbx(_ebxEntry.Name, _ebxAsset);
                App.EditorWindow.DataExplorer.RefreshItems();
            }
        }

        public void UICanvasKeyUp(object sender, KeyEventArgs e)
        {
            // this needs to be checked, otherwise if you were typing
            // it would just keep refreshing, not letting you to type anything
            if (!_tabPropertiesContent.IsKeyboardFocusWithin && SelectedElement != null)
            {
                // updates the property grid tab
                _tabPropertiesContent.Object = _rootObject;
                _tabPropertiesContent.Object = SelectedElement.Internal;
            }
        }

        private void CanvasMouseEnter(object sender, MouseEventArgs e)
        {
            if (_showHitboxes)
            {
                Canvas canvas = sender as Canvas;

                canvas.Background = new SolidColorBrush(Color.FromArgb(9, 157, 198, 252));
            }
        }

        private void CanvasMouseLeave(object sender, MouseEventArgs e)
        {
            if (_showHitboxes)
            {
                Canvas canvas = sender as Canvas;

                canvas.Background = new SolidColorBrush(Color.FromArgb(0, 157, 198, 252));
            }
        }

        private void CanvasHideUI(object sender, EventArgs e)
        {
            if (!_dragging)
            {
                Canvas canvas = sender as Canvas;
                canvas.Visibility = Visibility.Hidden;
            }
        }
    }
}
