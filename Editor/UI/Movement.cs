using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private bool _debugging = UIEditor.Debugging;
        private Point startPosition;
        private Action<EbxAssetEntry, bool, Canvas> LoadUI;
        private bool showHitboxes = Config.Get("ShowHitboxes", true);

        public Movement(Action<EbxAssetEntry, bool, Canvas> LoadUI, Canvas _uiCanvas, Button _refreshButton, Button _preciseButton, Button _unhideButton, TextBlock _uiSizeText, TextBlock _uiElementInfo, FrostyTabItem _tabProperties, FrostyPropertyGrid _tabPropertiesContent)
        {
            this.LoadUI = LoadUI;

            this._uiCanvas = _uiCanvas;
            this._refreshButton = _refreshButton;
            this._preciseButton = _preciseButton;
            this._unhideButton = _unhideButton;
            this._uiSizeText = _uiSizeText;
            this._uiElementInfo = _uiElementInfo;
            this._tabProperties = _tabProperties;
            this._tabPropertiesContent = _tabPropertiesContent;

            _tabPropertiesContent.OnModified += (sender, e) =>
            {
                if (SelectedCanvas != null && SelectedElement != null)
                {
                    Canvas parent = SelectedCanvas.Parent as Canvas;
                    dynamic rootObject = CurrentRootObject.Get();

                    Canvas newCanvas = LoadElement.Load(SelectedElement, false, this, rootObject, parent, LoadUI);

                    if (newCanvas != null)
                    {
                        int zindex = parent.Children.IndexOf(SelectedCanvas);

                        parent.Children.Remove(SelectedCanvas);

                        // LoadElement.Load already adds the canvas to the parent, but we want the layering
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

            // gets the mouse position
            startPosition = Mouse.GetPosition(_uiCanvas);
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;

            Canvas canvas = sender as Canvas;
            SelectedCanvas = canvas;

            if (showHitboxes)
            {
                canvas.Background = new SolidColorBrush(Color.FromArgb(15, 157, 198, 252));
            }

            // reset ZIndex after moving it
            Panel.SetZIndex(canvas, 0);

            double roundedX = Math.Round(Canvas.GetLeft(canvas) / UIEditor.RoundTo) * UIEditor.RoundTo;
            double roundedY = Math.Round(Canvas.GetTop(canvas) / UIEditor.RoundTo) * UIEditor.RoundTo;

            float movedX = (float)roundedX;
            float movedY = (float)roundedY;

            // sets the position
            Canvas.SetLeft(canvas, roundedX);
            Canvas.SetTop(canvas, roundedY);

            // gets the Guid of the canvas from the tag created earlier so we can use it as an ebx asset
            var canvasGuid = canvas.Tag;

            _refreshButton.Visibility = Visibility.Visible;
            _preciseButton.Visibility = Visibility.Visible;
            _unhideButton.Visibility = Visibility.Visible;
            _uiSizeText.Visibility = Visibility.Visible;
            _uiElementInfo.Visibility = Visibility.Visible;

            dynamic rootObject = CurrentRootObject.Get();

            dynamic guid = null;

            // goes through every ui component until the guid of it matches the guid from the tag
            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                foreach (var uiElement in layer.Internal.Elements)
                {
                    guid = uiElement.Internal.__InstanceGuid;

                    if (_debugging)
                    {
                        App.Logger.Log(Convert.ToString("Guid: " + guid));
                        App.Logger.Log(Convert.ToString("Canvas Guid: " + canvasGuid));
                    }

                    if (guid.ToString() == canvasGuid.ToString())
                    {
                        SelectedElement = uiElement;
                        break;
                    }
                }
            }

            // i dont think its possible for this to be null, but just in case
            if (SelectedElement == null)
                return;

            bool useAnchor = Config.Get<bool>("UseAnchor", false);

            if (!useAnchor)
            {
                // if useAnchor is false, we remove the anchor and set the position with offset
                SelectedElement.Internal.Offset.X = movedX;
                SelectedElement.Internal.Offset.Y = movedY;

                SelectedElement.Internal.Anchor.X = 0;
                SelectedElement.Internal.Anchor.Y = 0;
            }
            else
            {
                // if useAnchor is true, we remove the offset and set the position with anchor

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
                catch (RuntimeBinderException)
                {
                    // if UseElementSize can't be found that means it isn't a widget reference

                    // i dont think an exception would affect these values, but just in case
                    // they are set back to this
                    width = SelectedElement.Internal.Size.X;
                    height = SelectedElement.Internal.Size.Y;
                }
                catch (NullReferenceException) { } // if there's no blueprint references

                float x = rootObject.Object.Internal.Size.X - width;
                float y = rootObject.Object.Internal.Size.Y - height;

                // this is so we don't divide by 0 if the size of the element is equal to the ui size
                // since subtracting from the same numbers just gives you 0
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

            // saves it to the ebx so that it will show up in game or in frosty
            EbxAssetEntry ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);

            App.AssetManager.ModifyEbx(rootObject.Name, asset);

            // refreshes the data explorer so that it shows as modified on the left
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

            if (_debugging)
            {
                App.Logger.Log("Saved Position");
            }

            // updates the property grid tab
            _tabPropertiesContent.Object = rootObject;
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

                if (showHitboxes)
                {
                    canvas.Background = new SolidColorBrush(Color.FromArgb(0, 157, 198, 252));
                }

                // sets the ZIndex above everything else so it doesn't glitch when moving near other ui elements
                Panel.SetZIndex(canvas, 9999);

                Point newPosition = Mouse.GetPosition(_uiCanvas);

                double left = Canvas.GetLeft(canvas);
                double top = Canvas.GetTop(canvas);

                Canvas.SetLeft(canvas, left + (newPosition.X - startPosition.X));
                Canvas.SetTop(canvas, top + (newPosition.Y - startPosition.Y));
                startPosition = newPosition;

                _refreshButton.Visibility = Visibility.Hidden;
                _preciseButton.Visibility = Visibility.Hidden;
                _unhideButton.Visibility = Visibility.Hidden;
                _uiSizeText.Visibility = Visibility.Hidden;
                _uiElementInfo.Visibility = Visibility.Hidden;

                if (_debugging)
                {
                    // this can make it very laggy if you have debugging on and not commented out
                    //App.Logger.Log(left.ToString());
                    //App.Logger.Log(top.ToString());

                    //App.Logger.Log(roundTo.ToString());
                }
            }
        }
        #endregion

        // arrow key movement for precise movements
        public void UICanvasKeyDown(object sender, KeyEventArgs e)
        {
            if (SelectedCanvas == null)
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

            // i would've used a switch but i wanted to have both WASD and arrow keys
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

            // checks if its a movement key (wasd or arrow keys) so that nothing happens if you touch any other keys
            if (movementKey)
            {
                dynamic rootObject = CurrentRootObject.Get();

                bool useAnchor = Config.Get<bool>("UseAnchor", false);

                float movedX = (float)Canvas.GetLeft(SelectedCanvas);
                float movedY = (float)Canvas.GetTop(SelectedCanvas);

                if (!useAnchor)
                {
                    SelectedElement.Internal.Offset.X = movedX;
                    SelectedElement.Internal.Offset.Y = movedY;

                    SelectedElement.Internal.Anchor.X = 0;
                    SelectedElement.Internal.Anchor.Y = 0;
                }
                else
                {
                    // if useAnchor is true, we remove the offset and set the position with anchor

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
                    catch (RuntimeBinderException)
                    {
                        // if UseElementSize can't be found that means it isn't a widget reference

                        // i dont think an exception would affect these values, but just in case
                        // they are set back to this
                        width = SelectedElement.Internal.Size.X;
                        height = SelectedElement.Internal.Size.Y;
                    }

                    float x = rootObject.Object.Internal.Size.X - width;
                    float y = rootObject.Object.Internal.Size.Y - height;

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
                
                // saves it to the ebx so that it will show up in game or in frosty
                EbxAssetEntry ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
                EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);

                App.AssetManager.ModifyEbx(rootObject.Name, asset);
                
                // refreshes the data explorer so that it shows as modified on the left
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
        }

        public void UICanvasKeyUp(object sender, KeyEventArgs e)
        {
            // this needs to be checked, otherwise if you were typing
            // it would just keep refreshing, not letting you to type anything
            if (!_tabPropertiesContent.IsKeyboardFocusWithin && SelectedElement != null)
            {
                dynamic rootObject = CurrentRootObject.Get();

                // updates the property grid tab
                _tabPropertiesContent.Object = rootObject;
                _tabPropertiesContent.Object = SelectedElement.Internal;
            }
        }

        // shows a transparent background when your mouse is over the canvas
        // useful for showing where you can or can't drag certain elements
        private void CanvasMouseEnter(object sender, MouseEventArgs e)
        {
            if (showHitboxes)
            {
                Canvas canvas = sender as Canvas;

                canvas.Background = new SolidColorBrush(Color.FromArgb(9, 157, 198, 252));
            }
        }

        private void CanvasMouseLeave(object sender, MouseEventArgs e)
        {
            if (showHitboxes)
            {
                Canvas canvas = sender as Canvas;

                canvas.Background = new SolidColorBrush(Color.FromArgb(0, 157, 198, 252));
            }
        }

        // right clicking will hide the ui, this is useful if some ui elements are in the way of something you wanna move
        private void CanvasHideUI(object sender, EventArgs e)
        {
            // it will only work if you aren't dragging a ui element otherwise it will be buggy
            if (!_dragging)
            {
                Canvas canvas = sender as Canvas;
                canvas.Visibility = Visibility.Hidden;
            }
        }
    }
}
