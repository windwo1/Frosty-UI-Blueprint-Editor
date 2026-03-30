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
        private TextBlock _uiComponentInfo;
        private FrostyTabItem _tabProperties;
        private FrostyPropertyGrid _tabPropertiesContent;

        private bool dragging = false;

        private bool debugging = UIEditor.debugging;

        private Action<EbxAssetEntry, bool, Canvas> LoadUI;

        public Canvas selectedCanvas;
        public dynamic selectedElement;

        private Point startPosition;

        private bool showHitboxes = Config.Get("ShowHitboxes", true);

        public Movement(Action<EbxAssetEntry, bool, Canvas> LoadUI, Canvas _uiCanvas, Button _refreshButton, Button _preciseButton, Button _unhideButton, TextBlock _uiSizeText, TextBlock _uiComponentInfo, FrostyTabItem _tabProperties, FrostyPropertyGrid _tabPropertiesContent)
        {
            this.LoadUI = LoadUI;

            this._uiCanvas = _uiCanvas;
            this._refreshButton = _refreshButton;
            this._preciseButton = _preciseButton;
            this._unhideButton = _unhideButton;
            this._uiSizeText = _uiSizeText;
            this._uiComponentInfo = _uiComponentInfo;
            this._tabProperties = _tabProperties;
            this._tabPropertiesContent = _tabPropertiesContent;
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
            selectedCanvas = canvas;

            dragging = true;

            // gets the mouse position
            startPosition = Mouse.GetPosition(_uiCanvas);
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;
            App.Logger.Log("up");
            Canvas canvas = sender as Canvas;
            selectedCanvas = canvas;

            if (showHitboxes)
            {
                canvas.Background = new SolidColorBrush(Color.FromArgb(15, 157, 198, 252));
            }

            // reset ZIndex after moving it
            Panel.SetZIndex(canvas, 0);

            double roundedX = Math.Round(Canvas.GetLeft(canvas) / UIEditor.roundTo) * UIEditor.roundTo;
            double roundedY = Math.Round(Canvas.GetTop(canvas) / UIEditor.roundTo) * UIEditor.roundTo;

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
            _uiComponentInfo.Visibility = Visibility.Visible;

            dynamic rootObject = CurrentRootObject.Get();

            dynamic guid = null;

            // goes through every ui component until the guid of it matches the guid from the tag
            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                foreach (var uiComponent in layer.Internal.Elements)
                {
                    guid = uiComponent.Internal.__InstanceGuid;

                    if (debugging)
                    {
                        App.Logger.Log(Convert.ToString("Guid: " + guid));
                        App.Logger.Log(Convert.ToString("Canvas Guid: " + canvasGuid));
                    }

                    if (guid.ToString() == canvasGuid.ToString())
                    {
                        selectedElement = uiComponent;
                        break;
                    }
                }
            }

            // i dont think its possible for this to be null, but just in case
            if (selectedElement == null)
                return;

            bool useAnchor = Config.Get<bool>("UseAnchor", false);

            if (!useAnchor)
            {
                // if useAnchor is false, we remove the anchor and set the position with offset
                selectedElement.Internal.Offset.X = movedX;
                selectedElement.Internal.Offset.Y = movedY;

                selectedElement.Internal.Anchor.X = 0;
                selectedElement.Internal.Anchor.Y = 0;
            }
            else
            {
                // if useAnchor is true, we remove the offset and set the position with anchor

                var width = selectedElement.Internal.Size.X;
                var height = selectedElement.Internal.Size.Y;

                try
                {
                    if (!selectedElement.Internal.UseElementSize)
                    {
                        var widgetGuid = ((PointerRef)selectedElement.Internal.Blueprint).External.FileGuid;
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
                    width = selectedElement.Internal.Size.X;
                    height = selectedElement.Internal.Size.Y;
                }

                float x = rootObject.Object.Internal.Size.X - width;
                float y = rootObject.Object.Internal.Size.Y - height;

                // this is so we don't divide by 0 if the size of the element is equal to the ui size
                // since subtracting from the same numbers just gives you 0
                if (x == 0 || y == 0)
                {
                    App.Logger.Log($"({selectedElement.Internal.InstanceName}) Can't use anchor, moving by offset instead. Sorry!");

                    selectedElement.Internal.Offset.X = movedX;
                    selectedElement.Internal.Offset.Y = movedY;

                    selectedElement.Internal.Anchor.X = 0;
                    selectedElement.Internal.Anchor.Y = 0;
                }
                else
                {
                    selectedElement.Internal.Anchor.X = movedX / x;
                    selectedElement.Internal.Anchor.Y = movedY / y;

                    selectedElement.Internal.Offset.X = 0;
                    selectedElement.Internal.Offset.Y = 0;
                }
            }

            // saves it to the ebx so that it will show up in game or in frosty
            EbxAssetEntry ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);

            App.AssetManager.ModifyEbx(rootObject.Name, asset);

            // refreshes the data explorer so that it shows as modified on the left
            App.EditorWindow.DataExplorer.RefreshItems();

            bool isWidget = selectedElement.Internal.ToString() == "FrostySdk.Ebx.UIElementWidgetReferenceEntityData";

            Canvas newCanvas = LoadElement.Load(selectedElement, isWidget, this, rootObject, canvas.Parent as Canvas, LoadUI);
            if (newCanvas != null)
            {
                selectedCanvas = newCanvas;
            }

            // idk of any way to delete an element, so we'll just hide it
            canvas.Visibility = Visibility.Collapsed;

            _uiComponentInfo.Text =
                string.Format(
                "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}",
                selectedElement.Internal.InstanceName,
                selectedElement.Internal.Offset.X,
                selectedElement.Internal.Offset.Y,
                selectedElement.Internal.Anchor.X,
                selectedElement.Internal.Anchor.Y,
                guid.ToString());

            if (debugging)
            {
                App.Logger.Log("Saved Position");
            }

            // updates the property grid tab
            _tabPropertiesContent.Object = null;
            _tabPropertiesContent.Object = selectedElement.Internal;

            _tabProperties.IsEnabled = true;
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Canvas canvas = sender as Canvas;

                // if the wrong canvas is being selected it can be buggy so we will just cancel the movement
                if (canvas != selectedCanvas)
                {
                    dragging = false;

                    _refreshButton.Visibility = Visibility.Visible;
                    _preciseButton.Visibility = Visibility.Visible;
                    _unhideButton.Visibility = Visibility.Visible;
                    _uiSizeText.Visibility = Visibility.Visible;
                    _uiComponentInfo.Visibility = Visibility.Visible;

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
                _uiComponentInfo.Visibility = Visibility.Hidden;

                if (debugging)
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
            if (selectedCanvas == null)
                return;

            bool movementKey = false;

            double left = Canvas.GetLeft(selectedCanvas);
            double top = Canvas.GetTop(selectedCanvas);

            int move = Config.Get("ArrowKeyMovementSetting", 5);

            // this stops the arrow keys from navigating to some random place, otherwise arrow keys would just break
            if (e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Down || e.Key == Key.Right)
            {
                e.Handled = true;
            }

            // i would've used a switch but i wanted to have both WASD and arrow keys
            if (e.Key == Key.W || e.Key == Key.Up)
            {
                Canvas.SetTop(selectedCanvas, top + -move);
                movementKey = true;
            }
            else if (e.Key == Key.A || e.Key == Key.Left)
            {
                Canvas.SetLeft(selectedCanvas, left + -move);
                movementKey = true;
            }
            else if (e.Key == Key.S || e.Key == Key.Down)
            {
                Canvas.SetTop(selectedCanvas, top + move);
                movementKey = true;
            }
            else if (e.Key == Key.D || e.Key == Key.Right)
            {
                Canvas.SetLeft(selectedCanvas, left + move);
                movementKey = true;
            }
            
            // checks if its a movement key (wasd or arrow keys) so that nothing happens if you touch any other keys
            if (movementKey)
            {
                dynamic rootObject = CurrentRootObject.Get();

                bool useAnchor = Config.Get<bool>("UseAnchor", false);

                float movedX = (float)Canvas.GetLeft(selectedCanvas);
                float movedY = (float)Canvas.GetTop(selectedCanvas);

                if (!useAnchor)
                {
                    selectedElement.Internal.Offset.X = movedX;
                    selectedElement.Internal.Offset.Y = movedY;

                    selectedElement.Internal.Anchor.X = 0;
                    selectedElement.Internal.Anchor.Y = 0;
                }
                else
                {
                    // if useAnchor is true, we remove the offset and set the position with anchor

                    var width = selectedElement.Internal.Size.X;
                    var height = selectedElement.Internal.Size.Y;

                    try
                    {
                        if (!selectedElement.Internal.UseElementSize)
                        {
                            var widgetGuid = ((PointerRef)selectedElement.Internal.Blueprint).External.FileGuid;
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
                        width = selectedElement.Internal.Size.X;
                        height = selectedElement.Internal.Size.Y;
                    }

                    float x = rootObject.Object.Internal.Size.X - width;
                    float y = rootObject.Object.Internal.Size.Y - height;

                    // this is so we don't divide by 0 if the size of the element is equal to the ui size
                    if (x == 0 || y == 0)
                    {
                        App.Logger.Log($"({selectedElement.Internal.InstanceName}) Can't use anchor, moving by offset instead. Sorry!");

                        selectedElement.Internal.Offset.X = movedX;
                        selectedElement.Internal.Offset.Y = movedY;

                        selectedElement.Internal.Anchor.X = 0;
                        selectedElement.Internal.Anchor.Y = 0;
                    }
                    else
                    {
                        selectedElement.Internal.Anchor.X = movedX / x;
                        selectedElement.Internal.Anchor.Y = movedY / y;

                        selectedElement.Internal.Offset.X = 0;
                        selectedElement.Internal.Offset.Y = 0;
                    }
                }
                
                // saves it to the ebx so that it will show up in game or in frosty
                EbxAssetEntry ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
                EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);

                App.AssetManager.ModifyEbx(rootObject.Name, asset);
                
                // refreshes the data explorer so that it shows as modified on the left
                App.EditorWindow.DataExplorer.RefreshItems();

                var guid = selectedElement.Internal.__InstanceGuid;

                _uiComponentInfo.Text =
                    string.Format(
                    "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}",
                    selectedElement.Internal.InstanceName,
                    selectedElement.Internal.Offset.X,
                    selectedElement.Internal.Offset.Y,
                    selectedElement.Internal.Anchor.X,
                    selectedElement.Internal.Anchor.Y,
                    guid.ToString());

                // would've refreshed the property grid here, but i realised you have to click the element again
                // to use arrow keys on it which will refresh it anyway
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
            if (!dragging)
            {
                Canvas canvas = sender as Canvas;
                canvas.Visibility = Visibility.Hidden;
            }
        }

        public void KeyFocus(bool editorActive, UIEditor editor)
        {
            if (editorActive)
            {
                editor.Focus();
            }
        }
    }
}
